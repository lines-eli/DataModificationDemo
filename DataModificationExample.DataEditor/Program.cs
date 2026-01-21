using DataModificationExample.DataEditor;
using DataModificationExample.Server.Modifications;

// Run a dry run of the CreateRandomUsersModification
await DataEditor.RunDryRunWithRollback<CreateRandomUsersModification>();

Console.WriteLine("DataEditor finished");
