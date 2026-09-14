namespace Plugin.Maui.LocalStore;

static class MissingNative
{
    public static bool Matches(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is DllNotFoundException or BadImageFormatException or TypeInitializationException)
            {
                return true;
            }

            if (LooksLikeMissingLibrary(ex.Message))
            {
                return true;
            }
        }

        return false;
    }

    static bool LooksLikeMissingLibrary(string message) =>
        message.Contains("Unable to load shared library", StringComparison.OrdinalIgnoreCase)
        || message.Contains("libduckdb", StringComparison.OrdinalIgnoreCase)
        || message.Contains("duckdb.dll", StringComparison.OrdinalIgnoreCase)
        || message.Contains("librocksdb", StringComparison.OrdinalIgnoreCase)
        || message.Contains("kernel32", StringComparison.OrdinalIgnoreCase)
        || message.Contains("fbclient", StringComparison.OrdinalIgnoreCase)
        || message.Contains("fbembed", StringComparison.OrdinalIgnoreCase);
}
