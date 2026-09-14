using System.Collections.Concurrent;
using Realms;

namespace Plugin.Maui.LocalStore;

sealed class RealmLocalStore : ILocalStore
{
    readonly RealmWorker _worker;

    public RealmLocalStore(LocalStoreOptions options)
    {
        var path = StorePaths.PrepareFile(options, "Realm");
        try
        {
            var configuration = new RealmConfiguration(path);
            if (!string.IsNullOrWhiteSpace(options.EncryptionKey))
            {
                configuration.EncryptionKey = ToRealmKey(options.EncryptionKey);
            }

            _worker = new RealmWorker(configuration);
        }
        catch (Exception ex)
        {
            throw StorePaths.Wrap("Realm", ex);
        }
    }

    public StoreBackend Backend => StoreBackend.Realm;

    public IStoreCollection<T> GetCollection<T>(string name) where T : class, new() =>
        new JsonKvStoreCollection<T>(new RealmKeyValue(_worker), StoreNames.Collection(name));

    public ValueTask DisposeAsync()
    {
        _worker.Dispose();
        return ValueTask.CompletedTask;
    }

    static byte[] ToRealmKey(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var padded = new byte[64];
        Array.Copy(bytes, padded, Math.Min(bytes.Length, padded.Length));
        return padded;
    }
}

sealed class RealmWorker : IDisposable
{
    readonly BlockingCollection<Action> _queue = new();
    readonly Thread _thread;
    readonly Realms.Realm _realm;

    public RealmWorker(RealmConfiguration configuration)
    {
        var ready = new TaskCompletionSource<Realms.Realm>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            try
            {
                var realm = Realms.Realm.GetInstance(configuration);
                ready.SetResult(realm);
                foreach (var action in _queue.GetConsumingEnumerable())
                {
                    action();
                }

                realm.Dispose();
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Plugin.Maui.LocalStore.Realm"
        };
        _thread.Start();
        _realm = ready.Task.GetAwaiter().GetResult();
    }

    public T Invoke<T>(Func<Realms.Realm, T> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                done.SetResult(work(_realm));
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });
        return done.Task.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}

sealed class RealmKeyValue(RealmWorker worker) : IJsonKeyValue
{
    public string? Get(string key) =>
        worker.Invoke(realm => realm.Find<RealmStoreRow>(key)?.Payload);

    public void Put(string key, string json)
    {
        var collection = key.Split('\u001f')[0];
        worker.Invoke(realm =>
        {
            realm.Write(() =>
            {
                realm.Add(new RealmStoreRow
                {
                    Key = key,
                    Collection = collection,
                    Payload = json
                }, update: true);
            });
            return 0;
        });
    }

    public bool Delete(string key) =>
        worker.Invoke(realm =>
        {
            var row = realm.Find<RealmStoreRow>(key);
            if (row is null)
            {
                return false;
            }

            realm.Write(() => realm.Remove(row));
            return true;
        });

    public IEnumerable<KeyValuePair<string, string>> Scan(string prefix) =>
        worker.Invoke(realm =>
            realm.All<RealmStoreRow>()
                .AsEnumerable()
                .Where(row => row.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(row => new KeyValuePair<string, string>(row.Key, row.Payload))
                .ToList());

    public void Dispose()
    {
    }
}
