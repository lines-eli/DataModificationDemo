using DataModificationExample.Server.Data;
using DataModificationExample.Server.DataManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataModificationExample.Server.Modifications;

[DataModificationDescription("Deletes all users from the database. Use with caution!")]
public class DeleteAllUsersModification : DataModification
{
    private readonly ExampleDbContext _dbContext;

    public DeleteAllUsersModification(ExampleDbContext dbContext, ILogger logger) : base(logger)
    {
        _dbContext = dbContext;
    }

    public override async Task RunModification(DataModificationBehavior behavior, CancellationToken cancellationToken)
    {
        var userCount = await _dbContext.Users.CountAsync(cancellationToken);

        Logger.LogInformation("Found {UserCount} users to delete", userCount);
        Logger.LogInformation("Mode: {Mode}", behavior == DataModificationBehavior.DryRun ? "Dry Run" : "Real Run");

        if (userCount == 0)
        {
            Logger.LogInformation("No users to delete");
            return;
        }

        // Delete users in batches to show progress
        var users = await _dbContext.Users.ToListAsync(cancellationToken);
        var deleted = 0;

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _dbContext.Users.Remove(user);
            deleted++;

            Logger.LogInformation("Deleted user {Deleted} of {Total}: {Username}",
                deleted, userCount, user.Username);

            // Small delay to demonstrate streaming
            if (deleted < userCount)
            {
                await Task.Delay(200, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation("Successfully deleted {DeletedCount} users", deleted);
    }
}
