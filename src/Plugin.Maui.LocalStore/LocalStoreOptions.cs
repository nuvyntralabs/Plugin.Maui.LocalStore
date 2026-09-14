namespace Plugin.Maui.LocalStore;

/// <summary>Registration options for <see cref="MauiAppBuilderExtensions.UseMauiLocalStore"/>.</summary>
public sealed class LocalStoreOptions
{
    /// <summary>SQLite (<c>.db</c>) or NuvexaDB (<c>.nvx</c>). Default <see cref="StoreBackend.Nuvexa"/>.</summary>
    public StoreBackend Backend { get; set; } = StoreBackend.Nuvexa;

    /// <summary>File path. When empty, <c>app.nvx</c> or <c>app.db</c> under local app data.</summary>
    public string? Path { get; set; }

    /// <summary>Nuvexa encryption key. Ignored for SQLite in 1.0.</summary>
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
        var file = Backend == StoreBackend.Nuvexa ? "app.nvx" : "app.db";
        return System.IO.Path.Combine(folder, "Plugin.Maui.LocalStore", file);
    }
}
