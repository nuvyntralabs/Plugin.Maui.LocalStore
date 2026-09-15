using SQLite;

namespace Plugin.Maui.LocalStore;

sealed class SqliteLocalStore : ILocalStore
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _path;
    readonly string? _key;
    SQLiteAsyncConnection? _connection;
    bool _disposed;

    public SqliteLocalStore(LocalStoreOptions options)
        : this(options, options.Backend == StoreBackend.SqlCipher ? StoreBackend.SqlCipher : StoreBackend.Sqlite)
    {
    }

    public SqliteLocalStore(LocalStoreOptions options, StoreBackend backend)
    {
        if (backend is not (StoreBackend.Sqlite or StoreBackend.SqlCipher))
        {
            throw new LocalStoreException($"SqliteLocalStore cannot open {backend}.");
        }

        Backend = backend;
        _key = options.EncryptionKey;
        if (backend == StoreBackend.SqlCipher && string.IsNullOrWhiteSpace(_key))
        {
            throw new LocalStoreException("SQLCipher requires EncryptionKey.");
        }

        _path = StorePaths.PrepareFile(options, backend == StoreBackend.SqlCipher ? "SQLCipher" : "SQLite");
    }

    public StoreBackend Backend { get; }

    public StoreQueryLanguage QueryLanguage => StoreQueryLanguage.Sql;

    internal string FilePath => _path;

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new SqliteStoreCollection<T>(this, StoreNames.Collection(name));

    public Task<IReadOnlyList<T>> QueryAsync<T>(
        string command,
        object?[]? args = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return LockedAsync(async db =>
        {
            var rows = await db.QueryAsync<T>(command, args ?? []).ConfigureAwait(false);
            return (IReadOnlyList<T>)rows;
        }, cancellationToken);
    }

    public Task<int> ExecuteAsync(
        string command,
        object?[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return LockedAsync(db => db.ExecuteAsync(command, args ?? []), cancellationToken);
    }

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
            var flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex;
            _connection = Backend == StoreBackend.SqlCipher
                ? new SQLiteAsyncConnection(new SQLiteConnectionString(_path, flags, true, key: _key))
                : new SQLiteAsyncConnection(_path, flags);
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
