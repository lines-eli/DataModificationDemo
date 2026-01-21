using Microsoft.Extensions.Logging;

namespace DataModificationExample.Server.DataManagement;

[AttributeUsage(AttributeTargets.Class)]
public class DataModificationDescriptionAttribute : Attribute
{
    public string Description { get; }

    public DataModificationDescriptionAttribute(string description)
    {
        Description = description;
    }
}

public abstract class DataModification
{
    protected readonly ILogger Logger;

    protected DataModification(ILogger logger)
    {
        Logger = logger;
    }

    public abstract Task RunModification(DataModificationBehavior behavior, CancellationToken cancellationToken);
}
