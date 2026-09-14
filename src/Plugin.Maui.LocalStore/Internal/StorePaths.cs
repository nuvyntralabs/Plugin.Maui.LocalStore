namespace Plugin.Maui.LocalStore;

static class StorePaths
{
    public static string PrepareFile(LocalStoreOptions options, string engine)
    {
        var path = options.ResolvePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(path) && !options.CreateIfMissing)
        {
            throw new LocalStoreException($"{engine} database '{path}' was not found.");
        }

        return path;
    }

    public static string PrepareDirectory(LocalStoreOptions options, string engine)
    {
        var path = options.ResolvePath();
        if (!Directory.Exists(path) && !options.CreateIfMissing)
        {
            throw new LocalStoreException($"{engine} directory '{path}' was not found.");
        }

        Directory.CreateDirectory(path);
        return path;
    }

    public static string PrepareJsonFallback(LocalStoreOptions options, string engine)
    {
        var path = options.ResolvePath();
        var directory = IsDirectoryStore(path) ? path : path + ".kv";
        if (!Directory.Exists(directory) && !options.CreateIfMissing)
        {
            throw new LocalStoreException($"{engine} directory '{directory}' was not found.");
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    public static LocalStoreException Wrap(string engine, Exception exception) =>
        new($"{engine} failed to open: {exception.Message}", exception);

    static bool IsDirectoryStore(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Length == 0
               || ext.Equals(".rocksdb", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".leveldb", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".lmdb", StringComparison.OrdinalIgnoreCase);
    }
}
