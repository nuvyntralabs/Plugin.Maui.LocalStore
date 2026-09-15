namespace Plugin.Maui.LocalStore;

/// <summary>Outcome of an engine migrate on open or <c>LocalStore.MigrateAsync</c>.</summary>
public sealed class StoreMigrationResult
{
    public StoreBackend From { get; init; }

    public StoreBackend To { get; init; }

    public int Collections { get; init; }

    public int Documents { get; init; }

    public bool Skipped { get; init; }

    public string? Reason { get; init; }

    public static StoreMigrationResult Skip(StoreBackend from, StoreBackend to, string reason) =>
        new()
        {
            From = from,
            To = to,
            Skipped = true,
            Reason = reason
        };
}
