namespace Plugin.Maui.LocalStore;

/// <summary>Nuvexa-only escape hatch. SQLite has no NQL.</summary>
public interface INuvexaLocalStore : ILocalStore
{
    Task<IReadOnlyList<string>> ExecuteNqlAsync(string nql, CancellationToken cancellationToken = default);
}
