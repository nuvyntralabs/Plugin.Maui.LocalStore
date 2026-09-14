using Microsoft.Extensions.Logging;

namespace Plugin.Maui.LocalStore.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<MainPage>();
        builder
            .UseMauiApp<App>()
            .UseMauiLocalStore(o =>
            {
                o.Backend = StoreBackend.Nuvexa;
                o.Path = Path.Combine(FileSystem.AppDataDirectory, "app.nvx");
                o.EncryptionKey = "sample-key";
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
