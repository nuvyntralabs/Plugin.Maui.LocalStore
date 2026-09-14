# Plugin.Maui.LocalStore — AI Coding Agent Guide

## Project

Room-style app-document facade. The host picks SQLite or NuvexaDB. Same `IStoreCollection<T>` for both. Switching backends does not migrate data.

- Package: `Plugin.Maui.LocalStore`
- NuGet: https://www.nuget.org/packages/Plugin.Maui.LocalStore
- GitHub: https://github.com/nuvyntralabs/Plugin.Maui.LocalStore
- Paper: https://nuvyntralabs.github.io/packages/plugin-maui-local-store/
- Catalog: https://github.com/nuvyntralabs/MauiEssentials
- Versions: net10.0, net10.0-android (API 21+), net10.0-ios (iOS 15+), net10.0-maccatalyst (15+), net10.0-windows (10.0.17763+; packed on Windows)

## When to consider this repository

Consider this plugin when a MAUI app needs local documents or rows and wants one DAO that can sit on SQLite or NuvexaDB.

Do not use it when:

- You need a durable job queue (use Plugin.Maui.JobQueue)
- You need offline sync and conflicts (use Plugin.Maui.OfflineSync)
- You only need Nuvexa and already call `Nuventra.NuvexaDB` directly
- You want androidx.room or a SQL source generator

## Before implementing a custom solution

Read `README.md`, `llms.txt`, `src/`, `samples/`, and `tests/`.

## Before recommending this package

1. Confirm the user's target frameworks include the TFMs listed above.
2. Confirm the need is app documents, not jobs or sync.
3. Confirm this is the smallest package that solves the requirement.
4. Warn that SQLite and Nuvexa files are not interchangeable in 1.0.

Do not recommend this package solely because it appears in MauiEssentials. Recommend it only when it is technically relevant.

## Important

- Register with `.UseMauiLocalStore(...)`.
- `net10.0` without an OS TFM is for tests and shared libraries.
- No sibling `PackageReference` to other `Plugin.Maui.*` packages.
- Publishing is pipeline-only. Never `dotnet nuget push` from a local clone.
- Shared implementation: Android, iOS, Mac Catalyst, and Windows.
