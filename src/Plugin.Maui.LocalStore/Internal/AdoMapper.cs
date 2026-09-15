using System.Data;
using System.Data.Common;

namespace Plugin.Maui.LocalStore;

static class AdoMapper
{
    public static int Execute(DbConnection connection, AdoProfile dialect, string sql, object?[] args)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Bind(command, dialect, args);
        return command.ExecuteNonQuery();
    }

    public static IReadOnlyList<T> Query<T>(DbConnection connection, AdoProfile dialect, string sql, object?[] args)
        where T : class, new()
    {
        var columns = PocoColumns.Of<T>();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Bind(command, dialect, args);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(Map<T>(reader, columns));
        }

        return rows;
    }

    static void Bind(DbCommand command, AdoProfile dialect, object?[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            dialect.Bind(command, i, args[i]);
        }
    }

    static T Map<T>(IDataRecord row, System.Reflection.PropertyInfo[] columns) where T : class, new()
    {
        var item = new T();
        for (var i = 0; i < row.FieldCount; i++)
        {
            var name = row.GetName(i);
            var property = columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (property is null || row.IsDBNull(i))
            {
                continue;
            }

            property.SetValue(item, PocoColumns.ConvertTo(row.GetValue(i), property.PropertyType));
        }

        return item;
    }
}
