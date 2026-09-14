using Microsoft.Maui.Hosting;

namespace Plugin.Maui.LocalStore;

/// <summary>MAUI host registration for the Room-style local store.</summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="ILocalStore"/>. The host picks the engine on
    /// <see cref="LocalStoreOptions.Backend"/>. Switching backends does not migrate data.
    /// </summary>
    public static MauiAppBuilder UseMauiLocalStore(
        this MauiAppBuilder builder,
        Action<LocalStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddMauiLocalStore(configure);
        return builder;
    }
}
