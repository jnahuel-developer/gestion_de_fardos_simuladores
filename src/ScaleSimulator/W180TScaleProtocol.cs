using System.Globalization;
using System.IO.Ports;

namespace ScaleSimulator;

internal sealed class W180TScaleProtocol : IScaleProtocol
{
    private const byte Stx = 0x02;
    private const byte StateA = 0x00;
    private const byte StateB = 0x01;
    private const byte StateC = 0x00;
    private const byte Cr = 0x0D;
    private const byte Lf = 0x0A;

    public ScaleProtocolKind Kind => ScaleProtocolKind.W180T;
    public string Id => ScaleProtocolKindHelper.ToId(Kind);
    public string DisplayName => "W180-T";
    public bool SupportsTare => true;

    public SerialProfile ResolveSerialSettings(ScaleOptions options)
    {
        return new SerialProfile(9600, 7, Parity.Even, StopBits.Two, Handshake.None);
    }

    public void ValidateConfiguration(WeightSource weightSource, long tareValue)
    {
        ValidateValueRange(tareValue, "tara");

        foreach (ScaleWeightSample sample in weightSource.AllWeights)
        {
            ValidateValueRange(sample.WeightValue, $"peso '{sample.RawWeightText}'");
        }
    }

    public byte[] EncodeFrame(ScaleReading reading, ScaleOptions options)
    {
        ValidateValueRange(reading.WeightValue, "peso");
        ValidateValueRange(reading.TareValue, "tara");

        string weight = reading.WeightValue.ToString("D6", CultureInfo.InvariantCulture);
        string tare = reading.TareValue.ToString("D6", CultureInfo.InvariantCulture);

        byte[] frame = new byte[18];
        frame[0] = Stx;
        frame[1] = StateA;
        frame[2] = StateB;
        frame[3] = StateC;

        System.Text.Encoding.ASCII.GetBytes(weight, 0, weight.Length, frame, 4);
        System.Text.Encoding.ASCII.GetBytes(tare, 0, tare.Length, frame, 10);

        frame[16] = Cr;
        frame[17] = Lf;
        return frame;
    }

    public string DescribeFrame(ScaleReading reading)
    {
        return $"WEIGHT={reading.WeightValue:D6} TARE={reading.TareValue:D6}";
    }

    public override string ToString()
    {
        return DisplayName;
    }

    private static void ValidateValueRange(long value, string label)
    {
        if (value < 0 || value > ScaleOptions.MaxTareValue)
        {
            throw new InvalidOperationException(
                $"El valor de {label} debe estar entre 0 y {ScaleOptions.MaxTareValue} para el protocolo W180-T.");
        }
    }
}
