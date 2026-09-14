using System.Text.RegularExpressions;

namespace Plugin.Maui.LocalStore;

static partial class StoreNames
{
    public static string Collection(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!CollectionPattern().IsMatch(name))
        {
            throw new LocalStoreException($"Collection name '{name}' must be letters, digits, or underscore.");
        }

        return name;
    }

    public static string Property(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!PropertyPattern().IsMatch(name))
        {
            throw new LocalStoreException($"Property name '{name}' must be a POCO identifier.");
        }

        return name;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex CollectionPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex PropertyPattern();
}
