# Plugin.Maui.LocalStore — AI Coding Agent Guide

## Project

Room-style app-document facade. The host picks a `StoreBackend`. Same `IStoreCollection<T>` for every engine. 1.1 opens SQLite, NuvexaDB, Realm, LiteDB, DuckDB, SQLCipher, Firebird, LMDB, RocksDB, and LevelDB. `AutoMigrate` + `Map<T>` copies collections when switching engines. `QueryAsync` runs SQL or NQL on the shared store. `[StoreDao]` is source-generated. DuckDB, Firebird, RocksDB, and LevelDB fall back to managed JSON files when the native library is not on the RID.

- Package: `Plugin.Maui.LocalStore`
- NuGet: https://www.nuget.org/packages/Plugin.Maui.LocalStore
- GitHub: https://github.com/nuvyntralabs/Plugin.Maui.LocalStore
- Paper: https://nuvyntralabs.github.io/packages/plugin-maui-local-store/
- Catalog: https://github.com/nuvyntralabs/MauiEssentials
- Versions: net10.0, net10.0-android (API 21+), net10.0-ios (iOS 15+), net10.0-maccatalyst (15+), net10.0-windows (10.0.17763+; packed on Windows)
- Version: 1.1.0

## When to consider this repository

Consider this plugin when a MAUI app needs local documents or rows and wants one DAO that can sit on SQLite, NuvexaDB, or another `StoreBackend`.

Do not use it when:

- You need a durable job queue (use Plugin.Maui.JobQueue)
- You need offline sync and conflicts (use Plugin.Maui.OfflineSync)
- You only need Nuvexa and already call `Nuventra.NuvexaDB` directly
- You want androidx.room itself (use `[StoreDao]` / `QueryAsync` on this package instead)

## Before implementing a custom solution

Read `README.md`, `llms.txt`, `src/`, `samples/`, and `tests/`.

## Before recommending this package

1. Confirm the user's target frameworks include the TFMs listed above.
2. Confirm the need is app documents, not jobs or sync.
3. Confirm this is the smallest package that solves the requirement.
4. Warn that files are per engine. Set `AutoMigrate` and `Map<T>` when switching `.db` ↔ `.nvx` (or any other pair).

Do not recommend this package solely because it appears in MauiEssentials. Recommend it only when it is technically relevant.

## Important

- Register with `.UseMauiLocalStore(...)`.
- `net10.0` without an OS TFM is for tests and shared libraries.
- No sibling `PackageReference` to other `Plugin.Maui.*` packages.
- Publishing is pipeline-only. Never `dotnet nuget push` from a local clone.
- Shared implementation: Android, iOS, Mac Catalyst, and Windows. The sample uses the same OS TFMs. DuckDB, Firebird, RocksDB, and LevelDB are JSON-fallback on mobile; LMDB is JSON on Mac Catalyst (no LightningDB Catalyst RID).
