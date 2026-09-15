using System.Collections.Concurrent;

namespace Plugin.Maui.LocalStore;

/// <summary>
/// Factories for source-generated DAOs. The generator registers each <c>[StoreDao]</c> interface
/// with a <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>.
/// </summary>
public static class StoreDao
{
    static readonly ConcurrentDictionary<Type, Func<ILocalStore, object>> Factories = new();

    public static void Register<T>(Func<ILocalStore, T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        Factories[typeof(T)] = store => factory(store);
    }

    public static T Create<T>(ILocalStore store) where T : class
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!Factories.TryGetValue(typeof(T), out var factory))
        {
            throw new LocalStoreException(
                $"No generated DAO for {typeof(T).Name}. Mark the interface with [StoreDao] and rebuild.");
        }

        return (T)factory(store);
    }
}

/// <summary>Resolves a source-generated DAO from the open store.</summary>
public static class StoreDaoExtensions
{
    public static T GetDao<T>(this ILocalStore store) where T : class =>
        StoreDao.Create<T>(store);
}
