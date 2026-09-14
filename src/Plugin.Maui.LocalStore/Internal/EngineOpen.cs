namespace Plugin.Maui.LocalStore;

static class EngineOpen
{
    public static ILocalStore DuckDb(LocalStoreOptions options) =>
        TryNative(options, StoreBackend.DuckDb, "DuckDB", () =>
        {
            var store = new DuckDbLocalStore(options);
            try
            {
                store.EnsureOpen();
                return store;
            }
            catch
            {
                store.DisposeAsync().AsTask().GetAwaiter().GetResult();
                throw;
            }
        });

    public static ILocalStore Firebird(LocalStoreOptions options)
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
        {
            return FileFallback(options, StoreBackend.Firebird, "Firebird");
        }

        return TryNative(options, StoreBackend.Firebird, "Firebird", () => new FirebirdLocalStore(options));
    }

    static ILocalStore TryNative(LocalStoreOptions options, StoreBackend backend, string engine, Func<ILocalStore> open)
    {
        try
        {
            return open();
        }
        catch (Exception ex) when (MissingNative.Matches(ex))
        {
            return FileFallback(options, backend, engine);
        }
    }

    public static ILocalStore FileFallback(LocalStoreOptions options, StoreBackend backend, string engine) =>
        new FileJsonLocalStore(backend, StorePaths.PrepareJsonFallback(options, engine));
}
