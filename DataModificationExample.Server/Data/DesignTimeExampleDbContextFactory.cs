using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataModificationExample.Server.Data;

public class DesignTimeExampleDbContextFactory : IDesignTimeDbContextFactory<ExampleDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=data_modification_example;Username=postgres;Password=postgres";

    public ExampleDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<ExampleDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ExampleDbContext(optionsBuilder.Options);
    }

    public static string GetConnectionString()
    {
        var basePath = Directory.GetCurrentDirectory();
        var connectionString = DefaultConnectionString;

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("db.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var configuredConnectionString = config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(configuredConnectionString))
            {
                connectionString = configuredConnectionString;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error reading connection string from db.json: {e.Message}");
            Console.WriteLine("Using default connection string.");
        }

        // Safety check for non-local databases
        if (!connectionString.Contains("localhost"))
        {
            Console.WriteLine("⚠️  WARNING: Connecting to a remote database!");
            Console.WriteLine($"Connection: {connectionString.Split(';')[0]}");
            Console.Write("Continue? (y/n): ");
            var input = Console.ReadLine()?.Trim().ToLower();
            if (input != "y" && input != "yes")
            {
                Console.WriteLine("Aborting.");
                Environment.Exit(0);
            }
        }

        return connectionString;
    }
}
