using System.Text;

namespace Plugin.Maui.LocalStore.Sample;

public static class FeatureDemos
{
    public const string SqliteFile = "demo-migrate.db";
    public const string NuvexaFile = "demo-migrate.nvx";

    public static async Task<string> RunRawQueryAsync(ILocalStore store)
    {
        var log = new StringBuilder();
        log.AppendLine($"Backend: {store.Backend}");
        log.AppendLine($"QueryLanguage: {store.QueryLanguage}");

        if (store.QueryLanguage == StoreQueryLanguage.None)
        {
            log.AppendLine("This engine has no SQL or NQL. Use FindAsync or switch to SQLite / Nuvexa.");
            return log.ToString();
        }

        await EnsureRowsAsync(store).ConfigureAwait(false);
        var command = store.QueryLanguage == StoreQueryLanguage.Nql
            ? "db.users.find({ age: { $gte: 21 } }).sort({ name: 1 })"
            : "SELECT * FROM users WHERE Age >= 21 ORDER BY Name";
        log.AppendLine(command);
        foreach (var row in await store.QueryAsync<Person>(command).ConfigureAwait(false))
        {
            log.AppendLine($"{row.Name} {row.Age} {row.City}");
        }

        return log.ToString();
    }

    public static async Task<string> RunDaoAsync(ILocalStore store)
    {
        var log = new StringBuilder();
        log.AppendLine($"Backend: {store.Backend}");
        log.AppendLine($"QueryLanguage: {store.QueryLanguage}");

        await EnsureRowsAsync(store).ConfigureAwait(false);
        var dao = store.GetDao<IPersonDao>();
        var all = await dao.FindAsync(query: new StoreQuery { SortBy = "Name" }).ConfigureAwait(false);
        log.AppendLine($"IPersonDao.FindAsync → {all.Count} row(s)");

        if (store.QueryLanguage == StoreQueryLanguage.None)
        {
            log.AppendLine("FindAdultsAsync needs SQL or NQL. CRUD methods still work on every engine.");
            return log.ToString();
        }

        log.AppendLine("IPersonDao.FindAdultsAsync(21)");
        foreach (var row in await dao.FindAdultsAsync(21).ConfigureAwait(false))
        {
            log.AppendLine($"{row.Name} {row.Age} {row.City}");
        }

        return log.ToString();
    }

    public static async Task<(string Log, StoreBackend Destination)> RunMigrateAsync()
    {
        var log = new StringBuilder();
        var sqlitePath = Path.Combine(FileSystem.AppDataDirectory, SqliteFile);
        var nuvexaPath = Path.Combine(FileSystem.AppDataDirectory, NuvexaFile);
        Delete(nuvexaPath);

        await using (var sqlite = LocalStore.Create(new LocalStoreOptions
        {
            Backend = StoreBackend.Sqlite,
            Path = sqlitePath
        }))
        {
            var users = sqlite.GetCollection<Person>("users");
            var existing = await users.FindAsync().ConfigureAwait(false);
            if (existing.Count == 0)
            {
                await users.InsertManyAsync(
                [
                    new Person { Name = "Ada", Age = 36, Status = "active", City = "London" },
                    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" },
                    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" }
                ]).ConfigureAwait(false);
                log.AppendLine("Seeded SQLite demo-migrate.db with Ada, Alan, Cara.");
            }
            else
            {
                log.AppendLine($"SQLite already has {existing.Count} row(s).");
            }
        }

        var result = await LocalStore.MigrateAsync(
            new LocalStoreOptions { Backend = StoreBackend.Sqlite, Path = sqlitePath },
            new LocalStoreOptions
            {
                Backend = StoreBackend.Nuvexa,
                Path = nuvexaPath,
                EncryptionKey = "sample-key"
            }.Map<Person>("users")).ConfigureAwait(false);

        log.AppendLine(result.Skipped
            ? $"Skipped: {result.Reason}"
            : $"Copied {result.Documents} document(s) in {result.Collections} collection(s) from {result.From} to {result.To}.");
        log.AppendLine(nuvexaPath);
        return (log.ToString(), StoreBackend.Nuvexa);
    }

    static async Task EnsureRowsAsync(ILocalStore store)
    {
        var users = store.GetCollection<Person>("users");
        if ((await users.FindAsync().ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        await users.InsertManyAsync(
        [
            new Person { Name = "Ada", Age = 36, Status = "active", City = "London" },
            new Person { Name = "Alan", Age = 42, Status = "active", City = "London" },
            new Person { Name = "Scratch", Age = 19, Status = "active", City = "Paris" }
        ]).ConfigureAwait(false);
    }

    static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var wal = path + "-wal";
        if (File.Exists(wal))
        {
            File.Delete(wal);
        }
    }
}
