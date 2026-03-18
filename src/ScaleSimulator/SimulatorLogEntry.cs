namespace ScaleSimulator;

internal enum SimulatorLogLevel
{
    Info,
    Warning,
    Error,
    Sent
}

internal readonly record struct SimulatorLogEntry(DateTime Timestamp, SimulatorLogLevel Level, string Message)
{
    public string ToDisplayLine()
    {
        string prefix = Level switch
        {
            SimulatorLogLevel.Warning => "WARN ",
            SimulatorLogLevel.Error => "ERROR",
            SimulatorLogLevel.Sent => "SENT ",
            _ => "INFO "
        };

        return $"[{Timestamp:HH:mm:ss.fff}] {prefix} {Message}";
    }
}
