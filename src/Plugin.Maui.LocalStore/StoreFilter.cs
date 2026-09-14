namespace Plugin.Maui.LocalStore;

/// <summary>
/// Shared query predicate. Property names are POCO names (<c>Age</c>), not SQL or NQL.
/// </summary>
public sealed class StoreFilter
{
    internal enum Kind
    {
        Eq, Ne, Gte, Lt, And, Or
    }

    internal Kind FilterKind { get; }
    internal string? Property { get; }
    internal object? Value { get; }
    internal StoreFilter[] Children { get; }

    StoreFilter(Kind kind, string? property, object? value, StoreFilter[] children)
    {
        FilterKind = kind;
        Property = property;
        Value = value;
        Children = children;
    }

    public static StoreFilter Eq(string property, object? value) =>
        new(Kind.Eq, RequireProperty(property), value, []);

    public static StoreFilter Ne(string property, object? value) =>
        new(Kind.Ne, RequireProperty(property), value, []);

    public static StoreFilter Gte(string property, object value) =>
        new(Kind.Gte, RequireProperty(property), value, []);

    public static StoreFilter Lt(string property, object value) =>
        new(Kind.Lt, RequireProperty(property), value, []);

    public static StoreFilter And(params StoreFilter[] filters) =>
        new(Kind.And, null, null, filters ?? []);

    public static StoreFilter Or(params StoreFilter[] filters) =>
        new(Kind.Or, null, null, filters ?? []);

    static string RequireProperty(string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        return property;
    }
}
