using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace Plugin.Maui.LocalStore;

sealed class AdoProfile
{
    public required Func<string, string> Quote { get; init; }
    public required Func<int, string> Parameter { get; init; }
    public required Func<int, int, string> LimitOffset { get; init; }
    public required Func<Type, string> ColumnType { get; init; }
    public required bool CreateTableIfNotExists { get; init; }
    public required bool CreateIndexIfNotExists { get; init; }
    public Action<DbCommand, int, object?> Bind { get; init; } = DefaultBind;

    static void DefaultBind(DbCommand command, int index, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = command.Parameters.Count == 0 && !command.CommandText.Contains('?')
            ? "@p" + index
            : ParameterName(command, index);
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    static string ParameterName(DbCommand command, int index) =>
        command.CommandText.Contains('?') ? "p" + index : "@p" + index;
}

abstract class AdoLocalStore : ILocalStore
{
    readonly SemaphoreSlim _gate = new(1, 1);
    DbConnection? _connection;
    bool _disposed;

    protected AdoLocalStore(LocalStoreOptions options, StoreBackend backend, string engine)
    {
        Backend = backend;
        Path = StorePaths.PrepareFile(options, engine);
        Options = options;
        Engine = engine;
    }

    public StoreBackend Backend { get; }
    protected string Path { get; }
    protected LocalStoreOptions Options { get; }
    protected string Engine { get; }
    protected abstract AdoProfile Profile { get; }

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new AdoStoreCollection<T>(this, StoreNames.Collection(name));

    protected abstract DbConnection CreateConnection();

    internal void EnsureOpen()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _connection ??= OpenConnection();
    }

    internal async Task<TResult> LockedAsync<TResult>(Func<DbConnection, TResult> work, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _connection ??= OpenConnection();
            return work(_connection);
        }
        finally
        {
            _gate.Release();
        }
    }

    DbConnection OpenConnection()
    {
        try
        {
            EnsureDatabase();
            var connection = CreateConnection();
            connection.Open();
            return connection;
        }
        catch (Exception ex) when (ex is not LocalStoreException)
        {
            throw StorePaths.Wrap(Engine, ex);
        }
    }

    protected virtual void EnsureDatabase()
    {
    }

    internal AdoProfile Dialect => Profile;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _gate.Dispose();
    }
}

sealed class AdoStoreCollection<T> : IStoreCollection<T> where T : class, new()
{
    static readonly PropertyInfo[] Columns = PocoColumns.Of<T>();

    readonly AdoLocalStore _store;
    readonly string _table;
    int _ready;

    public AdoStoreCollection(AdoLocalStore store, string name)
    {
        PocoColumns.RequireId<T>();
        _store = store;
        _table = name;
    }

    public async Task<string> InsertAsync(T item, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var id = EntityId.Get(item);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            EntityId.Set(item, id);
        }

        var profile = _store.Dialect;
        var sql = new StringBuilder("INSERT INTO ").Append(profile.Quote(_table)).Append(" (");
        sql.Append(string.Join(", ", Columns.Select(c => profile.Quote(c.Name))));
        sql.Append(") VALUES (");
        sql.Append(string.Join(", ", Columns.Select((_, i) => profile.Parameter(i))));
        sql.Append(')');
        var values = Columns.Select(c => c.GetValue(item)).ToArray();
        await _store.LockedAsync(db => Execute(db, sql.ToString(), values), cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<string>> InsertManyAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        foreach (var item in items)
        {
            ids.Add(await InsertAsync(item, cancellationToken).ConfigureAwait(false));
        }

        return ids;
    }

    public async Task<T?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var profile = _store.Dialect;
        var sql = $"SELECT * FROM {profile.Quote(_table)} WHERE {profile.Quote("Id")} = {profile.Parameter(0)}";
        var rows = await _store.LockedAsync(db => Query(db, sql, [id]), cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    public async Task ReplaceAsync(T item, CancellationToken cancellationToken = default)
    {
        var id = EntityId.Require(item);
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var profile = _store.Dialect;
        var sets = Columns.Where(c => !c.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)).ToArray();
        var sql = new StringBuilder("UPDATE ").Append(profile.Quote(_table)).Append(" SET ");
        sql.Append(string.Join(", ", sets.Select((c, i) => $"{profile.Quote(c.Name)} = {profile.Parameter(i)}")));
        sql.Append(" WHERE ").Append(profile.Quote("Id")).Append(" = ").Append(profile.Parameter(sets.Length));
        var args = sets.Select(c => c.GetValue(item)).Append(id).ToArray();
        var updated = await _store.LockedAsync(db => Execute(db, sql.ToString(), args), cancellationToken)
            .ConfigureAwait(false);
        if (updated == 0)
        {
            throw new LocalStoreException($"No row with Id '{id}'.");
        }
    }

