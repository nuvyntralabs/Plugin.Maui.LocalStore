using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;

namespace Plugin.Maui.LocalStore;

sealed class FirebirdLocalStore : AdoLocalStore
{
    public FirebirdLocalStore(LocalStoreOptions options)
        : base(options, StoreBackend.Firebird, "Firebird")
    {
        try
        {
            EnsureDatabase();
            using var connection = CreateConnection();
            connection.Open();
        }
        catch (Exception ex) when (ex is not LocalStoreException)
        {
            throw StorePaths.Wrap("Firebird", ex);
        }
    }

    protected override AdoProfile Profile { get; } = new()
    {
        Quote = name => "\"" + name + "\"",
        Parameter = i => "@p" + i,
        LimitOffset = (skip, limit) =>
        {
            if (limit <= 0 && skip <= 0)
            {
                return "";
            }

            var start = skip + 1;
            var end = limit > 0 ? skip + limit : int.MaxValue;
            return "ROWS " + start + " TO " + end;
        },
        ColumnType = SqlType,
        CreateTableIfNotExists = false,
        CreateIndexIfNotExists = false,
        Bind = (command, index, value) =>
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p" + index;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    };

    protected override void EnsureDatabase()
    {
        _ = FirebirdNative.FindClientLibrary();
        if (File.Exists(Path))
        {
            return;
        }

        var user = "SYSDBA";
        var password = string.IsNullOrWhiteSpace(Options.EncryptionKey) ? "masterkey" : Options.EncryptionKey;
        if (FirebirdNative.TryCreateDatabase(Path, user, password))
        {
            return;
        }

        // Firebird.NET on macOS can throw after isc_create_database has already written the file
        // and leaked the attachment. Prefer isql above; only use the provider on other OSes.
        if (OperatingSystem.IsMacOS())
        {
            throw new LocalStoreException(
                "Firebird failed to create the database. Point FIREBIRD at a Firebird 5 tree that includes bin/isql (official macOS package).");
        }

        try
        {
            FbConnection.CreateDatabase(ConnectionString(), pageSize: 8192, forcedWrites: true, overwrite: false);
        }
        catch (Exception) when (File.Exists(Path))
        {
        }
    }

    protected override DbConnection CreateConnection() => new FbConnection(ConnectionString());

    string ConnectionString()
    {
        var builder = new FbConnectionStringBuilder
        {
            Database = Path,
            ServerType = FbServerType.Embedded,
            UserID = "SYSDBA",
            Password = string.IsNullOrWhiteSpace(Options.EncryptionKey) ? "masterkey" : Options.EncryptionKey,
            Charset = "UTF8",
            Pooling = false
        };
        var client = FirebirdNative.FindClientLibrary();
        if (client is not null)
        {
            builder.ClientLibrary = client;
        }

        return builder.ToString();
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
            return "DOUBLE PRECISION";
        }

        if (type == typeof(DateTime))
        {
            return "TIMESTAMP";
        }

        // UTF8 VARCHAR length is in characters; 4000 overflows an 8K page.
        return "VARCHAR(255)";
    }
}
