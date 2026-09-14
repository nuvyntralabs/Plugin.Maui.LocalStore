using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Plugin.Maui.LocalStore.Sample;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static bool RunAllEngines { get; set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        RunAllEngines = Intent?.GetBooleanExtra("run_all_engines", false) == true;
        base.OnCreate(savedInstanceState);
    }
}