    public async Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var profile = _store.Dialect;
        var sql = $"DELETE FROM {profile.Quote(_table)} WHERE {profile.Quote("Id")} = {profile.Parameter(0)}";
        var deleted = await _store.LockedAsync(db => Execute(db, sql.ToString(), [id]), cancellationToken)
            .ConfigureAwait(false);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<T>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var profile = _store.Dialect;
        var sql = new StringBuilder("SELECT * FROM ").Append(profile.Quote(_table));
        var args = new List<object?>();
        if (filter is not null)
        {
            sql.Append(" WHERE ").Append(ToSql(filter, args, profile));
        }

        if (!string.IsNullOrWhiteSpace(query?.SortBy))
        {
            var sort = StoreNames.Property(query.SortBy);
            sql.Append(" ORDER BY ").Append(profile.Quote(sort))
                .Append(query.SortDescending ? " DESC" : " ASC");
        }

        if (query is { Limit: > 0 } || query is { Skip: > 0 })
        {
            sql.Append(' ').Append(profile.LimitOffset(query?.Skip ?? 0, query?.Limit ?? 0));
        }

        return await _store.LockedAsync(db => Query(db, sql.ToString(), args.ToArray()), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task EnsureIndexAsync(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
        {
            return;
        }

        await EnsureTableAsync().ConfigureAwait(false);
        var profile = _store.Dialect;
        var cols = fields.Select(StoreNames.Property).ToArray();
        var index = $"idx_{_table}_{string.Join("_", cols)}";
        var list = string.Join(", ", cols.Select(c => profile.Quote(c)));
        var ifNot = profile.CreateIndexIfNotExists ? "IF NOT EXISTS " : "";
        var sql = $"CREATE INDEX {ifNot}{profile.Quote(index)} ON {profile.Quote(_table)} ({list})";
        try
        {
            await _store.LockedAsync(db => Execute(db, sql, []), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (!profile.CreateIndexIfNotExists && AlreadyExists(ex))
        {
            // index already exists
        }
    }

    async Task EnsureTableAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _ready, 1, 1) == 1)
        {
            return;
        }

        var profile = _store.Dialect;
        var defs = string.Join(", ", Columns.Select(c =>
            c.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                ? $"{profile.Quote("Id")} {profile.ColumnType(typeof(string))} PRIMARY KEY"
                : $"{profile.Quote(c.Name)} {profile.ColumnType(c.PropertyType)}"));
        var ifNot = profile.CreateTableIfNotExists ? "IF NOT EXISTS " : "";
        var sql = $"CREATE TABLE {ifNot}{profile.Quote(_table)} ({defs})";
        try
        {
            await _store.LockedAsync(db => Execute(db, sql, []), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!profile.CreateTableIfNotExists && AlreadyExists(ex))
        {
            // table already exists
        }

        Interlocked.Exchange(ref _ready, 1);
    }

    static string ToSql(StoreFilter filter, List<object?> args, AdoProfile profile)
    {
        if (filter.FilterKind is StoreFilter.Kind.And or StoreFilter.Kind.Or)
        {
            if (filter.Children.Length == 0)
            {
                return "1 = 1";
            }

            var op = filter.FilterKind == StoreFilter.Kind.And ? " AND " : " OR ";
            return "(" + string.Join(op, filter.Children.Select(child => ToSql(child, args, profile))) + ")";
        }

        var column = StoreNames.Property(filter.Property!);
        var token = profile.Parameter(args.Count);
        args.Add(filter.Value);
        var cmp = filter.FilterKind switch
        {
            StoreFilter.Kind.Eq => "=",
            StoreFilter.Kind.Ne => "<>",
            StoreFilter.Kind.Gte => ">=",
            StoreFilter.Kind.Lt => "<",
            _ => throw new LocalStoreException($"Unsupported filter {filter.FilterKind}.")
        };
        return $"{profile.Quote(column)} {cmp} {token}";
    }

    static bool AlreadyExists(Exception ex)
    {
        var text = ex.Message;
        return text.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || text.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    int Execute(DbConnection connection, string sql, object?[] args)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Bind(command, args);
        return command.ExecuteNonQuery();
    }

    IReadOnlyList<T> Query(DbConnection connection, string sql, object?[] args)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Bind(command, args);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(Map(reader));
        }

        return rows;
    }

    void Bind(DbCommand command, object?[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            _store.Dialect.Bind(command, i, args[i]);
        }
    }

    static T Map(IDataRecord row)
    {
        var item = new T();
        for (var i = 0; i < row.FieldCount; i++)
        {
            var name = row.GetName(i);
            var property = Columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (property is null || row.IsDBNull(i))
            {
                continue;
            }

            property.SetValue(item, PocoColumns.ConvertTo(row.GetValue(i), property.PropertyType));
        }

        return item;
    }
}
