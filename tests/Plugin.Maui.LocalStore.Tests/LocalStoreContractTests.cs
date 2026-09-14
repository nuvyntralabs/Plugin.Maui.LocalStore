using Nuventra.NuvexaDB;
using Xunit;

namespace Plugin.Maui.LocalStore.Tests;

[Collection("LocalStore")]
public sealed class LocalStoreContractTests
{
    [Fact] public Task Sqlite_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.Sqlite);
    [Fact] public Task Nuvexa_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.Nuvexa);
    [Fact] public Task LiteDb_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.LiteDb);
    [Fact] public Task SqlCipher_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.SqlCipher);
    [Fact] public Task DuckDb_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.DuckDb);
    [Fact] public Task Realm_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.Realm);
    [Fact] public Task Lmdb_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.Lmdb);
    [Fact] public Task RocksDb_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.RocksDb);
    [Fact] public Task LevelDb_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.LevelDb);
    [Fact] public Task Firebird_insert_find_replace_delete() => InsertFindReplaceDelete(StoreBackend.Firebird);

    [Fact] public Task Sqlite_find_gte_and_or() => FindGteAndOr(StoreBackend.Sqlite);
    [Fact] public Task Nuvexa_find_gte_and_or() => FindGteAndOr(StoreBackend.Nuvexa);
    [Fact] public Task LiteDb_find_gte_and_or() => FindGteAndOr(StoreBackend.LiteDb);
    [Fact] public Task SqlCipher_find_gte_and_or() => FindGteAndOr(StoreBackend.SqlCipher);
    [Fact] public Task DuckDb_find_gte_and_or() => FindGteAndOr(StoreBackend.DuckDb);
    [Fact] public Task Realm_find_gte_and_or() => FindGteAndOr(StoreBackend.Realm);
    [Fact] public Task Lmdb_find_gte_and_or() => FindGteAndOr(StoreBackend.Lmdb);
    [Fact] public Task RocksDb_find_gte_and_or() => FindGteAndOr(StoreBackend.RocksDb);
    [Fact] public Task LevelDb_find_gte_and_or() => FindGteAndOr(StoreBackend.LevelDb);
    [Fact] public Task Firebird_find_gte_and_or() => FindGteAndOr(StoreBackend.Firebird);

    [Fact]
    public async Task Nuvexa_open_without_key_is_fail_closed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"localstore-{Guid.NewGuid():N}.nvx");
        try
        {
            await using (LocalStore.Open(new LocalStoreOptions
            {
                Backend = StoreBackend.Nuvexa,
                Path = path,
                EncryptionKey = "sample-key"
            }))
            {
            }

            Assert.True(NuvexaDatabase.IsEncrypted(path));
            Assert.Throws<NuvexaEncryptionException>(() =>
                LocalStore.Open(new LocalStoreOptions
                {
                    Backend = StoreBackend.Nuvexa,
                    Path = path,
                    CreateIfMissing = false
                }));
        }
        finally
        {
            DeleteStore(path);
        }
    }

    [Fact]
    public void SqlCipher_requires_encryption_key()
    {
        var path = Path.Combine(Path.GetTempPath(), $"localstore-{Guid.NewGuid():N}.db");
        var ex = Assert.Throws<LocalStoreException>(() =>
            LocalStore.Open(new LocalStoreOptions
            {
                Backend = StoreBackend.SqlCipher,
                Path = path
            }));
        Assert.Contains("EncryptionKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Nuvexa_execute_nql()
    {
        await using var store = Open(StoreBackend.Nuvexa);
        var users = store.GetCollection<Person>("users");
        await users.InsertAsync(new Person { Name = "Ada", Age = 36 });
        var nql = store as INuvexaLocalStore
                  ?? throw new InvalidOperationException("Expected Nuvexa store.");
        var rows = await nql.ExecuteNqlAsync("db.users.find({ age: { $gte: 21 } })");
        Assert.Contains(rows, json => json.Contains("Ada", StringComparison.Ordinal));
    }

    static async Task InsertFindReplaceDelete(StoreBackend backend)
    {
        await using var store = Open(backend);
        var users = store.GetCollection<Person>("users");
            var id = await users.InsertAsync(new Person { Name = "Ada", Age = 36, City = "London" });
            Assert.False(string.IsNullOrWhiteSpace(id));

            var ada = await users.FindByIdAsync(id);
            Assert.NotNull(ada);
            Assert.Equal("Ada", ada!.Name);
            Assert.Equal(36, ada.Age);

            ada.Name = "Ada Lovelace";
            await users.ReplaceAsync(ada);
            Assert.Equal("Ada Lovelace", (await users.FindByIdAsync(id))!.Name);

        Assert.True(await users.DeleteByIdAsync(id));
        Assert.Null(await users.FindByIdAsync(id));
    }

    static async Task FindGteAndOr(StoreBackend backend)
    {
        await using var store = Open(backend);
        var users = store.GetCollection<Person>("users");
            await users.EnsureIndexAsync("Age");
            await users.EnsureIndexAsync("City", "Status");
            await users.InsertManyAsync(
            [
                new Person { Name = "Ada", Age = 36, Status = "active", City = "London" },
                new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
                new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
                new Person { Name = "Alan", Age = 42, Status = "active", City = "London" },
                new Person { Name = "Scratch", Age = 19, Status = "active", City = "Paris" }
            ]);

            var adults = await users.FindAsync(
                StoreFilter.Gte("Age", 21),
                new StoreQuery { SortBy = "Name", Limit = 10 });
            Assert.Equal(["Ada", "Alan", "Cara", "Grace"], adults.Select(p => p.Name).ToArray());

            var londonActive = await users.FindAsync(
                StoreFilter.And(StoreFilter.Eq("City", "London"), StoreFilter.Eq("Status", "active")),
                new StoreQuery { SortBy = "Age", SortDescending = true });
            Assert.Equal(["Alan", "Ada"], londonActive.Select(p => p.Name).ToArray());

            var orRows = await users.FindAsync(
                StoreFilter.Or(StoreFilter.Lt("Age", 30), StoreFilter.Eq("City", "NewYork")));
            Assert.Equal(3, orRows.Count);

            var page2 = await users.FindAsync(
                StoreFilter.Gte("Age", 21),
                new StoreQuery { SortBy = "Name", Skip = 2, Limit = 2 });
        Assert.Equal(["Cara", "Grace"], page2.Select(p => p.Name).ToArray());
    }

    static ILocalStore Open(StoreBackend backend)
    {
        if (backend == StoreBackend.Firebird)
        {
            PrepareFirebirdNative();
        }

        var path = Path.Combine(Path.GetTempPath(), $"localstore-{Guid.NewGuid():N}{Suffix(backend)}");
        var options = new LocalStoreOptions
        {
            Backend = backend,
            Path = path,
            EncryptionKey = backend is StoreBackend.Nuvexa or StoreBackend.SqlCipher ? "sample-key" : null
        };
        return new DisposingStore(LocalStore.Open(options), path);
    }

    static void PrepareFirebirdNative()
    {
        var dest = Path.Combine(Path.GetTempPath(), "Plugin.Maui.LocalStore.fb-root");
        var extracted = "/tmp/fb-pkg/Firebird.pkg/Payload/Versions/A/Resources";
        var testhost = Path.Combine(AppContext.BaseDirectory, "firebird");
        var source = Directory.Exists(Path.Combine(extracted, "lib")) ? extracted
            : Directory.Exists(Path.Combine(testhost, "lib")) ? testhost
            : null;
        if (source is not null && !File.Exists(Path.Combine(dest, "lib", "libfbclient.dylib")))
        {
            CopyDirectory(source, dest);
        }

        var client = Path.Combine(dest, "lib", "libfbclient.dylib");
        if (File.Exists(client))
        {
            Environment.SetEnvironmentVariable("FIREBIRD", dest);
            Environment.SetEnvironmentVariable("FIREBIRD_CLIENT", client);
        }
    }

    static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination), overwrite: true);
        }
    }

    static string Suffix(StoreBackend backend) => backend switch
    {
        StoreBackend.Nuvexa => ".nvx",
        StoreBackend.LiteDb => ".litedb",
        StoreBackend.DuckDb => ".duckdb",
        StoreBackend.Realm => ".realm",
        StoreBackend.Firebird => ".fdb",
        StoreBackend.Lmdb => ".lmdb",
        StoreBackend.RocksDb => ".rocksdb",
        StoreBackend.LevelDb => ".leveldb",
        _ => ".db"
    };

    static void DeleteStore(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            foreach (var extra in new[] { path + "-wal", path + ".lock", path + ".note" })
            {
                if (File.Exists(extra))
                {
                    File.Delete(extra);
                }
            }

            var management = path + ".management";
            if (Directory.Exists(management))
            {
                Directory.Delete(management, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    sealed class DisposingStore(ILocalStore inner, string path) : INuvexaLocalStore
    {
        public StoreBackend Backend => inner.Backend;

        public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
            inner.GetCollection<T>(name);

        public Task<IReadOnlyList<string>> ExecuteNqlAsync(string nql, CancellationToken cancellationToken = default) =>
            inner is INuvexaLocalStore nuvexa
                ? nuvexa.ExecuteNqlAsync(nql, cancellationToken)
                : throw new InvalidOperationException("This backend has no NQL.");

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            DeleteStore(path);
        }
    }
}
