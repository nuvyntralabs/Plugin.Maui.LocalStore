using Xunit;

namespace Plugin.Maui.LocalStore.Tests;

[Collection("LocalStore")]
public sealed class LocalStoreQueryDaoTests
{
    [Fact]
    public async Task Sqlite_query_sql_maps_poco()
    {
        await using var store = Open(StoreBackend.Sqlite);
        await TestStores.SeedUsersAsync(store);
        Assert.Equal(StoreQueryLanguage.Sql, store.QueryLanguage);

        var rows = await store.QueryAsync<Person>("SELECT * FROM users WHERE Age >= ? ORDER BY Name", [21]);
        Assert.Equal(["Ada", "Alan"], rows.Select(person => person.Name).ToArray());
    }

    [Fact]
    public async Task SqlCipher_query_sql()
    {
        await using var store = Open(StoreBackend.SqlCipher);
        await TestStores.SeedUsersAsync(store,
            new Person { Name = "Ada", Age = 36, City = "London" });
        var rows = await store.QueryAsync<Person>("SELECT * FROM users WHERE City = ?", ["London"]);
        Assert.Equal("Ada", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task Nuvexa_query_nql_maps_poco()
    {
        await using var store = Open(StoreBackend.Nuvexa);
        await TestStores.SeedUsersAsync(store);
        Assert.Equal(StoreQueryLanguage.Nql, store.QueryLanguage);

        var rows = await store.QueryAsync<Person>("db.users.find({ age: { $gte: 21 } }).sort({ name: 1 })");
        Assert.Equal(["Ada", "Alan"], rows.Select(person => person.Name).ToArray());
    }

    [Fact]
    public async Task Sqlite_execute_sql_updates_row()
    {
        await using var store = Open(StoreBackend.Sqlite);
        await TestStores.SeedUsersAsync(store,
            new Person { Name = "Ada", Age = 36, City = "London" });
        var updated = await store.ExecuteAsync("UPDATE users SET City = ? WHERE Name = ?", ["Paris", "Ada"]);
        Assert.Equal(1, updated);
        Assert.Equal("Paris", (await store.GetCollection<Person>("users").FindAsync()).Single().City);
    }

    [Fact]
    public async Task Json_fallback_has_no_query_language()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"localstore-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            await using ILocalStore store = new FileJsonLocalStore(StoreBackend.DuckDb, dir);
            Assert.Equal(StoreQueryLanguage.None, store.QueryLanguage);
            var ex = await Assert.ThrowsAsync<LocalStoreException>(() =>
                store.QueryAsync<Person>("SELECT * FROM users"));
            Assert.Contains("no SQL or NQL", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LiteDb_and_realm_have_no_query_language()
    {
        await using var lite = Open(StoreBackend.LiteDb);
        await using var realm = Open(StoreBackend.Realm);
        Assert.Equal(StoreQueryLanguage.None, lite.QueryLanguage);
        Assert.Equal(StoreQueryLanguage.None, realm.QueryLanguage);

        var liteEx = await Assert.ThrowsAsync<LocalStoreException>(() =>
            lite.QueryAsync<Person>("SELECT * FROM users"));
        var realmEx = await Assert.ThrowsAsync<LocalStoreException>(() =>
            realm.ExecuteAsync("SELECT 1"));
        Assert.Contains("no SQL or NQL", liteEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no SQL or NQL", realmEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generated_dao_sqlite_insert_find_and_raw_sql()
    {
        await using var store = Open(StoreBackend.Sqlite);
        var dao = store.GetDao<IPersonDao>();
        var id = await dao.InsertAsync(new Person { Name = "Ada", Age = 36, City = "London" });
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal("Ada", (await dao.FindByIdAsync(id))!.Name);

        await dao.InsertAsync(new Person { Name = "Scratch", Age = 19, City = "Paris" });
        var adults = await dao.FindAdultsAsync(21);
        Assert.Equal("Ada", Assert.Single(adults).Name);

        var london = await dao.FindAsync(StoreFilter.Eq("City", "London"));
        Assert.Equal("Ada", Assert.Single(london).Name);
    }

    [Fact]
    public async Task Generated_dao_nuvexa_raw_nql()
    {
        await using var store = Open(StoreBackend.Nuvexa);
        var dao = store.GetDao<IPersonDao>();
        await dao.InsertAsync(new Person { Name = "Ada", Age = 36, City = "London" });
        await dao.InsertAsync(new Person { Name = "Scratch", Age = 19, City = "Paris" });
        var adults = await dao.FindAdultsAsync(21);
        Assert.Equal("Ada", Assert.Single(adults).Name);
    }

    [Fact]
    public void StoreCommand_binds_sql_and_nql_placeholders()
    {
        var args = new Dictionary<string, object?>
        {
            ["minAge"] = 21,
            ["city"] = "London"
        };

        var sql = StoreCommand.Bind(
            "SELECT * FROM users WHERE Age >= {minAge} AND City = {city}",
            args,
            StoreQueryLanguage.Sql);
        Assert.Equal("SELECT * FROM users WHERE Age >= 21 AND City = 'London'", sql);

        var nql = StoreCommand.Bind(
            "db.users.find({ age: { $gte: {minAge} }, city: {city} })",
            args,
            StoreQueryLanguage.Nql);
        Assert.Contains("$gte: 21", nql, StringComparison.Ordinal);
        Assert.Contains("\"London\"", nql, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreCommand_rejects_none_and_unknown_placeholder()
    {
        var args = new Dictionary<string, object?> { ["minAge"] = 21 };
        Assert.Throws<LocalStoreException>(() =>
            StoreCommand.Bind("SELECT 1", args, StoreQueryLanguage.None));
        Assert.Throws<LocalStoreException>(() =>
            StoreCommand.Bind("SELECT {missing}", args, StoreQueryLanguage.Sql));
    }

    [Fact]
    public async Task GetDao_without_generated_interface_throws()
    {
        await using var store = Open(StoreBackend.Sqlite);
        var ex = Assert.Throws<LocalStoreException>(() => store.GetDao<IDisposable>());
        Assert.Contains("[StoreDao]", ex.Message, StringComparison.Ordinal);
    }

    static ILocalStore Open(StoreBackend backend)
    {
        var path = Path.Combine(Path.GetTempPath(), $"localstore-q-{Guid.NewGuid():N}-{TestStores.FileName(backend)}");
        return new QueryDaoStore(LocalStore.Open(TestStores.Options(backend, path)), path);
    }

    sealed class QueryDaoStore(ILocalStore inner, string path) : INuvexaLocalStore
    {
        public StoreBackend Backend => inner.Backend;

        public StoreQueryLanguage QueryLanguage => inner.QueryLanguage;

        public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
            inner.GetCollection<T>(name);

        public Task<IReadOnlyList<T>> QueryAsync<T>(
            string command,
            object?[]? args = null,
            CancellationToken cancellationToken = default) where T : class, new() =>
            inner.QueryAsync<T>(command, args, cancellationToken);

        public Task<int> ExecuteAsync(
            string command,
            object?[]? args = null,
            CancellationToken cancellationToken = default) =>
            inner.ExecuteAsync(command, args, cancellationToken);

        public Task<IReadOnlyList<string>> ExecuteNqlAsync(string nql, CancellationToken cancellationToken = default) =>
            inner is INuvexaLocalStore nuvexa
                ? nuvexa.ExecuteNqlAsync(nql, cancellationToken)
                : throw new InvalidOperationException("This backend has no NQL.");

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
