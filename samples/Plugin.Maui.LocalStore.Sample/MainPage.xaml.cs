namespace Plugin.Maui.LocalStore.Sample;

public partial class MainPage : ContentPage
{
    static readonly (StoreBackend Backend, string File)[] Choices =
    [
        (StoreBackend.Sqlite, "app.db"),
        (StoreBackend.Nuvexa, "app.nvx"),
        (StoreBackend.LiteDb, "app.litedb"),
        (StoreBackend.SqlCipher, "app-cipher.db"),
        (StoreBackend.Realm, "app.realm"),
        (StoreBackend.DuckDb, "app.duckdb"),
        (StoreBackend.Firebird, "app.fdb"),
        (StoreBackend.Lmdb, "app.lmdb"),
        (StoreBackend.RocksDb, "app.rocksdb"),
        (StoreBackend.LevelDb, "app.leveldb")
    ];

    bool _ready;

    public MainPage()
    {
        InitializeComponent();
        StatusEntry.SelectedIndex = 0;
        FilterPicker.SelectedIndex = 0;
        FilterStatusPicker.SelectedIndex = 0;
        BackendPicker.SelectedIndex = 0;
        _ready = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (MainActivity.RunAllEngines)
        {
            MainActivity.RunAllEngines = false;
            await SafeAsync(TestAllEnginesAsync);
            return;
        }
#endif
        await SafeAsync(() => RefreshAsync(logFind: true));
    }

    void OnBackendChanged(object? sender, EventArgs e)
    {
        if (!_ready || BackendPicker.SelectedIndex < 0)
        {
            return;
        }

        _ = SafeAsync(() => OpenSelectedAsync());
    }

