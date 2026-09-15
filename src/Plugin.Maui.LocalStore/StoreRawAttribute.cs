namespace Plugin.Maui.LocalStore;

/// <summary>
/// Raw SQL and/or NQL for a generated DAO method. <see cref="Sql"/> runs on SQL engines;
/// <see cref="Nql"/> runs on Nuvexa. <see cref="Command"/> is used when the dialect-specific
/// text is omitted. Placeholders are <c>{parameterName}</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class StoreRawAttribute : Attribute
{
    public StoreRawAttribute()
    {
    }

    public StoreRawAttribute(string command)
    {
        Command = command;
    }

    public string? Command { get; }

    public string? Sql { get; init; }

    public string? Nql { get; init; }
}
