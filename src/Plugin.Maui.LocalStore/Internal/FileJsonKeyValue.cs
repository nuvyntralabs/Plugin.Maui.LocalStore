using System.Text;

namespace Plugin.Maui.LocalStore;

/// <summary>Managed key-value used when a native engine is missing (DuckDB, Firebird, LMDB on Catalyst, RocksDB, LevelDB).</summary>
sealed class FileJsonKeyValue : IJsonKeyValue
{
    readonly string _root;
    readonly object _gate = new();

    public FileJsonKeyValue(string directory)
    {
        _root = directory;
        Directory.CreateDirectory(_root);
    }

    public string? Get(string key)
    {
        lock (_gate)
        {
            var path = FilePath(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }

    public void Put(string key, string json)
    {
        lock (_gate)
        {
            File.WriteAllText(FilePath(key), json);
        }
    }

    public bool Delete(string key)
    {
        lock (_gate)
        {
            var path = FilePath(key);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }

    public IEnumerable<KeyValuePair<string, string>> Scan(string prefix)
    {
        lock (_gate)
        {
            if (!Directory.Exists(_root))
            {
                return [];
            }

            return Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path =>
                {
                    var key = Decode(Path.GetFileNameWithoutExtension(path));
                    return new KeyValuePair<string, string>(key, File.ReadAllText(path));
                })
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();
        }
    }

    public void Dispose()
    {
    }

    string FilePath(string key) => Path.Combine(_root, Encode(key) + ".json");

    static string Encode(string key) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(key));

    static string Decode(string hex) =>
        Encoding.UTF8.GetString(Convert.FromHexString(hex));
}
