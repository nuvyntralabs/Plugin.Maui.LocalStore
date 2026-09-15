# Changelog

## 1.1.0

- Automatic engine migration: `AutoMigrate`, `MigrateFrom`, `Map<T>`, and `LocalStore.MigrateAsync`
- Raw SQL / NQL on `ILocalStore.QueryAsync<T>` / `ExecuteAsync` (`StoreQueryLanguage`). JSON fallback (mobile DuckDB / Firebird) reports `None`.
- Source-generated `[StoreDao]` implementations (`GetDao<T>`, `AddMauiLocalStoreDao<T>`)

## 1.0.1

- Ten engines behind `IStoreCollection<T>` (SQLite, NuvexaDB, Realm, LiteDB, DuckDB, SQLCipher, Firebird, LMDB, RocksDB, LevelDB)
- JSON file fallback when a native library is missing (mobile DuckDB / Firebird / RocksDB / LevelDB; LMDB on Mac Catalyst)
- README platform table: which database runs on Android, iOS, Windows, and Mac Catalyst
- Sample uses the same OS TFMs as the library; page-wide scroll for the engine report

## 1.0.0

- Initial release: `ILocalStore` / `IStoreCollection<T>` with host-selected SQLite or NuvexaDB
- Reserved `StoreBackend` values for Realm, LiteDB, DuckDB, SQLCipher, Firebird, LMDB, RocksDB, and LevelDB
- Implementations for those engines behind `IStoreCollection<T>` (Firebird uses native `fbembed` on desktop)
- Managed JSON file fallback when DuckDB, Firebird, RocksDB, or LevelDB natives are missing (Android / iOS)
- Library `PackageReference`s for LiteDB, Realm, DuckDB.NET.Data.Full, SQLitePCLRaw.bundle_e_sqlcipher, FirebirdSql.Data.FirebirdClient, LightningDB, RocksDB, and LevelDB.Standard
- `StoreFilter` (Eq, Ne, Gte, Lt, And, Or) and `StoreQuery` (sort / skip / limit)
- Nuvexa-only `INuvexaLocalStore.ExecuteNqlAsync`
- Contract tests on both backends; MAUI sample with a backend picker
