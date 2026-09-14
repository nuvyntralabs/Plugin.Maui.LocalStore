using LevelDB;

namespace Plugin.Maui.LocalStore;

sealed class LevelDbLocalStore : JsonKvLocalStore
{
    public LevelDbLocalStore(LocalStoreOptions options)
        : base(StoreBackend.LevelDb, Open(options))
    {
    }

    static IJsonKeyValue Open(LocalStoreOptions options)
    {
        var path = StorePaths.PrepareDirectory(options, "LevelDB");
        try
        {
            return new LevelDbKeyValue(new DB(new Options { CreateIfMissing = true }, path));
        }
        catch (Exception ex) when (MissingNative.Matches(ex))
        {
            return new FileJsonKeyValue(path);
        }
        catch (Exception ex)
        {
            throw StorePaths.Wrap("LevelDB", ex);
        }
    }
}

sealed class LevelDbKeyValue(DB db) : IJsonKeyValue
{
    public string? Get(string key) => db.Get(key);

    public void Put(string key, string json) => db.Put(key, json);

    public bool Delete(string key)
    {
        if (db.Get(key) is null)
        {
            return false;
        }

        db.Delete(key);
        return true;
    }

    public IEnumerable<KeyValuePair<string, string>> Scan(string prefix)
    {
        var rows = new List<KeyValuePair<string, string>>();
        using var iterator = db.CreateIterator();
        iterator.Seek(prefix);
        while (iterator.IsValid())
        {
            var key = iterator.KeyAsString();
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                break;
            }

            rows.Add(new KeyValuePair<string, string>(key, iterator.ValueAsString()));
            iterator.Next();
        }

        return rows;
    }

    public void Dispose() => db.Dispose();
}
