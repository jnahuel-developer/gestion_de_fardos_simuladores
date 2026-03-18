namespace ScaleSimulator;

internal interface IScaleProtocol
{
    ScaleProtocolKind Kind { get; }
    string Id { get; }
    string DisplayName { get; }
    bool SupportsTare { get; }
    SerialProfile ResolveSerialSettings(ScaleOptions options);
    void ValidateConfiguration(WeightSource weightSource, long tareValue);
    byte[] EncodeFrame(ScaleReading reading, ScaleOptions options);
    string DescribeFrame(ScaleReading reading);
}
