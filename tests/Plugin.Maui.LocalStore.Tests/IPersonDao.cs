namespace Plugin.Maui.LocalStore.Tests;

[StoreDao("users", typeof(Person))]
public interface IPersonDao
{
    Task<string> InsertAsync(Person item, CancellationToken cancellationToken = default);

    Task<Person?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Person>> FindAsync(
        StoreFilter? filter = null,
        StoreQuery? query = null,
        CancellationToken cancellationToken = default);

    [StoreRaw(
        Sql = "SELECT * FROM users WHERE Age >= {minAge}",
        Nql = "db.users.find({ age: { $gte: {minAge} } })")]
    Task<IReadOnlyList<Person>> FindAdultsAsync(int minAge, CancellationToken cancellationToken = default);
}
