using System.Text.Json;

namespace Plugin.Maui.LocalStore;

static class JsonEntity
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize<T>(T item) where T : class =>
        JsonSerializer.Serialize(item, Options);

    public static T Deserialize<T>(string json) where T : class, new() =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new LocalStoreException($"Could not deserialize {typeof(T).Name}.");
}
