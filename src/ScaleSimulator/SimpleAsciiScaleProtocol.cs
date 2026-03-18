namespace ScaleSimulator;

internal sealed class SimpleAsciiScaleProtocol : IScaleProtocol
{
    public ScaleProtocolKind Kind => ScaleProtocolKind.SimpleAscii;
    public string Id => ScaleProtocolKindHelper.ToId(Kind);
    public string DisplayName => "ASCII simple";
    public bool SupportsTare => false;

    public SerialProfile ResolveSerialSettings(ScaleOptions options)
    {
        return SerialProfile.FromOptions(options);
    }

    public void ValidateConfiguration(WeightSource weightSource, long tareValue)
    {
        if (tareValue < 0 || tareValue > ScaleOptions.MaxTareValue)
        {
            throw new InvalidOperationException(
                $"La tara debe estar entre 0 y {ScaleOptions.MaxTareValue}.");
        }
    }

    public byte[] EncodeFrame(ScaleReading reading, ScaleOptions options)
    {
        string payload = reading.RawWeightText + NewLineHelper.Resolve(options.NewLineMode);
        return System.Text.Encoding.ASCII.GetBytes(payload);
    }

    public string DescribeFrame(ScaleReading reading)
    {
        return $"WEIGHT={reading.RawWeightText}";
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
