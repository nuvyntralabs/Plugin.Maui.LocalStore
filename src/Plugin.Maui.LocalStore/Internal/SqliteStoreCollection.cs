using System.Globalization;
using System.Reflection;
using System.Text;
using SQLite;

namespace Plugin.Maui.LocalStore;

sealed class SqliteStoreCollection<T> : IStoreCollection<T> where T : class, new()
{
    static readonly PropertyInfo[] Columns = typeof(T)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.CanRead && p.CanWrite && IsScalar(p.PropertyType))
        .ToArray();

    readonly SqliteLocalStore _store;
    readonly string _table;
    int _ready;

    public SqliteStoreCollection(SqliteLocalStore store, string name)
    {
        _store = store;
        _table = name;
        if (!Columns.Any(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)))
        {
            throw new LocalStoreException($"{typeof(T).Name} needs a public string Id property.");
        }
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

        await _store.LockedAsync(async db =>
        {
            var sql = new StringBuilder($"INSERT INTO \"{_table}\" (");
            sql.Append(string.Join(", ", Columns.Select(c => $"\"{c.Name}\"")));
            sql.Append(") VALUES (");
            sql.Append(string.Join(", ", Columns.Select(_ => "?")));
            sql.Append(')');
            await db.ExecuteAsync(sql.ToString(), Values(item)).ConfigureAwait(false);
            return 0;
        }, cancellationToken).ConfigureAwait(false);

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
        return await _store.LockedAsync(async db =>
        {
            var rows = await db.QueryAsync<T>($"SELECT * FROM \"{_table}\" WHERE \"Id\" = ?", id)
                .ConfigureAwait(false);
            return rows.FirstOrDefault();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceAsync(T item, CancellationToken cancellationToken = default)
    {
        var id = EntityId.Require(item);
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var updated = await _store.LockedAsync(async db =>
        {
            var sets = string.Join(", ", Columns.Where(c => !c.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                .Select(c => $"\"{c.Name}\" = ?"));
            var args = Columns.Where(c => !c.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.GetValue(item))
                .Append(id)
                .ToArray();
            return await db.ExecuteAsync(
                $"UPDATE \"{_table}\" SET {sets} WHERE \"Id\" = ?",
                args).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        if (updated == 0)
        {
            throw new LocalStoreException($"No row with Id '{id}'.");
        }
    }

    public async Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await _store.LockedAsync(
            db => db.ExecuteAsync($"DELETE FROM \"{_table}\" WHERE \"Id\" = ?", id),
            cancellationToken).ConfigureAwait(false);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<T>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken).ConfigureAwait(false);
        var sql = new StringBuilder($"SELECT * FROM \"{_table}\"");
        var args = new List<object?>();
        if (filter is not null)
        {
            sql.Append(" WHERE ");
            sql.Append(ToSql(filter, args));
        }

        if (!string.IsNullOrWhiteSpace(query?.SortBy))
        {
            var sort = StoreNames.Property(query.SortBy);
            sql.Append(" ORDER BY \"").Append(sort).Append(query.SortDescending ? "\" DESC" : "\" ASC");
        }

        if (query is { Limit: > 0 })
        {
            sql.Append(" LIMIT ").Append(query.Limit.ToString(CultureInfo.InvariantCulture));
        }

        if (query is { Skip: > 0 })
        {
            sql.Append(" OFFSET ").Append(query.Skip.ToString(CultureInfo.InvariantCulture));
        }

        return await _store.LockedAsync(
            async db => await db.QueryAsync<T>(sql.ToString(), args.ToArray()).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureIndexAsync(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
        {
            return;
        }

        await EnsureTableAsync().ConfigureAwait(false);
        var cols = fields.Select(StoreNames.Property).ToArray();
        var index = $"idx_{_table}_{string.Join("_", cols)}";
        var list = string.Join(", ", cols.Select(c => $"\"{c}\""));
        await _store.LockedAsync(
            db => db.ExecuteAsync($"CREATE INDEX IF NOT EXISTS \"{index}\" ON \"{_table}\" ({list})"),
            CancellationToken.None).ConfigureAwait(false);
    }

    async Task EnsureTableAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _ready, 1, 1) == 1)
        {
            return;
        }

        var defs = string.Join(", ", Columns.Select(c =>
            c.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                ? "\"Id\" TEXT PRIMARY KEY"
                : $"\"{c.Name}\" {SqlType(c.PropertyType)}"));
        await _store.LockedAsync(
            db => db.ExecuteAsync($"CREATE TABLE IF NOT EXISTS \"{_table}\" ({defs})"),
            cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _ready, 1);
    }

    static string ToSql(StoreFilter filter, List<object?> args)
    {
        if (filter.FilterKind is StoreFilter.Kind.And or StoreFilter.Kind.Or)
        {
            if (filter.Children.Length == 0)
            {
                return "1 = 1";
            }

            var op = filter.FilterKind == StoreFilter.Kind.And ? " AND " : " OR ";
            return "(" + string.Join(op, filter.Children.Select(child => ToSql(child, args))) + ")";
        }

        var column = StoreNames.Property(filter.Property!);
        args.Add(filter.Value);
        const string token = "?";
        var cmp = filter.FilterKind switch
        {
            StoreFilter.Kind.Eq => "=",
            StoreFilter.Kind.Ne => "!=",
            StoreFilter.Kind.Gte => ">=",
            StoreFilter.Kind.Lt => "<",
            _ => throw new LocalStoreException($"Unsupported filter {filter.FilterKind}.")
        };
        return $"\"{column}\" {cmp} {token}";
    }

    static object?[] Values(T item) => Columns.Select(c => c.GetValue(item)).ToArray();

    static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string)
               || type == typeof(int)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(float)
               || type == typeof(bool)
               || type == typeof(DateTime);
    }

    static string SqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(int) || type == typeof(long) || type == typeof(bool))
        {
            return "INTEGER";
        }

        if (type == typeof(double) || type == typeof(float))
        {
            return "REAL";
        }

        return "TEXT";
    }
}
