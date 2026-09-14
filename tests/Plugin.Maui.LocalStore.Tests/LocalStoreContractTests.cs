using Nuventra.NuvexaDB;
using Xunit;

namespace Plugin.Maui.LocalStore.Tests;

public sealed class LocalStoreContractTests
{
    [Fact]
    public Task Sqlite_insert_find_replace_delete() =>
        InsertFindReplaceDelete(StoreBackend.Sqlite);

    [Fact]
    public Task Nuvexa_insert_find_replace_delete() =>
        InsertFindReplaceDelete(StoreBackend.Nuvexa);

    [Fact]
    public Task Sqlite_find_gte_and_or() =>
        FindGteAndOr(StoreBackend.Sqlite);

    [Fact]
    public Task Nuvexa_find_gte_and_or() =>
        FindGteAndOr(StoreBackend.Nuvexa);

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
    }

    static ILocalStore Open(StoreBackend backend)
    {
        var ext = backend == StoreBackend.Nuvexa ? ".nvx" : ".db";
        var path = Path.Combine(Path.GetTempPath(), $"localstore-{Guid.NewGuid():N}{ext}");
        var options = new LocalStoreOptions
        {
            Backend = backend,
            Path = path,
            EncryptionKey = backend == StoreBackend.Nuvexa ? "sample-key" : null
        };
        return new DisposingStore(LocalStore.Open(options), path);
    }

    static void DeleteStore(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        if (File.Exists(path + "-wal"))
        {
            File.Delete(path + "-wal");
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
                : throw new InvalidOperationException("SQLite has no NQL.");

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            DeleteStore(path);
        }
    }
}
