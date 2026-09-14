namespace Plugin.Maui.LocalStore;

/// <summary>Registration options for <see cref="MauiAppBuilderExtensions.UseMauiLocalStore"/>.</summary>
public sealed class LocalStoreOptions
{
    /// <summary>Persistence engine. Default <see cref="StoreBackend.Nuvexa"/>.</summary>
    public StoreBackend Backend { get; set; } = StoreBackend.Nuvexa;

    /// <summary>File or directory path. When empty, a backend-specific name under local app data.</summary>
    public string? Path { get; set; }

    /// <summary>Encryption key for Nuvexa, SQLCipher, LiteDB, Realm, and Firebird (SYSDBA password). Ignored by the other engines.</summary>
    public string? EncryptionKey { get; set; }

    /// <summary>Create the file when it is missing. Default true.</summary>
    public bool CreateIfMissing { get; set; } = true;

    /// <summary>Nuvexa page cache in megabytes. Default 16.</summary>
    public int CacheSizeMb { get; set; } = 16;

    internal string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(Path))
        {
            return Path;
        }

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var file = Backend switch
        {
            StoreBackend.Nuvexa => "app.nvx",
            StoreBackend.Realm => "app.realm",
            StoreBackend.LiteDb => "app.litedb",
            StoreBackend.DuckDb => "app.duckdb",
            StoreBackend.Firebird => "app.fdb",
            StoreBackend.Lmdb => "app.lmdb",
            StoreBackend.RocksDb => "app.rocksdb",
            StoreBackend.LevelDb => "app.leveldb",
            _ => "app.db"
        };
        return System.IO.Path.Combine(folder, "Plugin.Maui.LocalStore", file);
    }
}
