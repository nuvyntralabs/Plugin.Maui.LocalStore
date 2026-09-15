namespace Plugin.Maui.LocalStore;

/// <summary>Named collection plus CLR type. Used by engine migration and generated DAOs.</summary>
public sealed class StoreCollectionMap
{
    public required string Name { get; init; }

    public required Type EntityType { get; init; }

    public static StoreCollectionMap For<T>(string name) where T : class, new() =>
        new() { Name = name, EntityType = typeof(T) };
}
