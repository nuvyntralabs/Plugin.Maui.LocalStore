using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Plugin.Maui.LocalStore;

/// <summary>Registers <see cref="ILocalStore"/> without MAUI lifecycle hooks.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMauiLocalStore(this IServiceCollection services, LocalStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.TryAddSingleton<ILocalStore>(sp =>
        {
            var store = LocalStore.Open(sp.GetRequiredService<LocalStoreOptions>());
            return store;
        });
        return services;
    }

    public static IServiceCollection AddMauiLocalStore(
        this IServiceCollection services,
        Action<LocalStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new LocalStoreOptions();
        configure?.Invoke(options);
        return services.AddMauiLocalStore(options);
    }
}
