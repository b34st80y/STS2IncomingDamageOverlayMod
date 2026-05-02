using System.Reflection;

namespace STS2IncomingDamageOverlayMod.Infrastructure;

internal static class ReflectionMemberReader
{
    public static int ReadInt(object? source, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = ReadMember(source, name);
            if (value is null)
            {
                continue;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                // Ignore non-numeric candidate members.
            }
        }

        return 0;
    }

    public static string ReadString(object? source, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = ReadMember(source, name);
            if (value is null)
            {
                continue;
            }

            string? text = Convert.ToString(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return "";
    }

    public static bool ReadBool(object? source, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = ReadMember(source, name);
            if (value is null)
            {
                continue;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                // Ignore non-boolean candidate members.
            }
        }

        return false;
    }

    public static object? ReadMember(object? source, string name)
    {
        if (source is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = source.GetType();

        PropertyInfo? property = type.GetProperty(name, flags);
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
            return SafeGet(() => property.GetValue(source));
        }

        FieldInfo? field = type.GetField(name, flags);
        if (field is not null)
        {
            return SafeGet(() => field.GetValue(source));
        }

        MethodInfo? method = type.GetMethods(flags)
            .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == 0);
        if (method is not null)
        {
            return SafeGet(() => method.Invoke(source, null));
        }

        return null;
    }

    private static object? SafeGet(Func<object?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }
}
