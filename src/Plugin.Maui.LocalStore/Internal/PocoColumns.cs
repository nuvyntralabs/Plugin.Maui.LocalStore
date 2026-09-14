using System.Reflection;

namespace Plugin.Maui.LocalStore;

static class PocoColumns
{
    public static PropertyInfo[] Of<T>() where T : class =>
        typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.CanWrite && IsScalar(p.PropertyType))
            .ToArray();

    public static void RequireId<T>() where T : class
    {
        if (!Of<T>().Any(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)))
        {
            throw new LocalStoreException($"{typeof(T).Name} needs a public string Id property.");
        }
    }

    public static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string)
               || type == typeof(int)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(float)
               || type == typeof(bool)
               || type == typeof(DateTime);
    }

    public static object? ConvertTo(object? value, Type target)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        target = Nullable.GetUnderlyingType(target) ?? target;
        if (target.IsInstanceOfType(value))
        {
            return value;
        }

        if (target == typeof(string))
        {
            return Convert.ToString(value);
        }

        if (target == typeof(int))
        {
            return Convert.ToInt32(value);
        }

        if (target == typeof(long))
        {
            return Convert.ToInt64(value);
        }

        if (target == typeof(double))
        {
            return Convert.ToDouble(value);
        }

        if (target == typeof(float))
        {
            return Convert.ToSingle(value);
        }

        if (target == typeof(bool))
        {
            return Convert.ToBoolean(value);
        }

        if (target == typeof(DateTime))
        {
            return Convert.ToDateTime(value);
        }

        return Convert.ChangeType(value, target);
    }
}
