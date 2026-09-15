using Nuventra.NuvexaDB;

namespace Plugin.Maui.LocalStore;

sealed class NuvexaLocalStore : INuvexaLocalStore
{
    readonly NuvexaDatabase _db;
    readonly string _path;

    public NuvexaLocalStore(LocalStoreOptions options)
    {
        var path = options.ResolvePath();
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path))
        {
            _db = NuvexaDatabase.Open(path, new NuvexaOpenOptions
            {
                EncryptionKey = options.EncryptionKey,
                CacheSizeMb = options.CacheSizeMb
            });
            return;
        }

        if (!options.CreateIfMissing)
        {
            throw new LocalStoreException($"Nuvexa database '{path}' was not found.");
        }

        _db = NuvexaDatabase.Create(path, new NuvexaCreateOptions
        {
            EncryptionKey = options.EncryptionKey,
            CacheSizeMb = options.CacheSizeMb
        });
    }

    public StoreBackend Backend => StoreBackend.Nuvexa;

    public StoreQueryLanguage QueryLanguage => StoreQueryLanguage.Nql;

    internal string FilePath => _path;

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new NuvexaStoreCollection<T>(_db, StoreNames.Collection(name));

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string command,
        object?[]? args = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var nql = BindNql(command, args);
        var result = await _db.ExecuteAsync(nql, cancellationToken).ConfigureAwait(false);
        return result.Documents.Select(document =>
        {
            var item = document.Deserialize<T>();
            EntityId.Set(item, document.Id);
            return item;
        }).ToList();
    }

    public async Task<int> ExecuteAsync(
        string command,
        object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var result = await _db.ExecuteAsync(BindNql(command, args), cancellationToken).ConfigureAwait(false);
        return result.Documents.Count;
    }

    public async Task<IReadOnlyList<string>> ExecuteNqlAsync(string nql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nql);
        var result = await _db.ExecuteAsync(nql, cancellationToken).ConfigureAwait(false);
        return result.Documents.Select(d => d.ToJson()).ToList();
    }

    static string BindNql(string command, object?[]? args)
    {
        if (args is not { Length: > 0 })
        {
            return command;
        }

        var named = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            named[i.ToString()] = args[i];
        }

        return StoreCommand.Bind(command, named, StoreQueryLanguage.Nql);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
