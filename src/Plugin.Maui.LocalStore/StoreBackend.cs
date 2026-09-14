namespace Plugin.Maui.LocalStore;

/// <summary>Host-selected persistence engine. Switching does not migrate data.</summary>
public enum StoreBackend
{
    /// <summary>Relational SQLite. Shipped in 1.0. File <c>app.db</c>.</summary>
    Sqlite,

    /// <summary>Embedded NuvexaDB document file. Shipped in 1.0. File <c>app.nvx</c>.</summary>
    Nuvexa,

    /// <summary>Realm object database. File <c>app.realm</c>.</summary>
    Realm,

    /// <summary>LiteDB embedded document store. File <c>app.litedb</c>.</summary>
    LiteDb,

    /// <summary>DuckDB analytical SQL. File <c>app.duckdb</c>.</summary>
    DuckDb,

    /// <summary>Encrypted SQLite (SQLCipher). File <c>app.db</c>. Requires <see cref="LocalStoreOptions.EncryptionKey"/>.</summary>
    SqlCipher,

    /// <summary>Firebird Embedded relational. File <c>app.fdb</c>.</summary>
    Firebird,

    /// <summary>LMDB key-value. Path <c>app.lmdb</c>.</summary>
    Lmdb,

    /// <summary>RocksDB key-value. Path <c>app.rocksdb</c>.</summary>
    RocksDb,

    /// <summary>LevelDB key-value. Path <c>app.leveldb</c>.</summary>
    LevelDb
}
