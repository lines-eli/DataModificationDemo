using Microsoft.EntityFrameworkCore;

namespace DataModificationExample.Server.Data;

public class ExampleDbContext : DbContext
{
    public ExampleDbContext(DbContextOptions<ExampleDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
}
