using DataModificationExample.Server.Data;

namespace DataModificationExample.DataEditor;

internal static class DbUtilities
{
    internal static ExampleDbContext GetExampleDbContext()
    {
        var factory = new DesignTimeExampleDbContextFactory();
        return factory.CreateDbContext([]);
    }
}
