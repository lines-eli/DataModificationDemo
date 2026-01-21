using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DataModificationExample.Server.Data;
using Microsoft.Extensions.Logging;

namespace DataModificationExample.Server.DataManagement;

internal class DataModificationRunner : IDataModificationService
{
    private readonly Type _dataModificationType;
    private readonly IServiceProvider _serviceProvider;

    public string Name { get; }
    public string Description { get; }

    public DataModificationRunner(Type dataModificationType, IServiceProvider serviceProvider)
    {
        _dataModificationType = dataModificationType;
        _serviceProvider = serviceProvider;

        Name = dataModificationType.Name;

        var attribute = dataModificationType.GetCustomAttributes(typeof(DataModificationDescriptionAttribute), false)
            .FirstOrDefault() as DataModificationDescriptionAttribute;
        Description = attribute?.Description ?? "No description available";
    }

    public IAsyncEnumerable<DataModificationLogEvent> RunDryRun(CancellationToken cancellationToken)
    {
        return ExecuteModification(DataModificationBehavior.DryRun, cancellationToken);
    }

    public IAsyncEnumerable<DataModificationLogEvent> RunModification(CancellationToken cancellationToken)
    {
        return ExecuteModification(DataModificationBehavior.PerformModification, cancellationToken);
    }

    private async IAsyncEnumerable<DataModificationLogEvent> ExecuteModification(
        DataModificationBehavior behavior,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<DataModificationLogEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var modificationTask = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();

                var channelLoggerProvider = new ChannelLoggerProvider(channel.Writer, LogLevel.Information);

                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddConsole();
                    builder.AddProvider(channelLoggerProvider);
                });

                var logger = loggerFactory.CreateLogger(_dataModificationType.Name);

                var dataModification = (DataModification)ActivatorUtilities.CreateInstance(
                    scope.ServiceProvider,
                    _dataModificationType,
                    logger);

                var isDryRun = behavior == DataModificationBehavior.DryRun;
                logger.LogInformation(isDryRun
                    ? "Starting dry run in database transaction (will roll back)..."
                    : "Starting data modification in database transaction...");

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    await dataModification.RunModification(behavior, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (isDryRun)
                    {
                        logger.LogInformation("Dry run complete, rolling back transaction...");
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    else
                    {
                        await transaction.CommitAsync(cancellationToken);
                        logger.LogInformation("Data modification transaction committed successfully.");
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Data modification cancelled by client, rolling back transaction...");
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Data modification failed, rolling back transaction...");
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }

                await channel.Writer.WriteAsync(new DataModificationComplete(true), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await channel.Writer.WriteAsync(new DataModificationError("Operation was cancelled", null), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await channel.Writer.WriteAsync(new DataModificationError(ex.Message, ex.ToString()), CancellationToken.None);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var logEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return logEvent;
        }

        await modificationTask;
    }
}
