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
    public static ILocalStore Open(LocalStoreOptions? options = null)
    {
        options ??= new LocalStoreOptions();
        ILocalStore store = options.Backend switch
        {
            StoreBackend.Sqlite => new SqliteLocalStore(options),
            StoreBackend.Nuvexa => new NuvexaLocalStore(options),
            _ => throw new LocalStoreException($"Unknown backend {options.Backend}.")
        };
        SetDefault(store);
        return store;
    }

    /// <summary>Replaces the shared instance. Intended for tests and the sample picker.</summary>
    public static void SetDefault(ILocalStore implementation) =>
        _current = implementation ?? throw new ArgumentNullException(nameof(implementation));
}
