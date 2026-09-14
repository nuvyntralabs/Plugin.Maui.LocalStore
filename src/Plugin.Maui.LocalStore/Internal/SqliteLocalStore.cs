using SQLite;

namespace Plugin.Maui.LocalStore;

sealed class SqliteLocalStore : ILocalStore
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _path;
    SQLiteAsyncConnection? _connection;
    bool _disposed;

    public SqliteLocalStore(LocalStoreOptions options)
    {
        _path = options.ResolvePath();
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_path) && !options.CreateIfMissing)
        {
            throw new LocalStoreException($"SQLite database '{_path}' was not found.");
        }
    }

    public StoreBackend Backend => StoreBackend.Sqlite;

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new SqliteStoreCollection<T>(this, StoreNames.Collection(name));

    internal async Task<SQLiteAsyncConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connection is not null)
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            SQLitePCL.Batteries_V2.Init();
            _connection = new SQLiteAsyncConnection(
                _path,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<T> LockedAsync<T>(Func<SQLiteAsyncConnection, Task<T>> work, CancellationToken cancellationToken)
    {
        var db = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await work(db).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection = null;
        }

        _gate.Dispose();
    }
}
