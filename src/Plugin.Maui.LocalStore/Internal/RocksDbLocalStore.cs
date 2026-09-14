using RocksDbSharp;

namespace Plugin.Maui.LocalStore;

sealed class RocksDbLocalStore : JsonKvLocalStore
{
    public RocksDbLocalStore(LocalStoreOptions options)
        : base(StoreBackend.RocksDb, Open(options))
    {
    }

    static IJsonKeyValue Open(LocalStoreOptions options)
    {
        var path = StorePaths.PrepareDirectory(options, "RocksDB");
        try
        {
            var db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), path);
            return new RocksDbKeyValue(db);
        }
        catch (Exception ex) when (MissingNative.Matches(ex))
        {
            return new FileJsonKeyValue(path);
        }
        catch (Exception ex)
        {
            throw StorePaths.Wrap("RocksDB", ex);
        }
    }
}

sealed class RocksDbKeyValue(RocksDb db) : IJsonKeyValue
{
    public string? Get(string key) => db.Get(key);

    public void Put(string key, string json) => db.Put(key, json);

    public bool Delete(string key)
    {
        if (db.Get(key) is null)
        {
            return false;
        }

        db.Remove(key);
        return true;
    }

    public IEnumerable<KeyValuePair<string, string>> Scan(string prefix)
    {
        using var iterator = db.NewIterator();
        iterator.Seek(prefix);
        var rows = new List<KeyValuePair<string, string>>();
        while (iterator.Valid())
        {
            var key = iterator.StringKey();
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                break;
            }

            rows.Add(new KeyValuePair<string, string>(key, iterator.StringValue()));
            iterator.Next();
        }

        return rows;
    }

    public void Dispose() => db.Dispose();
}
