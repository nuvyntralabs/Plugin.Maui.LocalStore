# Changelog

## 1.0.0

- Initial release: `ILocalStore` / `IStoreCollection<T>` with host-selected SQLite or NuvexaDB
- `StoreFilter` (Eq, Ne, Gte, Lt, And, Or) and `StoreQuery` (sort / skip / limit)
- Nuvexa-only `INuvexaLocalStore.ExecuteNqlAsync`
- Contract tests on both backends; MAUI sample with a backend picker
