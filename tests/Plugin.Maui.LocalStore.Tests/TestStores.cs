namespace Plugin.Maui.LocalStore.Tests;

static class TestStores
{
    public static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"localstore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static LocalStoreOptions Options(StoreBackend backend, string path, bool createIfMissing = true) =>
        new()
        {
            Backend = backend,
            Path = path,
            CreateIfMissing = createIfMissing,
            EncryptionKey = Key(backend)
        };

    public static string Key(StoreBackend backend) =>
        backend is StoreBackend.Nuvexa or StoreBackend.SqlCipher ? "sample-key" : null!;

    public static string FileName(StoreBackend backend) => backend switch
    {
        StoreBackend.Nuvexa => "app.nvx",
        StoreBackend.LiteDb => "app.litedb",
        StoreBackend.DuckDb => "app.duckdb",
        StoreBackend.Realm => "app.realm",
        StoreBackend.Firebird => "app.fdb",
        StoreBackend.Lmdb => "app.lmdb",
        StoreBackend.RocksDb => "app.rocksdb",
        StoreBackend.LevelDb => "app.leveldb",
        StoreBackend.SqlCipher => "app-cipher.db",
        _ => "app.db"
    };

    public static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    public static async Task SeedUsersAsync(ILocalStore store, params Person[] people)
    {
        var users = store.GetCollection<Person>("users");
        await users.InsertManyAsync(people.Length == 0
            ?
            [
                new Person { Name = "Ada", Age = 36, Status = "active", City = "London" },
                new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
            ]
            : people);
    }
}
