using System.Reflection;

namespace Plugin.Maui.LocalStore;

static class StoreMigrator
{
    static readonly StoreBackend[] DiscoveryOrder =
    [
        StoreBackend.Sqlite,
        StoreBackend.SqlCipher,
        StoreBackend.Nuvexa,
        StoreBackend.LiteDb,
        StoreBackend.Realm,
        StoreBackend.DuckDb,
        StoreBackend.Firebird,
        StoreBackend.Lmdb,
        StoreBackend.RocksDb,
        StoreBackend.LevelDb
    ];

    public static async Task ApplyAsync(
        LocalStoreOptions destination,
        ILocalStore dest,
        CancellationToken cancellationToken)
    {
        if (destination.Collections.Count == 0)
        {
            throw new LocalStoreException(
                "AutoMigrate requires LocalStoreOptions.Map<T>(name) for each collection to copy.");
        }

        if (await HasRowsAsync(dest, destination.Collections, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!TryResolveSource(destination, out var fromBackend, out var fromPath))
        {
            return;
        }

        var sourceOptions = new LocalStoreOptions
        {
            Backend = fromBackend,
            Path = fromPath,
            EncryptionKey = destination.MigrateFromEncryptionKey ?? destination.EncryptionKey,
            CreateIfMissing = false,
            CacheSizeMb = destination.CacheSizeMb
        };

        await using (var source = LocalStore.Create(sourceOptions))
        {
            var result = await CopyAsync(source, dest, destination.Collections, cancellationToken)
                .ConfigureAwait(false);
            if (result.Skipped || !destination.DeleteSourceAfterMigrate)
            {
                return;
            }
        }

        StorePaths.Delete(fromPath);
    }

    public static async Task<StoreMigrationResult> CopyAsync(
        ILocalStore source,
        ILocalStore destination,
        IEnumerable<StoreCollectionMap> collections,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        var maps = collections?.ToArray() ?? [];
        if (maps.Length == 0)
        {
            throw new LocalStoreException("Map at least one collection with LocalStoreOptions.Map<T>(name).");
        }

        if (source.Backend == destination.Backend &&
            string.Equals(PathOf(source), PathOf(destination), StringComparison.OrdinalIgnoreCase))
        {
            return StoreMigrationResult.Skip(source.Backend, destination.Backend, "Source and destination are the same store.");
        }

        if (await HasRowsAsync(destination, maps, cancellationToken).ConfigureAwait(false))
        {
            return StoreMigrationResult.Skip(source.Backend, destination.Backend, "Destination already has rows.");
        }

        var documents = 0;
        foreach (var map in maps)
        {
            documents += await CopyCollectionAsync(source, destination, map, cancellationToken).ConfigureAwait(false);
        }

        return new StoreMigrationResult
        {
            From = source.Backend,
            To = destination.Backend,
            Collections = maps.Length,
            Documents = documents
        };
    }

    static async Task<int> CopyCollectionAsync(
        ILocalStore source,
        ILocalStore destination,
        StoreCollectionMap map,
        CancellationToken cancellationToken)
    {
        StoreNames.Collection(map.Name);
        var method = typeof(StoreMigrator)
            .GetMethod(nameof(CopyTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(map.EntityType);
        var task = (Task<int>)method.Invoke(null, [source, destination, map.Name, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }

    static async Task<int> CopyTypedAsync<T>(
        ILocalStore source,
        ILocalStore destination,
        string name,
        CancellationToken cancellationToken) where T : class, new()
    {
        var rows = await source.GetCollection<T>(name).FindAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return 0;
        }

        await destination.GetCollection<T>(name).InsertManyAsync(rows, cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    static async Task<bool> HasRowsAsync(
        ILocalStore store,
        IEnumerable<StoreCollectionMap> collections,
        CancellationToken cancellationToken)
    {
        foreach (var map in collections)
        {
            var method = typeof(StoreMigrator)
                .GetMethod(nameof(CountAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(map.EntityType);
            var task = (Task<int>)method.Invoke(null, [store, map.Name, cancellationToken])!;
            if (await task.ConfigureAwait(false) > 0)
            {
                return true;
            }
        }

        return false;
    }

    static async Task<int> CountAsync<T>(ILocalStore store, string name, CancellationToken cancellationToken)
        where T : class, new()
    {
        var rows = await store.GetCollection<T>(name).FindAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return rows.Count;
    }

    static bool TryResolveSource(LocalStoreOptions destination, out StoreBackend backend, out string path)
    {
        backend = destination.Backend;
        path = "";
        var destPath = destination.ResolvePath();
        var folder = Path.GetDirectoryName(destPath) ?? "";
        var stem = Path.GetFileNameWithoutExtension(destPath);

        if (destination.MigrateFrom is { } explicitFrom)
        {
            backend = explicitFrom;
            path = string.IsNullOrWhiteSpace(destination.MigrateFromPath)
                ? Path.Combine(folder, stem + LocalStoreOptions.DefaultExtension(explicitFrom))
                : destination.MigrateFromPath;
            if (!StorePaths.Exists(explicitFrom, path))
            {
                var fallback = Path.Combine(folder, LocalStoreOptions.DefaultFileName(explicitFrom));
                if (StorePaths.Exists(explicitFrom, fallback))
                {
                    path = fallback;
                    return true;
                }

                return false;
            }

            return true;
        }

        var found = new List<(StoreBackend Backend, string Path)>();
        foreach (var candidate in DiscoveryOrder)
        {
            if (candidate == destination.Backend)
            {
                continue;
            }

            var sibling = Path.Combine(folder, stem + LocalStoreOptions.DefaultExtension(candidate));
            var named = Path.Combine(folder, LocalStoreOptions.DefaultFileName(candidate));
            string? match = null;
            if (StorePaths.Exists(candidate, sibling))
            {
                match = sibling;
            }
            else if (!string.Equals(sibling, named, StringComparison.OrdinalIgnoreCase) &&
                     StorePaths.Exists(candidate, named))
            {
                match = named;
            }

            if (match is not null &&
                !found.Any(item => string.Equals(item.Path, match, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add((candidate, match));
            }
        }

        if (found.Count == 0)
        {
            return false;
        }

        if (found.Count > 1)
        {
            throw new LocalStoreException(
                "AutoMigrate found more than one sibling engine file. Set MigrateFrom to pick one: " +
                string.Join(", ", found.Select(item => $"{item.Backend} ({item.Path})")));
        }

        backend = found[0].Backend;
        path = found[0].Path;
        return true;
    }

    static string? PathOf(ILocalStore store) => store switch
    {
        SqliteLocalStore sqlite => sqlite.FilePath,
        NuvexaLocalStore nuvexa => nuvexa.FilePath,
        _ => null
    };
}
