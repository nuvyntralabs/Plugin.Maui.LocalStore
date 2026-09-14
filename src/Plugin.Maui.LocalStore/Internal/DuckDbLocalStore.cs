using System.Data.Common;
using DuckDB.NET.Data;

namespace Plugin.Maui.LocalStore;

sealed class DuckDbLocalStore : AdoLocalStore
{
    public DuckDbLocalStore(LocalStoreOptions options)
        : base(options, StoreBackend.DuckDb, "DuckDB")
    {
    }

    protected override AdoProfile Profile { get; } = new()
    {
        Quote = name => "\"" + name + "\"",
        Parameter = _ => "?",
        LimitOffset = (skip, limit) =>
        {
            var sql = limit > 0 ? "LIMIT " + limit : "LIMIT -1";
            return skip > 0 ? sql + " OFFSET " + skip : sql;
        },
        ColumnType = SqlType,
        CreateTableIfNotExists = true,
        CreateIndexIfNotExists = true,
        Bind = (command, _, value) =>
        {
            var parameter = command.CreateParameter();
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    };

    protected override DbConnection CreateConnection() =>
        new DuckDBConnection($"Data Source={Path}");

    static string SqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(int) || type == typeof(long) || type == typeof(bool))
        {
            return "INTEGER";
        }

        if (type == typeof(double) || type == typeof(float))
        {
            return "DOUBLE";
        }

        if (type == typeof(DateTime))
        {
            return "TIMESTAMP";
        }

        return "VARCHAR";
    }
}
