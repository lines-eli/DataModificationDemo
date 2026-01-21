using DataModificationExample.Server.Modifications;

namespace DataModificationExample.Server.DataManagement;

public class DataModificationRegistry : IDataModificationRegistry
{
    private readonly Dictionary<string, Type> _dataModifications = new();
    private readonly IServiceProvider _serviceProvider;

    public DataModificationRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Register<TDataModification>() where TDataModification : DataModification
    {
        var type = typeof(TDataModification);
        _dataModifications[type.Name] = type;
    }

    public IEnumerable<DataModificationInfo> GetAllDataModifications()
    {
        return _dataModifications.Values.Select(type =>
        {
            var attribute = type.GetCustomAttributes(typeof(DataModificationDescriptionAttribute), false)
                .FirstOrDefault() as DataModificationDescriptionAttribute;
            return new DataModificationInfo(type.Name, attribute?.Description ?? "No description available");
        });
    }

    public IDataModificationService? GetDataModification(string name)
    {
        if (!_dataModifications.TryGetValue(name, out var type))
        {
            return null;
        }

        return new DataModificationRunner(type, _serviceProvider);
    }
}

public static class DataModificationServiceExtensions
{
    public static IServiceCollection AddDataModificationRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IDataModificationRegistry>(serviceProvider =>
        {
            var registry = new DataModificationRegistry(serviceProvider);

            // Register all data modifications here
            registry.Register<CreateRandomUsersModification>();
            registry.Register<DeleteAllUsersModification>();

            return registry;
        });

        return services;
    }
}
