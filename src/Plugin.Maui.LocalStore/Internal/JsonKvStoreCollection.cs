namespace Plugin.Maui.LocalStore;

interface IJsonKeyValue : IDisposable
{
    string? Get(string key);

    void Put(string key, string json);

    bool Delete(string key);

    IEnumerable<KeyValuePair<string, string>> Scan(string prefix);
}

sealed class JsonKvStoreCollection<T> : IStoreCollection<T> where T : class, new()
{
    readonly IJsonKeyValue _kv;
    readonly string _collection;
    readonly string _prefix;

    public JsonKvStoreCollection(IJsonKeyValue kv, string name)
    {
        PocoColumns.RequireId<T>();
        _kv = kv;
        _collection = name;
        _prefix = name + "\u001f";
    }

    public Task<string> InsertAsync(T item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = EnsureId(item);
        _kv.Put(_prefix + id, JsonEntity.Serialize(item));
        return Task.FromResult(id);
    }

    public async Task<IReadOnlyList<string>> InsertManyAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        foreach (var item in items)
        {
            ids.Add(await InsertAsync(item, cancellationToken).ConfigureAwait(false));
        }

        return ids;
    }

    public Task<T?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();
        var json = _kv.Get(_prefix + id);
        return Task.FromResult(json is null ? null : Read(json, id));
    }

    public Task ReplaceAsync(T item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = EntityId.Require(item);
        if (_kv.Get(_prefix + id) is null)
        {
            throw new LocalStoreException($"No row with Id '{id}'.");
        }

        _kv.Put(_prefix + id, JsonEntity.Serialize(item));
        return Task.CompletedTask;
    }

    public Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_kv.Delete(_prefix + id));
    }

    public Task<IReadOnlyList<T>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = _kv.Scan(_prefix)
            .Select(pair => Read(pair.Value, pair.Key[_prefix.Length..]))
            .ToList();
        return Task.FromResult(StoreFilterEvaluator.Apply(rows, filter, query));
    }

    public Task EnsureIndexAsync(params string[] fields)
    {
        if (fields is { Length: > 0 })
        {
            foreach (var field in fields)
            {
                StoreNames.Property(field);
            }
        }

        return Task.CompletedTask;
    }

    static string EnsureId(T item)
    {
        var id = EntityId.Get(item);
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        id = Guid.NewGuid().ToString("N");
        EntityId.Set(item, id);
        return id;
    }

    static T Read(string json, string id)
    {
        var item = JsonEntity.Deserialize<T>(json);
        EntityId.Set(item, id);
        return item;
    }
}

abstract class JsonKvLocalStore : ILocalStore
{
    readonly IJsonKeyValue _kv;

    protected JsonKvLocalStore(StoreBackend backend, IJsonKeyValue kv)
    {
        Backend = backend;
        _kv = kv;
    }

    public StoreBackend Backend { get; }

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new JsonKvStoreCollection<T>(_kv, StoreNames.Collection(name));

    public ValueTask DisposeAsync()
    {
        _kv.Dispose();
        return ValueTask.CompletedTask;
    }
}
