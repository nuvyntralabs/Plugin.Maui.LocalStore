namespace Plugin.Maui.LocalStore;

/// <summary>Host misuse or store failure (missing id, unknown property, missing file).</summary>
public sealed class LocalStoreException : Exception
{
    public LocalStoreException(string message) : base(message)
    {
    }

    public LocalStoreException(string message, Exception inner) : base(message, inner)
    {
    }
}
