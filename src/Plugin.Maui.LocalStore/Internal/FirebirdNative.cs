using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Plugin.Maui.LocalStore;

static class FirebirdNative
{
    const int RtldNow = 2;
    const int RtldGlobal = 8;

    public static string? FindClientLibrary()
    {
        foreach (var candidate in ClientCandidates())
        {
            if (File.Exists(candidate))
            {
                var root = RootFromClient(candidate);
                if (root is not null)
                {
                    Environment.SetEnvironmentVariable("FIREBIRD", root);
                    var lockDir = Path.Combine(Path.GetTempPath(), "Plugin.Maui.LocalStore.fb-lock-" + Environment.ProcessId);
                    var tmpDir = Path.Combine(Path.GetTempPath(), "Plugin.Maui.LocalStore.fb-tmp-" + Environment.ProcessId);
                    Directory.CreateDirectory(lockDir);
                    Directory.CreateDirectory(tmpDir);
                    Environment.SetEnvironmentVariable("FIREBIRD_LOCK", lockDir);
                    Environment.SetEnvironmentVariable("FIREBIRD_TMP", tmpDir);
                    if (OperatingSystem.IsMacOS())
                    {
                        EnsureMacLayout(root);
                    }
                }

                Preload(candidate);
                return candidate;
            }
        }

        return null;
    }

