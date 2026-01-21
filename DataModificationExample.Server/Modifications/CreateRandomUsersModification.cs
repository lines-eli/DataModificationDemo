using DataModificationExample.Server.Data;
using DataModificationExample.Server.DataManagement;
using Microsoft.Extensions.Logging;

namespace DataModificationExample.Server.Modifications;

[DataModificationDescription("Creates 3-5 random users with unique usernames and emails each time it runs.")]
public class CreateRandomUsersModification : DataModification
{
    private readonly ExampleDbContext _dbContext;

    public CreateRandomUsersModification(ExampleDbContext dbContext, ILogger logger) : base(logger)
    {
        _dbContext = dbContext;
    }

    public override async Task RunModification(DataModificationBehavior behavior, CancellationToken cancellationToken)
    {
        var random = new Random();
        var userCount = random.Next(3, 6);
        var timestamp = DateTime.UtcNow.Ticks;

        Logger.LogInformation("Creating {UserCount} random users...", userCount);
        Logger.LogInformation("Mode: {Mode}", behavior == DataModificationBehavior.DryRun ? "Dry Run" : "Real Run");

        for (var i = 0; i < userCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Delay to demonstrate streaming and test cancellation
            Logger.LogInformation("Creating user {Current} of {Total}...", i + 1, userCount);
            await Task.Delay(300, cancellationToken);

            var userId = Guid.NewGuid();
            var username = $"user_{timestamp}_{i}_{random.Next(1000, 9999)}";
            var email = $"{username}@example.com";

            var user = new User
            {
                Id = userId,
                Username = username,
                Email = email,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            Logger.LogInformation("Created user: {Username} ({Email})", username, email);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation("Successfully saved {UserCount} users to database.", userCount);

        var totalUsers = _dbContext.Users.Count();
        Logger.LogInformation("Total users in database: {TotalUsers}", totalUsers);
    }
}
