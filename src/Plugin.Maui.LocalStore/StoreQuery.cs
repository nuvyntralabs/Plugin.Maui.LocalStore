namespace Plugin.Maui.LocalStore;

/// <summary>Sort and page options for <see cref="IStoreCollection{T}.FindAsync"/>.</summary>
public sealed class StoreQuery
{
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int Skip { get; init; }
    public int Limit { get; init; }
}
