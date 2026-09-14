using LiteDB;

namespace Plugin.Maui.LocalStore;

sealed class LiteDbLocalStore : ILocalStore
{
    readonly LiteDatabase _db;
    readonly SemaphoreSlim _gate = new(1, 1);

    public LiteDbLocalStore(LocalStoreOptions options)
    {
        var path = StorePaths.PrepareFile(options, "LiteDB");
        try
        {
            var connection = new ConnectionString
            {
                Filename = path,
                Connection = ConnectionType.Shared
            };
            if (!string.IsNullOrWhiteSpace(options.EncryptionKey))
            {
                connection.Password = options.EncryptionKey;
            }

            _db = new LiteDatabase(connection);
        }
        catch (Exception ex)
        {
            throw StorePaths.Wrap("LiteDB", ex);
        }
    }

    public StoreBackend Backend => StoreBackend.LiteDb;

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new LiteDbStoreCollection<T>(this, StoreNames.Collection(name));

    internal ILiteCollection<T> Collection<T>(string name) where T : class, new() =>
        _db.GetCollection<T>(name);

    internal TResult Locked<TResult>(Func<TResult> work)
    {
        _gate.Wait();
        try
        {
            return work();
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}

sealed class LiteDbStoreCollection<T> : IStoreCollection<T> where T : class, new()
{
    readonly LiteDbLocalStore _store;
    readonly string _name;

    public LiteDbStoreCollection(LiteDbLocalStore store, string name)
    {
        PocoColumns.RequireId<T>();
        _store = store;
        _name = name;
    }

    public Task<string> InsertAsync(T item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Locked(() =>
        {
            var id = EntityId.Get(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
                EntityId.Set(item, id);
            }

            _store.Collection<T>(_name).Insert(item);
            return id;
        }));
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
        return Task.FromResult<T?>(_store.Locked(() => _store.Collection<T>(_name).FindById(id)));
    }

    public Task ReplaceAsync(T item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EntityId.Require(item);
        _store.Locked(() =>
        {
            if (!_store.Collection<T>(_name).Update(item))
            {
                throw new LocalStoreException($"No row with Id '{EntityId.Get(item)}'.");
            }

            return 0;
        });
        return Task.CompletedTask;
    }

    public Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Locked(() => _store.Collection<T>(_name).Delete(id)));
    }

    public Task<IReadOnlyList<T>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Locked(() =>
            StoreFilterEvaluator.Apply(_store.Collection<T>(_name).FindAll(), filter, query)));
    }

    public Task EnsureIndexAsync(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
        {
            return Task.CompletedTask;
        }

        _store.Locked(() =>
        {
            var col = _store.Collection<T>(_name);
            foreach (var field in fields)
            {
                col.EnsureIndex(StoreNames.Property(field));
            }

            return 0;
        });
        return Task.CompletedTask;
    }
}
