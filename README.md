# Plugin.Maui.LocalStore

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.LocalStore.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.LocalStore)

A Room-style app-document facade for **.NET MAUI** on **Android**, **iOS**, **Mac Catalyst**, and **Windows**. One collection API (`IStoreCollection<T>`). The host picks **SQLite** or **NuvexaDB**.

Switching backends does **not** migrate data. You get two files (`app.db` vs `app.nvx`).

This is not JobQueue or RetryQueue (durable jobs). It is not OfflineSync (sync + conflicts). It is not androidx.room and not a replacement for using `Nuventra.NuvexaDB` directly.

```
host
  ↓
ILocalStore / IStoreCollection<T>
  ↓                ↓
SQLite           NuvexaDB
app.db           app.nvx
```

## Install

```bash
dotnet add package Plugin.Maui.LocalStore
```

Target frameworks: `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0` (Windows TFM when packed on Windows).

## Quick start

```csharp
builder.UseMauiApp<App>()
    .UseMauiLocalStore(o =>
    {
        o.Backend = StoreBackend.Nuvexa; // or StoreBackend.Sqlite
        o.Path = Path.Combine(FileSystem.AppDataDirectory,
            o.Backend == StoreBackend.Nuvexa ? "app.nvx" : "app.db");
        o.EncryptionKey = key; // Nuvexa only; ignored on SQLite in 1.0
    });

var users = LocalStore.Current.GetCollection<Person>("users");
await users.InsertAsync(new Person { Name = "Ada", Age = 36, City = "London" });
var adults = await users.FindAsync(
    StoreFilter.Gte("Age", 21),
    new StoreQuery { SortBy = "Name", Limit = 20 });
```

POCOs need a public `string Id` (nullable is fine). Filters use **top-level property names**. Nested `address.city` is Nuvexa-only; keep a `City` property on the POCO for the shared API.

Nuvexa-only NQL:

```csharp
if (LocalStore.Current is INuvexaLocalStore nuvexa)
{
    await nuvexa.ExecuteNqlAsync("db.users.find({ age: { $gte: 21 } })");
}
```

## What 1.0 does not do

- Automatic `.db` ↔ `.nvx` migration
- Encrypted SQLite (SQLCipher)
- Source-generated DAOs or raw SQL on the shared interface
- Sibling `PackageReference` to OfflineSync / JobQueue / FileVault

## Sample

`samples/Plugin.Maui.LocalStore.Sample` — picker for SQLite vs Nuvexa, same Person tour.

## License

MIT
