namespace Plugin.Maui.LocalStore;

static class StoreQueryLanguages
{
    public static StoreQueryLanguage For(StoreBackend backend) => backend switch
    {
        StoreBackend.Sqlite or StoreBackend.SqlCipher or StoreBackend.DuckDb or StoreBackend.Firebird
            => StoreQueryLanguage.Sql,
        StoreBackend.Nuvexa => StoreQueryLanguage.Nql,
        _ => StoreQueryLanguage.None
    };

    public static Task<IReadOnlyList<T>> NotSupported<T>(StoreBackend backend) where T : class, new() =>
        throw new LocalStoreException(
            $"{backend} has no SQL or NQL. Use FindAsync, a generated [StoreDao], or a SQL/Nuvexa engine.");

    public static Task<int> NotSupportedExecute(StoreBackend backend) =>
        throw new LocalStoreException(
            $"{backend} has no SQL or NQL. Use IStoreCollection or a SQL/Nuvexa engine.");
}
