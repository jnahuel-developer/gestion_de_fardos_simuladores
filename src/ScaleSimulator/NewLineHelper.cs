namespace ScaleSimulator;

internal static class NewLineHelper
{
    public static bool IsSupportedMode(string mode)
    {
        return mode.Equals("crlf", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("lf", StringComparison.OrdinalIgnoreCase);
    }

    public static string Resolve(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "crlf" => "\r\n",
            "lf" => "\n",
            _ => throw new ArgumentException($"Modo de newline no soportado: '{mode}'.")
        };
    }

    public static string Describe(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "crlf" => @"\r\n",
            "lf" => @"\n",
            _ => mode
        };
    }
}