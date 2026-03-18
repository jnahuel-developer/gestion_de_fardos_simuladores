using System.IO.Ports;

namespace ScaleSimulator;

internal readonly record struct SerialProfile(int BaudRate, int DataBits, Parity Parity, StopBits StopBits, Handshake Handshake)
{
    public SerialPort CreatePort(string portName)
    {
        return new SerialPort
        {
            PortName = portName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = Parity,
            StopBits = StopBits,
            Handshake = Handshake,
            Encoding = System.Text.Encoding.ASCII,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 500,
            WriteTimeout = 2000
        };
    }

    public string Describe()
    {
        return $"{BaudRate} / {DataBits}{DescribeParity(Parity)}{DescribeStopBits(StopBits)} / Handshake {Handshake}";
    }

    public static SerialProfile FromOptions(ScaleOptions options)
    {
        return new SerialProfile(
            options.BaudRate,
            options.DataBits,
            ParseParity(options.Parity),
            ParseStopBits(options.StopBits),
            Handshake.None);
    }

    public static Parity ParseParity(string value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out Parity parity))
        {
            return parity;
        }

        throw new ArgumentException(
            $"Valor de paridad invalido: '{value}'. Valores soportados: None, Odd, Even, Mark, Space.");
    }

    public static StopBits ParseStopBits(int value)
    {
        return value switch
        {
            1 => StopBits.One,
            2 => StopBits.Two,
            _ => throw new ArgumentException("El valor de --stopbits debe ser 1 o 2.")
        };
    }

    private static string DescribeParity(Parity value)
    {
        return value switch
        {
            Parity.None => "N",
            Parity.Odd => "O",
            Parity.Even => "E",
            Parity.Mark => "M",
            Parity.Space => "S",
            _ => value.ToString().ToUpperInvariant()
        };
    }

    private static string DescribeStopBits(StopBits value)
    {
        return value switch
        {
            StopBits.One => "1",
            StopBits.Two => "2",
            StopBits.OnePointFive => "1.5",
            _ => value.ToString()
        };
    }
}
