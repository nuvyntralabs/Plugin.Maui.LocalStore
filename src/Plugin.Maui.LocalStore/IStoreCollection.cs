namespace Plugin.Maui.LocalStore;

/// <summary>Room-style DAO over a named collection or table.</summary>
public interface IStoreCollection<T> where T : class, new()
{
    Task<string> InsertAsync(T item, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> InsertManyAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

    Task<T?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task ReplaceAsync(T item, CancellationToken cancellationToken = default);

    Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default);

    Task EnsureIndexAsync(params string[] fields);
}
