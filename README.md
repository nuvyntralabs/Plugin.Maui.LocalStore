# Plugin.Maui.LocalStore

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.LocalStore.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.LocalStore)

- NuGet: https://www.nuget.org/packages/Plugin.Maui.LocalStore
- GitHub: https://github.com/nuvyntralabs/Plugin.Maui.LocalStore
- Docs: https://nuvyntralabs.github.io/packages/plugin-maui-local-store/
- Catalog: [MauiEssentials](https://github.com/nuvyntralabs/MauiEssentials) (Niladri Padhy / Nuvyntra Labs)

An **abstract database layer** for **.NET MAUI** on **Android**, **iOS**, **Mac Catalyst**, and **Windows**. The host picks an engine (`StoreBackend`). Application code always uses the same methods on `ILocalStore` / `IStoreCollection<T>`.

You do not change insert / find / replace / delete / select when you add or switch a backend. Each engine has its own file. Switching does **not** migrate data.

```
host always calls
  InsertAsync / InsertManyAsync / FindByIdAsync / ReplaceAsync / DeleteByIdAsync / FindAsync
                    ↓
              IStoreCollection<T>
     ┌────────────┼────────────┐
  SQLite      NuvexaDB     Realm / LiteDB / DuckDB
  SQLCipher   Firebird     LMDB / RocksDB / LevelDB
```

1.0 **opens** every engine in the table below. Host CRUD stays the same. See [Platforms](#platforms) for which databases actually run on Android, iOS, Windows, and Mac Catalyst.

This is not JobQueue or RetryQueue (durable jobs). It is not OfflineSync (sync + conflicts). It is not androidx.room.

Package: [https://www.nuget.org/packages/Plugin.Maui.LocalStore](https://www.nuget.org/packages/Plugin.Maui.LocalStore)

## Local / Embedded databases

| Database | Type | Default path | MAUI | Offline | Relationships | Best for | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [SQLite](#sqlite) | Relational | `app.db` | ⭐⭐⭐⭐⭐ | ✅ | ✅ | General-purpose local DB | Shipped |
| [NuvexaDB](#nuvexadb) | Document NoSQL | `app.nvx` | ⭐⭐⭐⭐⭐ | ✅ | Limited | Embedded `.nvx` documents | Shipped |
| [Realm](#realm) | Object DB | `app.realm` | ⭐⭐⭐⭐ | ✅ | ✅ | Mobile / offline-first | Shipped |
| [LiteDB](#litedb) | Document NoSQL | `app.litedb` | ⭐⭐⭐⭐ | ✅ | Limited | Embedded NoSQL | Shipped |
| [DuckDB](#duckdb) | Analytical SQL | `app.duckdb` | ⭐⭐⭐ | ✅ | ✅ | Analytics / OLAP | Shipped (JSON fallback on mobile) |
| [SQLCipher](#sqlcipher) | Encrypted SQLite | `app.db` | ⭐⭐⭐⭐ | ✅ | ✅ | Secure local DB | Shipped |
| [Firebird Embedded](#firebird-embedded) | Relational | `app.fdb` | ⭐⭐⭐ | ✅ | ✅ | More advanced relational DB | Shipped (JSON fallback without `fbembed`) |
| [LMDB](#lmdb) | Key-value | `app.lmdb/` | ⭐⭐⭐ | ✅ | Limited | Very fast key-value storage | Shipped |
| [RocksDB](#rocksdb) | Key-value | `app.rocksdb/` | ⭐⭐⭐ | ✅ | No | High-performance storage | Shipped (JSON fallback on mobile) |
| [LevelDB](#leveldb) | Key-value | `app.leveldb/` | ⭐⭐⭐ | ✅ | No | Simple KV storage | Shipped (JSON fallback when native is missing) |

NuvexaDB is [Nuventra.NuvexaDB](https://www.nuget.org/packages/Nuventra.NuvexaDB) (Nuvyntra Labs). Every row above is reached through the **same** `IStoreCollection<T>` methods.

## Platforms

LocalStore targets **Android**, **iOS**, **Windows**, and **Mac Catalyst**. You still set `StoreBackend` the same way on every OS.

**Yes** = that database actually runs. **JSON fallback** = the NuGet has no native library for that OS, so LocalStore does not use the real engine.

| Database | Android | iOS | Windows | Mac Catalyst |
| --- | --- | --- | --- | --- |
| SQLite | Yes | Yes | Yes | Yes |
| SQLCipher | Yes | Yes | Yes | Yes |
| NuvexaDB | Yes | Yes | Yes | Yes |
| LiteDB | Yes | Yes | Yes | Yes |
| Realm | Yes | Yes | Yes | Yes |
| LMDB | Yes | Yes | Yes | JSON fallback |
| DuckDB | JSON fallback | JSON fallback | Yes (`win-x64`, `win-arm64`) | JSON fallback |
| Firebird | JSON fallback | JSON fallback | Yes (Embedded NuGet) | JSON fallback |
| RocksDB | JSON fallback | JSON fallback | Yes (`win-x64` only) | JSON fallback |
| LevelDB | JSON fallback | JSON fallback | JSON fallback | JSON fallback |

### If you select a database on an unsupported platform

Selection does not change. Keep `o.Backend = StoreBackend.DuckDb` (or Firebird, RocksDB, LevelDB, or LMDB on Catalyst). `LocalStore.Open` / `UseMauiLocalStore` **does not throw**.

On that OS, LocalStore stores each row as a JSON file (`app.duckdb.kv/`, `app.fdb.kv/`, or files under `app.lmdb/`, `app.rocksdb/`, `app.leveldb/`). `InsertAsync`, `FindByIdAsync`, `ReplaceAsync`, `DeleteByIdAsync`, and `FindAsync` still work. Filters run in memory. `EnsureIndexAsync` does nothing.

This is **not** DuckDB / Firebird / RocksDB / LevelDB / LMDB. Switching later to a native file on a supported OS does **not** migrate those JSON rows. Use SQLite, SQLCipher, NuvexaDB, LiteDB, or Realm when you need the real engine on every MAUI platform.

## Common methods

The host never writes SQL, NQL, or engine APIs on the shared path. Every backend must implement these operations:

| Operation | Method |
| --- | --- |
| Create | `InsertAsync` / `InsertManyAsync` |
| Read | `FindByIdAsync` |
| Update | `ReplaceAsync` |
| Delete | `DeleteByIdAsync` |
| Select | `FindAsync(StoreFilter, StoreQuery)` |
| Index | `EnsureIndexAsync` |
| Close | `DisposeAsync` |

```csharp
ILocalStore store = LocalStore.Current;
var users = store.GetCollection<Person>("users");
```

POCOs need a public `string Id` (nullable is fine) and a public parameterless constructor. Filters use **top-level property names** (`Age`, `City`). Get-only or `[JsonIgnore]` members are not stored (the sample `Person.Summary` is ignored).

```csharp
public sealed class Person
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Status { get; set; } = "active";
    public string? City { get; set; }
}
```

Scalar types: `string`, `int`, `long`, `double`, `float`, `bool`, `DateTime`.

```bash
dotnet add package Plugin.Maui.LocalStore
```

Host registration is `UseMauiLocalStore`. Non-MAUI hosts can call `services.AddMauiLocalStore(...)` or `LocalStore.Open(...)`.

**`LocalStoreOptions.Backend` defaults to `StoreBackend.Nuvexa`.** Set it explicitly when you want SQLite or another engine. When `Path` is empty, the file is `LocalApplicationData/Plugin.Maui.LocalStore/` plus the default path in the engine table above.

| Option | Default | Used by |
| --- | --- | --- |
| `Backend` | `Nuvexa` | All |
| `Path` | see `ResolvePath` | All (file or directory, per engine) |
| `CreateIfMissing` | `true` | All. `false` throws `LocalStoreException` if the file is missing |
| `EncryptionKey` | none | Nuvexa (required to open an encrypted `.nvx`), SQLCipher (required), LiteDB password, Realm, Firebird SYSDBA password. Ignored by SQLite, DuckDB, LMDB, RocksDB, LevelDB |
| `CacheSizeMb` | `16` | Nuvexa only |

Library and sample share the OS TFMs: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, plus `net10.0-windows10.0.19041.0` when built on Windows. The library also packs `net10.0` for tests and shared hosts.

The Create / Read / Update / Delete / Select samples in each engine section below are the **same methods**. Only `StoreBackend` and the file path change.

### Engine NuGet references (library)

| Backend | Package |
| --- | --- |
| SQLite | `sqlite-net-base`, `SQLitePCLRaw.bundle_e_sqlite3` |
| SQLCipher | same mapping + `SQLitePCLRaw.bundle_e_sqlcipher` (do not also reference `sqlite-net-sqlcipher`) |
| NuvexaDB | `Nuventra.NuvexaDB` 1.0.5 |
| LiteDB | `LiteDB` |
| Realm | `Realm` |
| DuckDB | `DuckDB.NET.Data.Full` (desktop natives) |
| Firebird | `FirebirdSql.Data.FirebirdClient`, `FirebirdDb.Embedded.V5.NativeAssets.Windows.All`, `FirebirdDb.Embedded.V5.NativeAssets.Linux.All` |
| LMDB | `LightningDB` |
| RocksDB | `RocksDB` (desktop natives) |
| LevelDB | `LevelDB.Standard` with `ExcludeAssets=native;build;buildTransitive` (Android cannot load those native assets) |

---

## SQLite

Relational. File: `app.db`. One table per collection. `EncryptionKey` is ignored — use [SQLCipher](#sqlcipher) when you need an encrypted SQLite file.

### Register

```csharp
using Plugin.Maui.LocalStore;

builder
    .UseMauiApp<App>()
    .UseMauiLocalStore(o =>
    {
        o.Backend = StoreBackend.Sqlite;
        o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.db");
        o.CreateIfMissing = true;
    });

var store = LocalStore.Current; // Backend == StoreBackend.Sqlite
var users = store.GetCollection<Person>("users");
```

Or without MAUI:

```csharp
await using var store = LocalStore.Open(new LocalStoreOptions
{
    Backend = StoreBackend.Sqlite,
    Path = path
});
```

`GetCollection` creates the table on first write.

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada",
    Age = 36,
    Status = "active",
    City = "London"
});
// generated when Person.Id is null; written back onto the POCO

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
if (ada is null)
{
    return;
}
```

### Update

`ReplaceAsync` requires a non-empty `Id`. It updates the row; it does not insert. Missing id throws `LocalStoreException`.

```csharp
ada.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
// false when the id is not present
```

### Select

`FindAsync` with no filter returns every row. `StoreQuery.Limit` of `0` means no limit. SQLite runs this as SQL on the table columns.

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));
```

| Filter | Meaning |
| --- | --- |
| `StoreFilter.Eq("City", "London")` | equal |
| `StoreFilter.Ne("Status", "retired")` | not equal |
| `StoreFilter.Gte("Age", 21)` | greater than or equal |
| `StoreFilter.Lt("Age", 30)` | less than |
| `StoreFilter.And(...)` | all children |
| `StoreFilter.Or(...)` | any child |

```csharp
await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

### Close and delete the file

```csharp
await store.DisposeAsync();
File.Delete(path);
```

---

## NuvexaDB

Document NoSQL. File: `app.nvx`. One collection per name. Set `EncryptionKey` for AES-256-GCM. Opening an encrypted file without the key throws `NuvexaEncryptionException` (fail-closed). Store the key in `SecureStorage` — not in source.

Engine: [Nuventra.NuvexaDB](https://www.nuget.org/packages/Nuventra.NuvexaDB) — [GitHub](https://github.com/nuvyntralabs/NuvexaDB).

### Register

```csharp
using Plugin.Maui.LocalStore;

builder
    .UseMauiApp<App>()
    .UseMauiLocalStore(o =>
    {
        o.Backend = StoreBackend.Nuvexa;
        o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.nvx");
        o.EncryptionKey = key;
        o.CreateIfMissing = true;
        o.CacheSizeMb = 16;
    });

var store = LocalStore.Current; // Backend == StoreBackend.Nuvexa
var users = store.GetCollection<Person>("users");
```

Or without MAUI:

```csharp
await using var store = LocalStore.Open(new LocalStoreOptions
{
    Backend = StoreBackend.Nuvexa,
    Path = path,
    EncryptionKey = key
});
```

`GetCollection` creates the collection on first write. Same `Person` type and same method names as every other engine.

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada",
    Age = 36,
    Status = "active",
    City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
if (ada is null)
{
    return;
}
```

### Update

```csharp
ada.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

Same `StoreFilter` / `StoreQuery` as SQLite. Nuvexa maps POCO names to NQL paths (`Age` → `age`, `Id` → `_id`).

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));
```

```csharp
await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

### Close and delete the file

```csharp
await store.DisposeAsync();
File.Delete(path);
var wal = path + "-wal";
if (File.Exists(wal))
{
    File.Delete(wal);
}
```

### Engine-only: NQL

Optional. Not part of the common layer. Other engines have no NQL.

```csharp
if (store is INuvexaLocalStore nuvexa)
{
    var rows = await nuvexa.ExecuteNqlAsync(
        """db.users.find({ age: { $gte: 21 } }).sort({ name: 1 }).limit(20)""");
}
```

NQL `update` / `delete` needs NuvexaDB **1.0.2+**.

---

## Realm

Object database. File: `app.realm`. Host POCOs stay plain classes. Rows are stored as JSON on a Realm object. Live Realm thread confinement is handled inside the adapter. Optional `EncryptionKey` encrypts the Realm file.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.Realm;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.realm");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## LiteDB

Embedded document NoSQL. File: `app.litedb`. Closest peer to Nuvexa on the common API: named collections, BSON-style documents, limited relationships. Optional `EncryptionKey` becomes the LiteDB password.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.LiteDb;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.litedb");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## DuckDB

Analytical SQL (OLAP). File: `app.duckdb`. Same CRUD; `FindAsync` maps to SQL. `DuckDB.NET.Data.Full` ships desktop natives only. On Android / iOS, LocalStore keeps the same `IStoreCollection<T>` API on a managed JSON folder (`app.duckdb.kv`).

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.DuckDb;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.duckdb");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## SQLCipher

Encrypted SQLite. File: `app.db`. Same relational mapping as [SQLite](#sqlite). `EncryptionKey` is required. Do not reuse an unencrypted SQLite file as a SQLCipher path.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.SqlCipher;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app-cipher.db");
    o.EncryptionKey = key;
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

Do not reuse an unencrypted SQLite `app.db` as a SQLCipher path. Dispose, then open a **different** file.

---

## Firebird Embedded

Advanced relational. File: `app.fdb`. Same table-per-collection mapping as SQLite. The host still calls `IStoreCollection<T>`. Desktop opening needs a Firebird 5 embedded tree (`FIREBIRD` pointing at the folder that contains `lib/`, `plugins/`, and `bin/isql`). Windows and Linux can use the `FirebirdDb.Embedded.V5.NativeAssets.*` packages. macOS has no NuGet native assets — use the official Firebird 5 package and set `FIREBIRD` / `FIREBIRD_CLIENT`. Android / iOS / Mac Catalyst have no `fbembed` package; LocalStore uses the managed JSON folder (`app.fdb.kv`) so CRUD still works. Optional `EncryptionKey` is the SYSDBA password on a real Firebird file (`masterkey` when omitted). Switching backends does not migrate data.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.Firebird;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.fdb");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## LMDB

Key-value. Path: `app.lmdb` (directory). Each collection is a key prefix. The POCO is stored as JSON keyed by `Id`. `FindAsync` filters in memory. `EnsureIndexAsync` is a no-op. Relationships are limited.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.Lmdb;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.lmdb");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## RocksDB

Key-value. Path: `app.rocksdb` (directory). High-write LSM storage. Same `Id` → JSON mapping as LMDB. `FindAsync` scans the prefix and filters in memory. `EnsureIndexAsync` is a no-op. The `RocksDB` NuGet ships desktop natives only. On Android / iOS, LocalStore uses the same managed per-key JSON files as LevelDB.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.RocksDb;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.rocksdb");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## LevelDB

Key-value. Path: `app.leveldb` (directory). Same collection + `Id` contract as RocksDB. `FindAsync` filters in memory. `EnsureIndexAsync` is a no-op. If the native LevelDB library is missing for the RID (`osx-arm64` is not in `LevelDB.Standard`; Android native assets are excluded because they are not PE), LocalStore uses a managed per-key JSON file store so the same API still works.

### Register

```csharp
builder.UseMauiLocalStore(o =>
{
    o.Backend = StoreBackend.LevelDb;
    o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.leveldb");
    o.CreateIfMissing = true;
});

var users = LocalStore.Current.GetCollection<Person>("users");
```

### Create

```csharp
var id = await users.InsertAsync(new Person
{
    Name = "Ada", Age = 36, Status = "active", City = "London"
});

var ids = await users.InsertManyAsync(
[
    new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
    new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
    new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
]);
```

### Read

```csharp
var ada = await users.FindByIdAsync(id);
```

### Update

```csharp
ada!.Name = "Ada Lovelace";
await users.ReplaceAsync(ada);
```

### Delete

```csharp
var removed = await users.DeleteByIdAsync(id);
```

### Select

```csharp
var all = await users.FindAsync();

var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });

var page2 = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Skip = 20, Limit = 20 });

var londonActive = await users.FindAsync(
    StoreFilter.And(
        StoreFilter.Eq("City", "London"),
        StoreFilter.Eq("Status", "active")),
    new StoreQuery { SortBy = "Age", SortDescending = true });

var youngOrNy = await users.FindAsync(
    StoreFilter.Or(
        StoreFilter.Lt("Age", 30),
        StoreFilter.Eq("City", "NewYork")));

var notRetired = await users.FindAsync(StoreFilter.Ne("Status", "retired"));

await users.EnsureIndexAsync("Age");
await users.EnsureIndexAsync("City", "Status");
```

---

## Add another database later

A new engine implements `ILocalStore` / `IStoreCollection<T>` and a `StoreBackend` value. Host code stays on the common methods. Dispose, then `Open` with the new backend and a **different path** — data does not copy.

```csharp
await LocalStore.Current.DisposeAsync();
LocalStore.Open(new LocalStoreOptions
{
    Backend = StoreBackend.Sqlite, // or Nuvexa, Realm, LiteDb, DuckDb, SqlCipher, Firebird, Lmdb, RocksDb, LevelDb
    Path = Path.Combine(FileSystem.AppDataDirectory, "app.db")
});
```

`CreateIfMissing = false` throws `LocalStoreException` when the file is missing.

## What 1.0 does not do

- Automatic migration between engines
- Source-generated DAOs or raw SQL / NQL on the shared interface
- Automatic promotion from the JSON fallback to a later native DuckDB / Firebird / RocksDB / LevelDB file
- Sibling `PackageReference` to OfflineSync, JobQueue, or FileVault

## Sample

`samples/Plugin.Maui.LocalStore.Sample` uses the same OS TFMs as the library: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, and `net10.0-windows10.0.19041.0` when the sample is built on Windows. `MauiProgram` does **not** call `UseMauiLocalStore` — the Backend picker calls `LocalStore.Open` so you can walk every engine. A host app that uses one engine should register it with `UseMauiLocalStore`.

Use insert / update / delete / find by Id, `FindAsync` presets (all, Age ≥ 21, London AND active, Age &lt; 30 OR NewYork, custom), Seed, Reset file, Contract tour, and **Test all engines**. DuckDB, Firebird, and RocksDB pass on device via the JSON fallback (they are not silent skips). SQLCipher uses `app-cipher.db` so it does not share the SQLite `app.db`.

## License

MIT
