using System.Text;

namespace Plugin.Maui.LocalStore.Sample;

public static class StoreTour
{
    public static async Task<string> RunAsync(ILocalStore store)
    {
        var log = new StringBuilder();
        log.AppendLine($"Backend: {store.Backend}");
        var users = store.GetCollection<Person>("users");

        var adaId = await users.InsertAsync(new Person
        {
            Name = "Ada",
            Age = 36,
            Status = "active",
            City = "London"
        });
        await users.InsertManyAsync(
        [
            new Person { Name = "Grace", Age = 85, Status = "retired", City = "NewYork" },
            new Person { Name = "Cara", Age = 21, Status = "active", City = "Bengaluru" },
            new Person { Name = "Alan", Age = 42, Status = "active", City = "London" }
        ]);
        var scratchId = await users.InsertAsync(new Person
        {
            Name = "Scratch",
            Age = 19,
            Status = "active",
            City = "Paris"
        });

        await users.EnsureIndexAsync("Age");
        await users.EnsureIndexAsync("City", "Status");
        log.AppendLine($"Inserted Ada {adaId}.");

        var ada = await users.FindByIdAsync(adaId);
        log.AppendLine($"Read Ada: {ada?.Name} ({ada?.Age})");
        if (ada is not null)
        {
            ada.Name = "Ada Lovelace";
            await users.ReplaceAsync(ada);
            log.AppendLine($"Updated Ada: {(await users.FindByIdAsync(adaId))?.Name}");
        }

        log.AppendLine($"Deleted scratch: {await users.DeleteByIdAsync(scratchId)}");

        log.AppendLine("-- Age >= 21, sort Name, limit 10 --");
        foreach (var row in await users.FindAsync(
                     StoreFilter.Gte("Age", 21),
                     new StoreQuery { SortBy = "Name", Limit = 10 }))
        {
            log.AppendLine($"{row.Name} {row.Age} {row.City}");
        }

        log.AppendLine("-- London + active, sort Age desc --");
        foreach (var row in await users.FindAsync(
                     StoreFilter.And(StoreFilter.Eq("City", "London"), StoreFilter.Eq("Status", "active")),
                     new StoreQuery { SortBy = "Age", SortDescending = true }))
        {
            log.AppendLine($"{row.Name} {row.Age}");
        }

        log.AppendLine("-- Age < 30 or NewYork --");
        foreach (var row in await users.FindAsync(
                     StoreFilter.Or(StoreFilter.Lt("Age", 30), StoreFilter.Eq("City", "NewYork"))))
        {
            log.AppendLine($"{row.Name} {row.Age} {row.City}");
        }

        if (store is INuvexaLocalStore nuvexa)
        {
            log.AppendLine("-- NQL escape hatch --");
            foreach (var json in await nuvexa.ExecuteNqlAsync("db.users.find({ age: { $gte: 21 } }).limit(5)"))
            {
                log.AppendLine(json);
            }
        }

        return log.ToString();
    }
}
