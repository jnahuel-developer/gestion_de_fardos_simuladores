namespace ScaleSimulator;

internal enum ButtonLineMode
{
    Rts,
    Dtr
}

internal static class ButtonLineHelper
{
    public static ButtonLineMode Parse(string value)
    {
        if (TryParse(value, out var result))
        {
            return result;
        }

        throw new ArgumentException("El valor de --button-line debe ser 'rts' o 'dtr'.");
    }

    public static bool TryParse(string value, out ButtonLineMode result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "rts":
                result = ButtonLineMode.Rts;
                return true;

            case "dtr":
                result = ButtonLineMode.Dtr;
                return true;

            default:
                result = default;
                return false;
        }
    }

    public static string Describe(ButtonLineMode value)
    {
        return value switch
        {
            ButtonLineMode.Rts => "RTS",
            ButtonLineMode.Dtr => "DTR",
            _ => value.ToString().ToUpperInvariant()
        };
    }
}
