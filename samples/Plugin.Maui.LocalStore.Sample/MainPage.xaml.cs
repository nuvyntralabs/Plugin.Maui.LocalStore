namespace Plugin.Maui.LocalStore.Sample;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BackendPicker.SelectedIndex = 0;
    }

    void OnBackendChanged(object? sender, EventArgs e)
    {
        if (BackendPicker.SelectedIndex < 0)
        {
            return;
        }

        _ = OpenSelectedAsync();
    }

    async void OnTourClicked(object? sender, EventArgs e)
    {
        try
        {
            await OpenSelectedAsync(reset: true).ConfigureAwait(true);
            Output.Text = await StoreTour.RunAsync(LocalStore.Current).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Output.Text = ex.ToString();
        }
    }

    async Task OpenSelectedAsync(bool reset = false)
    {
        var nuvexa = BackendPicker.SelectedIndex == 0;
        var path = Path.Combine(
            FileSystem.AppDataDirectory,
            nuvexa ? "app.nvx" : "app.db");
        if (LocalStore.IsInitialized)
        {
            await LocalStore.Current.DisposeAsync().ConfigureAwait(true);
        }

        if (reset)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(path + "-wal"))
            {
                File.Delete(path + "-wal");
            }
        }

        LocalStore.Open(new LocalStoreOptions
        {
            Backend = nuvexa ? StoreBackend.Nuvexa : StoreBackend.Sqlite,
            Path = path,
            EncryptionKey = nuvexa ? "sample-key" : null
        });
    }
}
