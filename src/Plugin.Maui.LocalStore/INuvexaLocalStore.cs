namespace Plugin.Maui.LocalStore;

/// <summary>Nuvexa JSON-string escape hatch. Prefer <see cref="ILocalStore.QueryAsync{T}"/> for mapped NQL.</summary>
public interface INuvexaLocalStore : ILocalStore
{
    Task<IReadOnlyList<string>> ExecuteNqlAsync(string nql, CancellationToken cancellationToken = default);
}
