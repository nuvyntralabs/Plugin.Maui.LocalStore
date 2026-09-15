namespace Plugin.Maui.LocalStore;

/// <summary>
/// Marks an interface as a Room-style DAO. The source generator emits an implementation
/// that wraps <see cref="IStoreCollection{T}"/> and <see cref="ILocalStore.QueryAsync{T}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class StoreDaoAttribute : Attribute
{
    public StoreDaoAttribute(string collection)
    {
        Collection = collection;
    }

    public StoreDaoAttribute(string collection, Type entityType)
    {
        Collection = collection;
        EntityType = entityType;
    }

    public string Collection { get; }

    public Type? EntityType { get; }
}
