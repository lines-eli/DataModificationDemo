using DataModificationExample.Server;
using DataModificationExample.Server.Data;
using DataModificationExample.Server.DataManagement;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json");

builder.Services.AddDbContext<ExampleDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDataModificationRegistry();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

app.MapDataModificationEndpoints();

app.MapGet("/api/users", async (ExampleDbContext db) =>
{
    var users = await db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    return TypedResults.Ok(users);
});

Console.WriteLine("Server running at http://localhost:5000");
Console.WriteLine("Run 'npm run dev' in client/ folder, then open http://localhost:3000");

app.Run("http://localhost:5000");
