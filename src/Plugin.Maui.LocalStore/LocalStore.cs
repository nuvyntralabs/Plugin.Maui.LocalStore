namespace Plugin.Maui.LocalStore;

/// <summary>Static accessor and factory. Register with <c>UseMauiLocalStore</c> or call <see cref="Open"/>.</summary>
public static class LocalStore
{
    static ILocalStore? _current;

    /// <summary>Shared store registered by <see cref="MauiAppBuilderExtensions.UseMauiLocalStore"/>.</summary>
    public static ILocalStore Current =>
        _current ?? throw new InvalidOperationException(
            "LocalStore has not been initialized. Call builder.UseMauiLocalStore() in MauiProgram.");

    public static bool IsInitialized => _current is not null;

    /// <summary>Opens or creates the file for <paramref name="options"/> and sets <see cref="Current"/>.</summary>
    public static ILocalStore Open(LocalStoreOptions? options = null) =>
        OpenAsync(options).GetAwaiter().GetResult();

    /// <summary>Opens the destination, copies mapped collections from another engine when configured, then sets <see cref="Current"/>.</summary>
    public static async Task<ILocalStore> OpenAsync(
        LocalStoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LocalStoreOptions();
        var store = Create(options);
        if (options.AutoMigrate || options.MigrateFrom is not null)
        {
            await StoreMigrator.ApplyAsync(options, store, cancellationToken).ConfigureAwait(false);
        }

        SetDefault(store);
        return store;
    }

    /// <summary>Copies mapped collections from <paramref name="source"/> into <paramref name="destination"/>.</summary>
    public static Task<StoreMigrationResult> MigrateAsync(
        ILocalStore source,
        ILocalStore destination,
        IEnumerable<StoreCollectionMap> collections,
        CancellationToken cancellationToken = default) =>
        StoreMigrator.CopyAsync(source, destination, collections, cancellationToken);

    /// <summary>Opens both files, copies <see cref="LocalStoreOptions.Collections"/>, and disposes the source.</summary>
    public static async Task<StoreMigrationResult> MigrateAsync(
        LocalStoreOptions source,
        LocalStoreOptions destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Collections.Count == 0)
        {
            throw new LocalStoreException("Map at least one collection with LocalStoreOptions.Map<T>(name).");
        }

        await using var from = Create(source);
        await using var to = Create(destination);
        return await StoreMigrator.CopyAsync(from, to, destination.Collections, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Opens an engine without changing <see cref="Current"/>.</summary>
    public static ILocalStore Create(LocalStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Backend switch
        {
            StoreBackend.Sqlite => new SqliteLocalStore(options, StoreBackend.Sqlite),
            StoreBackend.SqlCipher => new SqliteLocalStore(options, StoreBackend.SqlCipher),
            StoreBackend.Nuvexa => new NuvexaLocalStore(options),
            StoreBackend.LiteDb => new LiteDbLocalStore(options),
            StoreBackend.DuckDb => EngineOpen.DuckDb(options),
            StoreBackend.Firebird => EngineOpen.Firebird(options),
            StoreBackend.Realm => new RealmLocalStore(options),
            StoreBackend.Lmdb => new LmdbLocalStore(options),
            StoreBackend.RocksDb => new RocksDbLocalStore(options),
            StoreBackend.LevelDb => new LevelDbLocalStore(options),
            _ => throw new LocalStoreException($"Unknown backend {options.Backend}.")
        };
    }

    /// <summary>Replaces the shared instance. Intended for tests and the sample picker.</summary>
    public static void SetDefault(ILocalStore implementation) =>
        _current = implementation ?? throw new ArgumentNullException(nameof(implementation));
}
