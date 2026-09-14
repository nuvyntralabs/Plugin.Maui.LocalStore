using System.Collections.Concurrent;
using System.Reflection;

namespace Plugin.Maui.LocalStore;

static class EntityId
{
    static readonly ConcurrentDictionary<Type, PropertyInfo?> Cache = new();

    public static string? Get<T>(T item) where T : class
    {
        var property = Property(typeof(T));
        return property?.GetValue(item) as string;
    }

    public static void Set<T>(T item, string id) where T : class
    {
        var property = Property(typeof(T));
        if (property is null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(item, id);
    }

    public static string Require<T>(T item) where T : class
    {
        var id = Get(item);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new LocalStoreException($"{typeof(T).Name} must have a non-empty Id for replace.");
        }

        return id;
    }

    static PropertyInfo? Property(Type type) =>
        Cache.GetOrAdd(type, static t =>
            t.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)
            ?? t.GetProperty("ID", BindingFlags.Instance | BindingFlags.Public));
}
