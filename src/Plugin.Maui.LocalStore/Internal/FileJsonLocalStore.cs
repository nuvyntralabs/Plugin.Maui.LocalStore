namespace Plugin.Maui.LocalStore;

sealed class FileJsonLocalStore : JsonKvLocalStore
{
    public FileJsonLocalStore(StoreBackend backend, string directory)
        : base(backend, new FileJsonKeyValue(directory))
    {
    }
}
