namespace Plugin.Maui.LocalStore;

/// <summary>Room-style database facade. The host picks <see cref="Backend"/>.</summary>
public interface ILocalStore : IAsyncDisposable
{
    StoreBackend Backend { get; }

    IStoreCollection<T> GetCollection<T>(string name) where T : class, new();
}
