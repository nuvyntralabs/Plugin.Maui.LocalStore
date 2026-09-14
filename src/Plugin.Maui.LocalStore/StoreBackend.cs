namespace Plugin.Maui.LocalStore;

/// <summary>Host-selected persistence engine. Switching does not migrate data.</summary>
public enum StoreBackend
{
    Sqlite,
    Nuvexa
}
