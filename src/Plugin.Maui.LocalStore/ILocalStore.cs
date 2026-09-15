namespace Plugin.Maui.LocalStore;

/// <summary>Room-style database facade. The host picks <see cref="Backend"/>.</summary>
public interface ILocalStore : IAsyncDisposable
{
    StoreBackend Backend { get; }

    /// <summary>
    /// SQL when the native SQLite / SQLCipher / DuckDB / Firebird engine is open.
    /// NQL for Nuvexa. <see cref="StoreQueryLanguage.None"/> for other engines and for the JSON file fallback.
    /// </summary>
    StoreQueryLanguage QueryLanguage => StoreQueryLanguages.For(Backend);

    IStoreCollection<T> GetCollection<T>(string name) where T : class, new();

    /// <summary>
    /// Runs a backend-native command and maps rows to <typeparamref name="T"/>.
    /// SQL for SQLite / SQLCipher / DuckDB / Firebird. NQL for Nuvexa.
    /// Other engines throw <see cref="LocalStoreException"/>.
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string command,
        object?[]? args = null,
        CancellationToken cancellationToken = default) where T : class, new() =>
        StoreQueryLanguages.NotSupported<T>(Backend);

    /// <summary>
    /// Runs a backend-native command that does not return mapped rows (INSERT/UPDATE/DELETE or NQL write).
    /// </summary>
    Task<int> ExecuteAsync(
        string command,
        object?[]? args = null,
        CancellationToken cancellationToken = default) =>
        StoreQueryLanguages.NotSupportedExecute(Backend);
}
