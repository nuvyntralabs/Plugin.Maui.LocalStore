namespace Plugin.Maui.LocalStore;

/// <summary>Registration options for <see cref="MauiAppBuilderExtensions.UseMauiLocalStore"/>.</summary>
public sealed class LocalStoreOptions
{
    readonly List<StoreCollectionMap> _collections = [];

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

    /// <summary>
    /// Copy <see cref="Collections"/> from another engine file into <see cref="Backend"/> on open
    /// when the destination collections are empty.
    /// </summary>
    public bool AutoMigrate { get; set; }

    /// <summary>Source engine. When omitted and <see cref="AutoMigrate"/> is true, a single sibling file is used.</summary>
    public StoreBackend? MigrateFrom { get; set; }

    /// <summary>Source file. Empty uses the destination folder plus the default name for <see cref="MigrateFrom"/>.</summary>
    public string? MigrateFromPath { get; set; }

    /// <summary>Source encryption key. Empty reuses <see cref="EncryptionKey"/>.</summary>
    public string? MigrateFromEncryptionKey { get; set; }

    /// <summary>Delete the source file after a successful copy. Default false.</summary>
    public bool DeleteSourceAfterMigrate { get; set; }

    /// <summary>Named collections for automatic migrate. Add with <see cref="Map{T}"/>.</summary>
    public IReadOnlyList<StoreCollectionMap> Collections => _collections;

    /// <summary>Registers a typed collection for migrate and documents the Room-style schema.</summary>
    public LocalStoreOptions Map<T>(string name) where T : class, new()
    {
        _collections.Add(StoreCollectionMap.For<T>(name));
        return this;
    }

    internal string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(Path))
        {
            return Path;
        }

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(folder, "Plugin.Maui.LocalStore", DefaultFileName(Backend));
    }

    internal static string DefaultFileName(StoreBackend backend) => backend switch
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

    internal static string DefaultExtension(StoreBackend backend) =>
        System.IO.Path.GetExtension(DefaultFileName(backend));
}
