using Nuventra.NuvexaDB.Query;

namespace Plugin.Maui.LocalStore;

static class StoreFilterMapper
{
    public static NuvexaFilter ToNuvexa(StoreFilter? filter)
    {
        if (filter is null)
        {
            return NuvexaFilter.And();
        }

        return filter.FilterKind switch
        {
            StoreFilter.Kind.Eq => NuvexaFilter.Eq(JsonPath(filter.Property!), filter.Value),
            StoreFilter.Kind.Ne => NuvexaFilter.Ne(JsonPath(filter.Property!), filter.Value),
            StoreFilter.Kind.Gte => NuvexaFilter.Gte(JsonPath(filter.Property!), filter.Value!),
            StoreFilter.Kind.Lt => NuvexaFilter.Lt(JsonPath(filter.Property!), filter.Value!),
            StoreFilter.Kind.And => NuvexaFilter.And(filter.Children.Select(ToNuvexa).ToArray()),
            StoreFilter.Kind.Or => NuvexaFilter.Or(filter.Children.Select(ToNuvexa).ToArray()),
            _ => throw new LocalStoreException($"Unsupported filter {filter.FilterKind}.")
        };
    }

    public static string JsonPath(string property)
    {
        var name = StoreNames.Property(property);
        if (name.Equals("Id", StringComparison.OrdinalIgnoreCase) || name == "_id")
        {
            return "_id";
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
