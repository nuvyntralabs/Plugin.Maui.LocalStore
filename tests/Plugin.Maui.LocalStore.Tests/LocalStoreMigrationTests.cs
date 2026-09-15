using Xunit;

namespace Plugin.Maui.LocalStore.Tests;

[Collection("LocalStore")]
public sealed class LocalStoreMigrationTests
{
    [Fact]
    public async Task Migrate_sqlite_to_nuvexa_copies_rows()
    {
        var folder = TestStores.NewFolder();
        var sqlitePath = Path.Combine(folder, "app.db");
        var nuvexaPath = Path.Combine(folder, "app.nvx");
        try
        {
            await using (var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath)))
            {
                await TestStores.SeedUsersAsync(sqlite);
            }

            var result = await LocalStore.MigrateAsync(
                TestStores.Options(StoreBackend.Sqlite, sqlitePath),
                TestStores.Options(StoreBackend.Nuvexa, nuvexaPath).Map<Person>("users"));

            Assert.False(result.Skipped);
            Assert.Equal(StoreBackend.Sqlite, result.From);
            Assert.Equal(StoreBackend.Nuvexa, result.To);
            Assert.Equal(1, result.Collections);
            Assert.Equal(2, result.Documents);

            await using var nuvexa = LocalStore.Create(
                TestStores.Options(StoreBackend.Nuvexa, nuvexaPath, createIfMissing: false));
            var names = (await nuvexa.GetCollection<Person>("users")
                    .FindAsync(query: new StoreQuery { SortBy = "Name" }))
                .Select(person => person.Name)
                .ToArray();
            Assert.Equal(["Ada", "Alan"], names);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task Migrate_nuvexa_to_sqlite_round_trip()
    {
        var folder = TestStores.NewFolder();
        var nuvexaPath = Path.Combine(folder, "app.nvx");
        var sqlitePath = Path.Combine(folder, "app.db");
        try
        {
            await using (var nuvexa = LocalStore.Create(TestStores.Options(StoreBackend.Nuvexa, nuvexaPath)))
            {
                await TestStores.SeedUsersAsync(nuvexa,
                    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" });
            }

            var result = await LocalStore.MigrateAsync(
                TestStores.Options(StoreBackend.Nuvexa, nuvexaPath),
                TestStores.Options(StoreBackend.Sqlite, sqlitePath).Map<Person>("users"));

            Assert.False(result.Skipped);
            Assert.Equal(1, result.Documents);

            await using var sqlite = LocalStore.Create(
                TestStores.Options(StoreBackend.Sqlite, sqlitePath, createIfMissing: false));
            var cara = Assert.Single(await sqlite.GetCollection<Person>("users").FindAsync());
            Assert.Equal("Cara", cara.Name);
            Assert.Equal(21, cara.Age);
            Assert.Equal("Bengaluru", cara.City);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task Migrate_litedb_to_sqlite()
    {
        var folder = TestStores.NewFolder();
        var litePath = Path.Combine(folder, "app.litedb");
        var sqlitePath = Path.Combine(folder, "app.db");
        try
        {
            await using (var lite = LocalStore.Create(TestStores.Options(StoreBackend.LiteDb, litePath)))
            {
                await TestStores.SeedUsersAsync(lite,
                    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" });
            }

            var result = await LocalStore.MigrateAsync(
                TestStores.Options(StoreBackend.LiteDb, litePath),
                TestStores.Options(StoreBackend.Sqlite, sqlitePath).Map<Person>("users"));

            Assert.False(result.Skipped);
            Assert.Equal(1, result.Documents);

            await using var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath, false));
            Assert.Equal("Grace", Assert.Single(await sqlite.GetCollection<Person>("users").FindAsync()).Name);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task AutoMigrate_on_open_copies_empty_destination()
    {
        var folder = TestStores.NewFolder();
        var sqlitePath = Path.Combine(folder, "app.db");
        var nuvexaPath = Path.Combine(folder, "app.nvx");
        try
        {
            await using (var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath)))
            {
                await TestStores.SeedUsersAsync(sqlite,
                    new Person { Name = "Ada", Age = 36, City = "London" });
            }

            var options = TestStores.Options(StoreBackend.Nuvexa, nuvexaPath);
            options.AutoMigrate = true;
            options.MigrateFrom = StoreBackend.Sqlite;
            options.MigrateFromPath = sqlitePath;
            options.Map<Person>("users");

            await using var opened = await LocalStore.OpenAsync(options);
            var ada = Assert.Single(await opened.GetCollection<Person>("users").FindAsync());
            Assert.Equal("Ada", ada.Name);
            Assert.Equal(36, ada.Age);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task AutoMigrate_skips_when_destination_has_rows()
    {
        var folder = TestStores.NewFolder();
        var sqlitePath = Path.Combine(folder, "app.db");
        var nuvexaPath = Path.Combine(folder, "app.nvx");
        try
        {
            await using (var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath)))
            {
                await TestStores.SeedUsersAsync(sqlite,
                    new Person { Name = "Ada", Age = 36, City = "London" });
            }

            await using (var nuvexa = LocalStore.Create(TestStores.Options(StoreBackend.Nuvexa, nuvexaPath)))
            {
                await TestStores.SeedUsersAsync(nuvexa,
                    new Person { Name = "Existing", Age = 99, City = "Paris" });
            }

            await using var source = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath, false));
            await using var dest = LocalStore.Create(TestStores.Options(StoreBackend.Nuvexa, nuvexaPath, false));
            var result = await LocalStore.MigrateAsync(
                source,
                dest,
                [StoreCollectionMap.For<Person>("users")]);

            Assert.True(result.Skipped);
            Assert.Contains("already has rows", result.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Existing", Assert.Single(await dest.GetCollection<Person>("users").FindAsync()).Name);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task AutoMigrate_without_map_throws()
    {
        var folder = TestStores.NewFolder();
        try
        {
            var ex = await Assert.ThrowsAsync<LocalStoreException>(() =>
                LocalStore.OpenAsync(new LocalStoreOptions
                {
                    Backend = StoreBackend.Nuvexa,
                    Path = Path.Combine(folder, "app.nvx"),
                    EncryptionKey = "sample-key",
                    AutoMigrate = true,
                    MigrateFrom = StoreBackend.Sqlite
                }));
            Assert.Contains("Map<T>", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task AutoMigrate_discovers_single_sibling()
    {
        var folder = TestStores.NewFolder();
        var sqlitePath = Path.Combine(folder, "app.db");
        var nuvexaPath = Path.Combine(folder, "app.nvx");
        try
        {
            await using (var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath)))
            {
                await TestStores.SeedUsersAsync(sqlite,
                    new Person { Name = "Ada", Age = 36, City = "London" });
            }

            var options = TestStores.Options(StoreBackend.Nuvexa, nuvexaPath);
            options.AutoMigrate = true;
            options.Map<Person>("users");

            await using var nuvexa = await LocalStore.OpenAsync(options);
            Assert.Equal("Ada", Assert.Single(await nuvexa.GetCollection<Person>("users").FindAsync()).Name);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task AutoMigrate_two_siblings_requires_migrate_from()
    {
        var folder = TestStores.NewFolder();
        try
        {
            await using (var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, Path.Combine(folder, "app.db"))))
            {
                await TestStores.SeedUsersAsync(sqlite);
            }

            await using (var lite = LocalStore.Create(TestStores.Options(StoreBackend.LiteDb, Path.Combine(folder, "app.litedb"))))
            {
                await TestStores.SeedUsersAsync(lite);
            }

            var options = TestStores.Options(StoreBackend.Nuvexa, Path.Combine(folder, "app.nvx"));
            options.AutoMigrate = true;
            options.Map<Person>("users");

            var ex = await Assert.ThrowsAsync<LocalStoreException>(() => LocalStore.OpenAsync(options));
            Assert.Contains("MigrateFrom", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task AutoMigrate_can_delete_source_file()
    {
        var folder = TestStores.NewFolder();
        var sqlitePath = Path.Combine(folder, "app.db");
        var nuvexaPath = Path.Combine(folder, "app.nvx");
        try
        {
            await using (var sqlite = LocalStore.Create(TestStores.Options(StoreBackend.Sqlite, sqlitePath)))
            {
                await TestStores.SeedUsersAsync(sqlite,
                    new Person { Name = "Ada", Age = 36, City = "London" });
            }

            var options = TestStores.Options(StoreBackend.Nuvexa, nuvexaPath);
            options.AutoMigrate = true;
            options.MigrateFrom = StoreBackend.Sqlite;
            options.MigrateFromPath = sqlitePath;
            options.DeleteSourceAfterMigrate = true;
            options.Map<Person>("users");

            await using var nuvexa = await LocalStore.OpenAsync(options);
            Assert.Equal("Ada", Assert.Single(await nuvexa.GetCollection<Person>("users").FindAsync()).Name);
            Assert.False(File.Exists(sqlitePath));
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task MigrateAsync_requires_mapped_collections()
    {
        var folder = TestStores.NewFolder();
        try
        {
            var ex = await Assert.ThrowsAsync<LocalStoreException>(() =>
                LocalStore.MigrateAsync(
                    TestStores.Options(StoreBackend.Sqlite, Path.Combine(folder, "app.db")),
                    TestStores.Options(StoreBackend.Nuvexa, Path.Combine(folder, "app.nvx"))));
            Assert.Contains("Map at least one collection", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestStores.DeleteFolder(folder);
        }
    }
}
