namespace Plugin.Maui.LocalStore;

/// <summary>Native command dialect for <see cref="ILocalStore.QueryAsync{T}"/> / <see cref="ILocalStore.ExecuteAsync"/>.</summary>
public enum StoreQueryLanguage
{
    /// <summary>No SQL or NQL. Use <see cref="IStoreCollection{T}.FindAsync"/> or a generated DAO.</summary>
    None = 0,

    /// <summary>Native SQLite, SQLCipher, DuckDB, and Firebird. JSON fallback stores report <see cref="None"/>.</summary>
    Sql = 1,

    /// <summary>NuvexaDB.</summary>
    Nql = 2
}