    void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        _ = SafeAsync(() => RefreshAsync(logFind: true));
    }

    async void OnInsertClicked(object? sender, EventArgs e) =>
        await SafeAsync(async () =>
        {
            var users = Users();
            var person = ReadForm(requireId: false);
            var id = await users.InsertAsync(person).ConfigureAwait(true);
            IdEntry.Text = id;
            Output.Text = $"Inserted {person.Name} ({id}).";
            await RefreshAsync().ConfigureAwait(true);
        });

    async void OnUpdateClicked(object? sender, EventArgs e) =>
        await SafeAsync(async () =>
        {
            var users = Users();
            var person = ReadForm(requireId: true);
            await users.ReplaceAsync(person).ConfigureAwait(true);
            Output.Text = $"Updated {person.Id}.";
            await RefreshAsync().ConfigureAwait(true);
        });

    async void OnDeleteClicked(object? sender, EventArgs e) =>
        await SafeAsync(async () =>
        {
            var id = IdEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Set Id (tap a row or Load Id).");
            }

            var deleted = await Users().DeleteByIdAsync(id).ConfigureAwait(true);
            Output.Text = deleted ? $"Deleted {id}." : $"No row {id}.";
            if (deleted)
            {
                OnClearFormClicked(sender, e);
            }

            await RefreshAsync().ConfigureAwait(true);
        });

    async void OnLoadIdClicked(object? sender, EventArgs e) =>
        await SafeAsync(async () =>
        {
            var id = IdEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Enter an Id.");
            }

            var person = await Users().FindByIdAsync(id).ConfigureAwait(true);
            if (person is null)
            {
                Output.Text = $"No row {id}.";
                return;
            }

            BindForm(person);
            Output.Text = $"Loaded {person.Name}.";
        });

    async void OnSeedClicked(object? sender, EventArgs e) =>
        await SafeAsync(async () =>
        {
            var users = Users();
            await users.EnsureIndexAsync("Age").ConfigureAwait(true);
            await users.EnsureIndexAsync("City", "Status").ConfigureAwait(true);
            await users.InsertManyAsync(
            [
                new Person { Name = "Ada", Age = 36, Status = "active", City = "London" },
                new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
                new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
                new Person { Name = "Alan", Age = 42, Status = "active", City = "London" },
                new Person { Name = "Scratch", Age = 19, Status = "active", City = "Paris" }
            ]).ConfigureAwait(true);
            Output.Text = "Seeded Ada, Grace, Cara, Alan, Scratch.";
            await RefreshAsync().ConfigureAwait(true);
        });

    async void OnResetClicked(object? sender, EventArgs e) =>
        await SafeAsync(() => OpenSelectedAsync(reset: true));

    void OnClearFormClicked(object? sender, EventArgs e)
    {
        IdEntry.Text = "";
        NameEntry.Text = "";
        AgeEntry.Text = "";
        CityEntry.Text = "";
        StatusEntry.SelectedIndex = 0;
        PeopleList.SelectedItem = null;
    }

    async void OnFindClicked(object? sender, EventArgs e) =>
        await SafeAsync(() => RefreshAsync(logFind: true));

    async void OnTourClicked(object? sender, EventArgs e) =>
        await SafeAsync(async () =>
        {
            await OpenSelectedAsync(reset: true).ConfigureAwait(true);
            Output.Text = await StoreTour.RunAsync(LocalStore.Current).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        });

    async void OnTestAllClicked(object? sender, EventArgs e) =>
        await SafeAsync(TestAllEnginesAsync);

    async Task TestAllEnginesAsync()
    {
        var log = new System.Text.StringBuilder();
        var passed = 0;
        foreach (var choice in Choices)
        {
            var started = DateTime.UtcNow;
            try
            {
                StatusLabel.Text = $"Testing {choice.Backend}…";
                await OpenChoiceAsync(choice, reset: true, refresh: false).ConfigureAwait(true);
                await StoreTour.RunAsync(LocalStore.Current).ConfigureAwait(true);
                await AssertTourResultsAsync().ConfigureAwait(true);
                var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                log.AppendLine($"PASS {choice.Backend} ({ms} ms)");
                passed++;
                WriteLog($"PASS {choice.Backend}");
            }
            catch (Exception ex)
            {
                log.AppendLine($"FAIL {choice.Backend}: {ex.Message}");
                WriteLog($"FAIL {choice.Backend}: {ex}");
            }
        }

        log.AppendLine($"{passed}/{Choices.Length} engines passed.");
        Output.Text = log.ToString();
        StatusLabel.Text = $"{passed}/{Choices.Length} engines passed.";
        var report = Path.Combine(FileSystem.AppDataDirectory, "engine-test.txt");
        File.WriteAllText(report, log.ToString());
        WriteLog(log.ToString());
        await PageScroll.ScrollToAsync(Output, ScrollToPosition.Start, animated: true);
    }

    static async Task AssertTourResultsAsync()
    {
        var users = Users();
        var adults = await users.FindAsync(
            StoreFilter.Gte("Age", 21),
            new StoreQuery { SortBy = "Name", Limit = 10 }).ConfigureAwait(true);
        var names = adults.Select(p => p.Name).ToArray();
        if (names is not ["Ada Lovelace", "Alan", "Cara", "Grace"])
        {
            throw new InvalidOperationException("Age>=21 select: " + string.Join(", ", names));
        }

        var london = await users.FindAsync(
            StoreFilter.And(StoreFilter.Eq("City", "London"), StoreFilter.Eq("Status", "active")),
            new StoreQuery { SortBy = "Age", SortDescending = true }).ConfigureAwait(true);
        var londonNames = london.Select(p => p.Name).ToArray();
        if (londonNames is not ["Alan", "Ada Lovelace"])
        {
            throw new InvalidOperationException("London+active select: " + string.Join(", ", londonNames));
        }

        var orRows = await users.FindAsync(
            StoreFilter.Or(StoreFilter.Lt("Age", 30), StoreFilter.Eq("City", "NewYork"))).ConfigureAwait(true);
        if (orRows.Count != 2)
        {
            throw new InvalidOperationException("OR select count " + orRows.Count);
        }
    }

    static void WriteLog(string message)
    {
#if ANDROID
        Android.Util.Log.Info("LocalStoreSample", message);
#endif
    }

    void OnPersonSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Person person)
        {
            BindForm(person);
        }
    }

    async Task OpenSelectedAsync(bool reset = false)
    {
        var choice = Choices[Math.Clamp(BackendPicker.SelectedIndex, 0, Choices.Length - 1)];
        await OpenChoiceAsync(choice, reset, refresh: true).ConfigureAwait(true);
    }

    async Task OpenChoiceAsync((StoreBackend Backend, string File) choice, bool reset, bool refresh)
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, choice.File);
        if (choice.Backend == StoreBackend.Firebird)
        {
            PrepareFirebirdNative();
        }

        if (LocalStore.IsInitialized)
        {
            await LocalStore.Current.DisposeAsync().ConfigureAwait(true);
        }

        if (reset)
        {
            DeleteStore(path);
        }

        LocalStore.Open(new LocalStoreOptions
        {
            Backend = choice.Backend,
            Path = path,
            EncryptionKey = choice.Backend is StoreBackend.Nuvexa or StoreBackend.SqlCipher
                ? "sample-key"
                : null
        });

        StatusLabel.Text = $"{choice.Backend} · {path}";
        Output.Text = reset ? $"Opened a new {choice.Backend} file." : $"Opened {choice.Backend}.";
        if (refresh)
        {
            await RefreshAsync(logFind: true).ConfigureAwait(true);
        }
    }

    async Task RefreshAsync(bool logFind = false)
    {
        if (!LocalStore.IsInitialized)
        {
            await OpenSelectedAsync().ConfigureAwait(true);
            return;
        }

        var (filter, query) = CurrentFilter();
        var rows = await Users().FindAsync(filter, query).ConfigureAwait(true);
        PeopleList.ItemsSource = rows;
        StatusLabel.Text = $"{LocalStore.Current.Backend} · {rows.Count} row(s)";
        if (logFind)
        {
            Output.Text = DescribeFilter(filter, query, rows.Count);
        }
    }

    (StoreFilter? Filter, StoreQuery Query) CurrentFilter()
    {
        var query = new StoreQuery { SortBy = "Name", Limit = 100 };
        return FilterPicker.SelectedIndex switch
        {
            1 => (StoreFilter.Gte("Age", 21), query),
            2 => (StoreFilter.And(StoreFilter.Eq("City", "London"), StoreFilter.Eq("Status", "active")),
                new StoreQuery { SortBy = "Age", SortDescending = true, Limit = 100 }),
            3 => (StoreFilter.Or(StoreFilter.Lt("Age", 30), StoreFilter.Eq("City", "NewYork")), query),
            4 => (CustomFilter(), query),
            _ => (null, query)
        };
    }

    StoreFilter? CustomFilter()
    {
        var parts = new List<StoreFilter>();
        if (int.TryParse(MinAgeEntry.Text, out var minAge))
        {
            parts.Add(StoreFilter.Gte("Age", minAge));
        }

        var city = FilterCityEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(city))
        {
            parts.Add(StoreFilter.Eq("City", city));
        }

        if (FilterStatusPicker.SelectedIndex > 0)
        {
            parts.Add(StoreFilter.Eq("Status", FilterStatusPicker.Items[FilterStatusPicker.SelectedIndex]));
        }

        return parts.Count == 0 ? null : parts.Count == 1 ? parts[0] : StoreFilter.And(parts.ToArray());
    }

    static string DescribeFilter(StoreFilter? filter, StoreQuery query, int count)
    {
        var sort = string.IsNullOrWhiteSpace(query.SortBy)
            ? ""
            : $", sort {query.SortBy}{(query.SortDescending ? " desc" : "")}";
        return filter is null
            ? $"FindAsync (all{sort}) → {count} row(s)."
            : $"FindAsync{sort} → {count} row(s).";
    }

    Person ReadForm(bool requireId)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Name is required.");
        }

        if (!int.TryParse(AgeEntry.Text, out var age) || age < 0)
        {
            throw new InvalidOperationException("Age must be a number.");
        }

        var id = IdEntry.Text?.Trim();
        if (requireId && string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Set Id (tap a row or Load Id).");
        }

        return new Person
        {
            Id = string.IsNullOrWhiteSpace(id) ? null : id,
            Name = name,
            Age = age,
            Status = StatusEntry.SelectedIndex >= 0
                ? StatusEntry.Items[StatusEntry.SelectedIndex]
                : "active",
            City = CityEntry.Text?.Trim()
        };
    }

    void BindForm(Person person)
    {
        IdEntry.Text = person.Id;
        NameEntry.Text = person.Name;
        AgeEntry.Text = person.Age.ToString();
        CityEntry.Text = person.City;
        var status = StatusEntry.Items.IndexOf(person.Status);
        StatusEntry.SelectedIndex = status >= 0 ? status : 0;
    }

    static IStoreCollection<Person> Users() => LocalStore.Current.GetCollection<Person>("users");

    async Task SafeAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Output.Text = ex.Message;
            StatusLabel.Text = LocalStore.IsInitialized
                ? $"{LocalStore.Current.Backend} · {ex.GetType().Name}"
                : ex.GetType().Name;
        }
    }

    static void PrepareFirebirdNative()
    {
        var dest = Path.Combine(FileSystem.AppDataDirectory, "firebird");
        var extracted = "/tmp/fb-pkg/Firebird.pkg/Payload/Versions/A/Resources";
        if (Directory.Exists(extracted) && !File.Exists(Path.Combine(dest, "lib", "libfbclient.dylib")))
        {
            CopyDirectory(extracted, dest);
        }

        var client = Path.Combine(dest, "lib", "libfbclient.dylib");
        if (File.Exists(client))
        {
            Environment.SetEnvironmentVariable("FIREBIRD", dest);
            Environment.SetEnvironmentVariable("FIREBIRD_CLIENT", client);
        }
    }

    static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination), overwrite: true);
        }
    }

    static void DeleteStore(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }

        foreach (var extra in new[] { path + "-wal", path + ".lock", path + ".note" })
        {
            if (File.Exists(extra))
            {
                File.Delete(extra);
            }
        }

        var kv = path + ".kv";
        if (Directory.Exists(kv))
        {
            Directory.Delete(kv, recursive: true);
        }
    }
}
