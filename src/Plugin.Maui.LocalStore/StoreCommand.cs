using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Plugin.Maui.LocalStore;

/// <summary>Formats <c>{name}</c> placeholders in raw SQL or NQL.</summary>
public static partial class StoreCommand
{
    public static string Bind(string template, IReadOnlyDictionary<string, object?> args, StoreQueryLanguage language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(args);
        if (language is StoreQueryLanguage.None)
        {
            throw new LocalStoreException("This engine has no SQL or NQL.");
        }

        return Placeholder().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!args.TryGetValue(key, out var value) &&
                int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                index >= 0 &&
                index < args.Count)
            {
                value = args.ElementAt(index).Value;
            }
            else if (!args.TryGetValue(key, out value))
            {
                throw new LocalStoreException($"Unknown query placeholder '{{{key}}}'.");
            }

            return language == StoreQueryLanguage.Nql ? FormatNql(value) : FormatSql(value);
        });
    }

    public static string FormatSql(object? value) => value switch
    {
        null => "NULL",
        string text => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'",
        bool flag => flag ? "1" : "0",
        DateTime date => "'" + date.ToString("o", CultureInfo.InvariantCulture) + "'",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
        _ => "'" + Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("'", "''", StringComparison.Ordinal) +
             "'"
    };

    public static string FormatNql(object? value) => value switch
    {
        null => "null",
        string text => JsonSerializer.Serialize(text),
        bool flag => flag ? "true" : "false",
        DateTime date => JsonSerializer.Serialize(date),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "null",
        _ => JsonSerializer.Serialize(Convert.ToString(value, CultureInfo.InvariantCulture))
    };

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*|\d+)\}")]
    private static partial Regex Placeholder();
}
