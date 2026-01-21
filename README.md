# Data Modification Example

A standalone example demonstrating the data modification pattern with real PostgreSQL transactions, dry-run support, and SSE streaming.

## Prerequisites

- PostgreSQL running on localhost:5432
- Node.js 18+
- .NET 8 SDK

## Quick Start

```bash
# Terminal 1: Start the server
cd DataModificationExample.Server
dotnet run

# Terminal 2: Start the client
cd client
npm install
npm run dev
```

Open http://localhost:3000 in your browser.

## Configuration

### Database Connection
Both the Server and DataEditor use the same connection string loading pattern via `DesignTimeExampleDbContextFactory`:

1. **db.json** (recommended): Create or edit `DataModificationExample.Server/db.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=data_modification_example;Username=postgres;Password=postgres"
     }
   }
   ```

2. **Environment variable**: Set `ConnectionStrings__DefaultConnection`

3. **Default**: Falls back to `Host=localhost;Port=5432;Database=data_modification_example;Username=postgres;Password=postgres`

**Safety check**: If connecting to a non-localhost database, you'll be prompted to confirm before proceeding.

## Projects

- **DataModificationExample.Server** - .NET web server with API endpoints
- **DataModificationExample.DataEditor** - Console app with `RunDryRunWithRollback<T>()` helper
- **client/** - React + Vite web UI

## How to Add a New Modification

### 1. Create the modification class

Create a new file in `DataModificationExample.Server/Modifications/`:

```csharp
using DataModificationExample.Server.Data;
using DataModificationExample.Server.DataManagement;
using Microsoft.Extensions.Logging;

namespace DataModificationExample.Server.Modifications;

[DataModificationDescription("Description shown in the UI")]
public class MyNewModification : DataModification
{
    private readonly ExampleDbContext _dbContext;

    public MyNewModification(ExampleDbContext dbContext, ILogger logger) : base(logger)
    {
        _dbContext = dbContext;
    }

    public override async Task RunModification(DataModificationBehavior behavior, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting modification...");
        Logger.LogInformation("Mode: {Mode}", behavior == DataModificationBehavior.DryRun ? "Dry Run" : "Real Run");

        // Your modification logic here
        // Use cancellationToken.ThrowIfCancellationRequested() to support cancellation
        // Use Logger.LogInformation() to stream progress to the UI

        await _dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation("Done!");
    }
}
```

### 2. Register the modification

In `DataModificationExample.Server/DataManagement/DataModificationRegistry.cs`, add:

```csharp
registry.Register<MyNewModification>();
```

### 3. Test it

Run the server and client, then use the web UI to dry-run your modification.

## DataEditor (Console)

For quick local testing without the web UI:

```bash
cd DataModificationExample.DataEditor
dotnet run
```

Edit `Program.cs` to run your modification:

```csharp
await DataEditor.RunDryRunWithRollback<MyNewModification>();
```

## Key Concepts

### Dry Run vs Execute
- **Dry Run**: Runs the modification in a transaction that is always rolled back. Safe to test.
- **Execute**: Runs the modification and commits the transaction. Requires typing the modification name to confirm.

### The `DataModificationBehavior` Parameter
The `behavior` parameter passed to `RunModification()` is **informational only** - it tells the modification whether it's a dry run so it can log appropriately (e.g., "Mode: Dry Run"). The actual rollback/commit is handled by the runner via the database transaction, not by the modification itself. This means:
- You don't need to check `behavior` to decide whether to save changes
- Even if your modification ignores the behavior flag, dry runs will still roll back
- Use it for logging and UI feedback

### Cancellation
Click Cancel during a running modification to abort. The transaction is rolled back, so no partial changes are persisted.

### Transaction Safety
All modifications run inside a database transaction. If anything fails (or is cancelled), everything is rolled back automatically.
