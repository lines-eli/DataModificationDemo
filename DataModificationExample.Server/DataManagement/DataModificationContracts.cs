using System.Text.Json.Serialization;

namespace DataModificationExample.Server.DataManagement;

public interface IDataModificationService
{
    IAsyncEnumerable<DataModificationLogEvent> RunDryRun(CancellationToken cancellationToken);
    IAsyncEnumerable<DataModificationLogEvent> RunModification(CancellationToken cancellationToken);
}

public record DataModificationInfo(string Name, string Description);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DataModificationLogMessage), "log")]
[JsonDerivedType(typeof(DataModificationComplete), "complete")]
[JsonDerivedType(typeof(DataModificationError), "error")]
public abstract record DataModificationLogEvent;

public record DataModificationLogMessage(
    string Timestamp,
    string Level,
    string Category,
    string Message
) : DataModificationLogEvent;

public record DataModificationComplete(bool Success) : DataModificationLogEvent;

public record DataModificationError(string ErrorMessage, string? StackTrace = null) : DataModificationLogEvent;

public interface IDataModificationRegistry
{
    IEnumerable<DataModificationInfo> GetAllDataModifications();
    IDataModificationService? GetDataModification(string name);
}
