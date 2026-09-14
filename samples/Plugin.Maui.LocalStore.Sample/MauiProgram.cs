using Microsoft.Extensions.Logging;
#if ANDROID
using Android.OS;
using Android.Widget;
using Microsoft.Maui.Handlers;
#endif

namespace Plugin.Maui.LocalStore.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        FixAndroidTextBounds();
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<MainPage>();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        // The sample opens the engine from the Backend picker (LocalStore.Open).
        // A host app that uses one engine would call UseMauiLocalStore here instead.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    static void FixAndroidTextBounds()
    {
#if ANDROID
        static void Apply(TextView view)
        {
            if ((int)Build.VERSION.SdkInt < 35)
            {
                return;
            }

            view.UseBoundsForWidth = false;
            view.ShiftDrawingOffsetForStartOverhang = false;
        }

        LabelHandler.Mapper.AppendToMapping("AndroidTextBounds", (handler, _) => Apply(handler.PlatformView));
        ButtonHandler.Mapper.AppendToMapping("AndroidTextBounds", (handler, _) => Apply(handler.PlatformView));
        EntryHandler.Mapper.AppendToMapping("AndroidTextBounds", (handler, _) => Apply(handler.PlatformView));
        PickerHandler.Mapper.AppendToMapping("AndroidTextBounds", (handler, _) =>
        {
            if (handler.PlatformView is TextView view)
            {
                Apply(view);
            }
        });
#endif
    }
}
