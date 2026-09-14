using System.Text;
using LightningDB;

namespace Plugin.Maui.LocalStore;

sealed class LmdbLocalStore : JsonKvLocalStore
{
    public LmdbLocalStore(LocalStoreOptions options)
        : base(StoreBackend.Lmdb, Open(options))
    {
    }

    static IJsonKeyValue Open(LocalStoreOptions options)
    {
        var path = StorePaths.PrepareDirectory(options, "LMDB");
        try
        {
            var env = new LightningEnvironment(path)
            {
                MaxDatabases = 2,
                MapSize = Math.Max(16, options.CacheSizeMb) * 1024L * 1024L
            };
            env.Open();
            return new LmdbKeyValue(env);
        }
        catch (Exception ex) when (MissingNative.Matches(ex))
        {
            return new FileJsonKeyValue(path);
        }
        catch (Exception ex)
        {
            throw StorePaths.Wrap("LMDB", ex);
        }
    }
}

sealed class LmdbKeyValue(LightningEnvironment env) : IJsonKeyValue
{
    static readonly Encoding Utf8 = Encoding.UTF8;

    public string? Get(string key)
    {
        using var tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly);
        using var db = tx.OpenDatabase();
        return tx.TryGet(db, Utf8.GetBytes(key), out var value) && value is not null
            ? Utf8.GetString(value)
            : null;
    }

    public void Put(string key, string json)
    {
        using var tx = env.BeginTransaction();
        using var db = tx.OpenDatabase(configuration: new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
        tx.Put(db, Utf8.GetBytes(key), Utf8.GetBytes(json));
        tx.Commit();
    }

    public bool Delete(string key)
    {
        using var tx = env.BeginTransaction();
        using var db = tx.OpenDatabase();
        var result = tx.Delete(db, Utf8.GetBytes(key));
        if (result is MDBResultCode.Success)
        {
            tx.Commit();
            return true;
        }

        return false;
    }

    public IEnumerable<KeyValuePair<string, string>> Scan(string prefix)
    {
        using var tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly);
        using var db = tx.OpenDatabase();
        using var cursor = tx.CreateCursor(db);
        var rows = new List<KeyValuePair<string, string>>();
        var code = cursor.SetRange(Utf8.GetBytes(prefix));
        while (code == MDBResultCode.Success)
        {
            var current = cursor.GetCurrent();
            if (current.resultCode != MDBResultCode.Success)
            {
                break;
            }

            var itemKey = Utf8.GetString(current.key.CopyToNewArray());
            if (!itemKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                break;
            }

            rows.Add(new KeyValuePair<string, string>(itemKey, Utf8.GetString(current.value.CopyToNewArray())));
            var next = cursor.Next();
            code = next.resultCode;
        }

        return rows;
    }

    public void Dispose() => env.Dispose();
}
