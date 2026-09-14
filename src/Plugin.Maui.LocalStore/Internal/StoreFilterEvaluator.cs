using System.Globalization;
using System.Reflection;

namespace Plugin.Maui.LocalStore;

static class StoreFilterEvaluator
{
    public static IReadOnlyList<T> Apply<T>(IEnumerable<T> rows, StoreFilter? filter, StoreQuery? query)
        where T : class
    {
        IEnumerable<T> result = filter is null ? rows : rows.Where(item => Matches(item, filter));

        if (!string.IsNullOrWhiteSpace(query?.SortBy))
        {
            var property = StoreNames.Property(query.SortBy);
            var info = typeof(T).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                       ?? throw new LocalStoreException($"Unknown property '{property}'.");
            result = query.SortDescending
                ? result.OrderByDescending(item => info.GetValue(item), Comparer<object?>.Create(Compare))
                : result.OrderBy(item => info.GetValue(item), Comparer<object?>.Create(Compare));
        }

        if (query is { Skip: > 0 })
        {
            result = result.Skip(query.Skip);
        }

        if (query is { Limit: > 0 })
        {
            result = result.Take(query.Limit);
        }

        return result.ToList();
    }

    public static bool Matches<T>(T item, StoreFilter filter) where T : class
    {
        if (filter.FilterKind is StoreFilter.Kind.And)
        {
            return filter.Children.Length == 0 || filter.Children.All(child => Matches(item, child));
        }

        if (filter.FilterKind is StoreFilter.Kind.Or)
        {
            return filter.Children.Length != 0 && filter.Children.Any(child => Matches(item, child));
        }

        var property = StoreNames.Property(filter.Property!);
        var info = typeof(T).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                   ?? throw new LocalStoreException($"Unknown property '{property}'.");
        var left = info.GetValue(item);
        var cmp = Compare(left, filter.Value);
        return filter.FilterKind switch
        {
            StoreFilter.Kind.Eq => cmp == 0,
            StoreFilter.Kind.Ne => cmp != 0,
            StoreFilter.Kind.Gte => cmp >= 0,
            StoreFilter.Kind.Lt => cmp < 0,
            _ => throw new LocalStoreException($"Unsupported filter {filter.FilterKind}.")
        };
    }

    static int Compare(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (IsNumber(left) && IsNumber(right))
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture)
                .CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }

        if (left is IComparable comparable && left.GetType() == right.GetType())
        {
            return comparable.CompareTo(right);
        }

        return string.CompareOrdinal(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture));
    }

    static bool IsNumber(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
