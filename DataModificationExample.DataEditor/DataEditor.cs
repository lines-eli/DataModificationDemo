using DataModificationExample.Server.Data;
using DataModificationExample.Server.DataManagement;
using Microsoft.Extensions.Logging;

namespace DataModificationExample.DataEditor;

public static class DataEditor
{
    /// <summary>
    /// Runs a data modification as a dry run, wrapping it in a transaction that is always rolled back.
    /// This ensures dry runs never persist changes to the database.
    /// </summary>
    public static async Task RunDryRunWithRollback<T>() where T : DataModification
    {
        var db = DbUtilities.GetExampleDbContext();
        db.Database.EnsureCreated();

        await using var transaction = await db.Database.BeginTransactionAsync();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<T>();
        var modification = (T)Activator.CreateInstance(typeof(T), db, logger)!;

        await modification.RunModification(DataModificationBehavior.DryRun, CancellationToken.None);

        Console.WriteLine("Dry run complete, rolling back transaction...");
        await transaction.RollbackAsync();
    }
}