    static IEnumerable<string> ClientCandidates()
    {
        var fromEnv = Environment.GetEnvironmentVariable("FIREBIRD_CLIENT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            yield return fromEnv;
        }

        var firebird = Environment.GetEnvironmentVariable("FIREBIRD");
        if (!string.IsNullOrWhiteSpace(firebird))
        {
            yield return Path.Combine(firebird, "lib", "libfbclient.dylib");
            yield return Path.Combine(firebird, "libfbclient.dylib");
            yield return Path.Combine(firebird, "fbclient.dll");
            yield return Path.Combine(firebird, "libfbclient.so");
        }

        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "firebird", "lib", "libfbclient.dylib");
        yield return Path.Combine(baseDir, "libfbclient.dylib");
        yield return Path.Combine(baseDir, "fbclient.dll");
        yield return Path.Combine(baseDir, "libfbclient.so");
        yield return Path.Combine(baseDir, "runtimes", "osx-arm64", "native", "libfbclient.dylib");
        yield return Path.Combine(baseDir, "runtimes", "osx-x64", "native", "libfbclient.dylib");
        yield return Path.Combine(baseDir, "runtimes", "linux-x64", "native", "libfbclient.so");
        yield return Path.Combine(baseDir, "runtimes", "linux-arm64", "native", "libfbclient.so");
        yield return Path.Combine(baseDir, "runtimes", "win-x64", "native", "fbclient.dll");
        yield return "/opt/homebrew/opt/firebird/lib/libfbclient.dylib";
        yield return "/opt/homebrew/lib/libfbclient.dylib";
        yield return "/usr/local/lib/libfbclient.dylib";
        yield return "/Library/Frameworks/Firebird.framework/Resources/lib/libfbclient.dylib";
        yield return "/Library/Frameworks/Firebird.framework/Versions/A/Resources/lib/libfbclient.dylib";
        yield return "/usr/lib/libfbclient.so";
        yield return "/usr/lib/x86_64-linux-gnu/libfbclient.so";
        yield return @"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll";
    }

    public static bool TryCreateDatabase(string databasePath, string user, string password)
    {
        var firebird = Environment.GetEnvironmentVariable("FIREBIRD");
        if (string.IsNullOrWhiteSpace(firebird))
        {
            return false;
        }

        var isql = Path.Combine(firebird, "bin", OperatingSystem.IsWindows() ? "isql.exe" : "isql");
        if (!File.Exists(isql))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = isql,
                ArgumentList = { "-q" },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process is null)
            {
                return false;
            }

            process.StandardInput.WriteLine(
                "CREATE DATABASE '" + databasePath.Replace("'", "''", StringComparison.Ordinal) +
                "' USER '" + user.Replace("'", "''", StringComparison.Ordinal) +
                "' PASSWORD '" + password.Replace("'", "''", StringComparison.Ordinal) + "';");
            process.StandardInput.Close();
            if (!process.WaitForExit(30_000))
            {
                try
                {
                    process.Kill();
                }
                catch (Exception)
                {
                }

                return false;
            }

            return File.Exists(databasePath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void Preload(string client)
    {
        var lib = Path.GetDirectoryName(client);
        if (string.IsNullOrWhiteSpace(lib))
        {
            return;
        }

        foreach (var name in new[]
                 {
                     "libtommath.dylib", "libtomcrypt.dylib", "libib_util.dylib",
                     "libicudata.dylib", "libicuuc.dylib", "libicui18n.dylib",
                     "libtommath.so", "libtomcrypt.so", "libib_util.so"
                 })
        {
            TryLoadGlobal(Path.Combine(lib, name));
        }

        TryLoadGlobal(client);
    }

    static void EnsureMacLayout(string root)
    {
        var marker = Path.Combine(root, ".localstore-rpath");
        if (File.Exists(marker))
        {
            return;
        }

        var lib = Path.Combine(root, "lib");
        if (Directory.Exists(lib))
        {
            foreach (var dylib in Directory.EnumerateFiles(lib, "*.dylib"))
            {
                foreach (var dep in new[]
                         {
                             "libtommath.dylib", "libtomcrypt.dylib", "libfbclient.dylib",
                             "libib_util.dylib", "libicuuc.dylib", "libicui18n.dylib", "libicudata.dylib"
                         })
                {
                    TryChangeInstallName(dylib, "@rpath/lib/" + dep, "@loader_path/" + dep);
                }

                TryCodesign(dylib);
            }
        }

        var plugins = Path.Combine(root, "plugins");
        if (Directory.Exists(plugins))
        {
            foreach (var dylib in Directory.EnumerateFiles(plugins, "*.dylib"))
            {
                TryAddRpath(dylib, "@loader_path/..");
            }

            var udr = Path.Combine(plugins, "udr");
            if (Directory.Exists(udr))
            {
                foreach (var dylib in Directory.EnumerateFiles(udr, "*.dylib"))
                {
                    TryAddRpath(dylib, "@loader_path/../..");
                }
            }
        }

        var intl = Path.Combine(root, "intl");
        if (Directory.Exists(intl))
        {
            foreach (var dylib in Directory.EnumerateFiles(intl, "*.dylib"))
            {
                TryAddRpath(dylib, "@loader_path/..");
            }
        }

        try
        {
            File.WriteAllText(marker, root);
        }
        catch (IOException)
        {
        }
    }

    static void TryAddRpath(string dylib, string rpath)
    {
        TryInstallNameTool("-add_rpath", rpath, dylib);
        TryCodesign(dylib);
    }

    static void TryChangeInstallName(string dylib, string from, string to) =>
        TryInstallNameTool("-change", from, to, dylib);

    static void TryInstallNameTool(params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "install_name_tool",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var tool = Process.Start(start);
            tool?.WaitForExit(15_000);
        }
        catch (Exception)
        {
        }
    }

    static void TryCodesign(string dylib)
    {
        try
        {
            using var sign = Process.Start(new ProcessStartInfo
            {
                FileName = "codesign",
                ArgumentList = { "--force", "-s", "-", dylib },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            sign?.WaitForExit(15_000);
        }
        catch (Exception)
        {
        }
    }

    static void TryLoadGlobal(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            _ = DlopenMac(path, RtldNow | RtldGlobal);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            _ = DlopenLinux(path, RtldNow | RtldGlobal);
            return;
        }

        NativeLibrary.TryLoad(path, out _);
    }

    [DllImport("libSystem.dylib", EntryPoint = "dlopen")]
    static extern IntPtr DlopenMac(string path, int mode);

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    static extern IntPtr DlopenLinux(string path, int mode);

    static string? RootFromClient(string client)
    {
        var lib = Path.GetDirectoryName(client);
        if (string.IsNullOrWhiteSpace(lib))
        {
            return null;
        }

        var root = Directory.GetParent(lib)?.FullName;
        return root is not null && Directory.Exists(Path.Combine(root, "plugins")) ? root : lib;
    }
}
