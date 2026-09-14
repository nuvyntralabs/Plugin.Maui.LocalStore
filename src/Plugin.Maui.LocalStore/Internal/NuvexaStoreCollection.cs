using Nuventra.NuvexaDB;

namespace Plugin.Maui.LocalStore;

sealed class NuvexaStoreCollection<T> : IStoreCollection<T> where T : class, new()
{
    readonly NuvexaDatabase _db;
    readonly string _name;

    public NuvexaStoreCollection(NuvexaDatabase db, string name)
    {
        _db = db;
        _name = name;
    }

    public async Task<string> InsertAsync(T item, CancellationToken cancellationToken = default)
    {
        var document = ToDocument(item);
        var id = await _db.GetCollection(_name).InsertAsync(document, cancellationToken).ConfigureAwait(false);
        EntityId.Set(item, id);
        return id;
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

    public async Task<T?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var document = await _db.GetCollection(_name).FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return document is null ? null : FromDocument(document);
    }

    public async Task ReplaceAsync(T item, CancellationToken cancellationToken = default)
    {
        var document = ToDocument(item);
        if (string.IsNullOrWhiteSpace(document.Id))
        {
            throw new LocalStoreException($"{typeof(T).Name} must have a non-empty Id for replace.");
        }

        await _db.GetCollection(_name).ReplaceAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _db.GetCollection(_name).DeleteByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var fluent = _db.GetCollection(_name).Find(StoreFilterMapper.ToNuvexa(filter));
        if (!string.IsNullOrWhiteSpace(query?.SortBy))
        {
            fluent = fluent.Sort(StoreFilterMapper.JsonPath(query.SortBy), !query.SortDescending);
        }

        if (query is { Skip: > 0 })
        {
            fluent = fluent.Skip(query.Skip);
        }

        if (query is { Limit: > 0 })
        {
            fluent = fluent.Limit(query.Limit);
        }

        var rows = await fluent.ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(FromDocument).ToList();
    }

    public Task EnsureIndexAsync(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
        {
            return Task.CompletedTask;
        }

        var paths = fields.Select(StoreFilterMapper.JsonPath).ToArray();
        return paths.Length == 1
            ? _db.GetCollection(_name).EnsureIndexAsync(paths[0], cancellationToken: default)
            : _db.GetCollection(_name).EnsureIndexAsync(paths, cancellationToken: default);
    }

    static NuvexaDocument ToDocument(T item)
    {
        var document = NuvexaDocument.FromObject(item);
        var id = EntityId.Get(item);
        if (!string.IsNullOrWhiteSpace(id))
        {
            document.Id = id;
        }

        return document;
    }

    static T FromDocument(NuvexaDocument document)
    {
        var item = document.Deserialize<T>();
        EntityId.Set(item, document.Id);
        return item;
    }
}
