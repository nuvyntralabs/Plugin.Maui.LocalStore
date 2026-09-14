using Nuventra.NuvexaDB;

namespace Plugin.Maui.LocalStore;

sealed class NuvexaLocalStore : INuvexaLocalStore
{
    readonly NuvexaDatabase _db;

    public NuvexaLocalStore(LocalStoreOptions options)
    {
        var path = options.ResolvePath();
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

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new NuvexaStoreCollection<T>(_db, StoreNames.Collection(name));

    public async Task<IReadOnlyList<string>> ExecuteNqlAsync(string nql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nql);
        var result = await _db.ExecuteAsync(nql, cancellationToken).ConfigureAwait(false);
        return result.Documents.Select(d => d.ToJson()).ToList();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
