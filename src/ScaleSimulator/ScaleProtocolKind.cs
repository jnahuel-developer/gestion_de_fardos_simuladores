namespace ScaleSimulator;

internal enum ScaleProtocolKind
{
    SimpleAscii,
    W180T
}

internal static class ScaleProtocolKindHelper
{
    public static ScaleProtocolKind Parse(string value)
    {
        if (TryParse(value, out ScaleProtocolKind result))
        {
            return result;
        }

        throw new ArgumentException("El valor de --scale-protocol debe ser 'simple-ascii' o 'w180-t'.");
    }

    public static bool TryParse(string value, out ScaleProtocolKind result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "simple-ascii":
                result = ScaleProtocolKind.SimpleAscii;
                return true;

            case "w180-t":
                result = ScaleProtocolKind.W180T;
                return true;

            default:
                result = default;
                return false;
        }
    }

    public static string Describe(ScaleProtocolKind value)
    {
        return value switch
        {
            ScaleProtocolKind.SimpleAscii => "ASCII simple",
            ScaleProtocolKind.W180T => "W180-T",
            _ => value.ToString()
        };
    }

    public static string ToId(ScaleProtocolKind value)
    {
        return value switch
        {
            ScaleProtocolKind.SimpleAscii => "simple-ascii",
            ScaleProtocolKind.W180T => "w180-t",
            _ => value.ToString().ToLowerInvariant()
        };
    }
}
