namespace ScaleSimulator;

internal sealed class ScaleOptions
{
    public const long MaxTareValue = 999999;

    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public int StopBits { get; set; } = 1;
    public string Parity { get; set; } = "None";
    public int IntervalMs { get; set; } = 1000;
    public string? FilePath { get; set; }
    public string NewLineMode { get; set; } = "crlf";
    public bool UseUi { get; set; }
    public ButtonLineMode ButtonLine { get; set; } = ButtonLineMode.Rts;
    public ScaleProtocolKind ScaleProtocol { get; set; } = ScaleProtocolKind.SimpleAscii;
    public long Tare { get; set; }
    public bool BaudRateWasSpecified { get; private set; }
    public bool DataBitsWasSpecified { get; private set; }
    public bool StopBitsWasSpecified { get; private set; }
    public bool ParityWasSpecified { get; private set; }
    public bool NewLineWasSpecified { get; private set; }

    public static ScaleOptions Parse(string[] args)
    {
        var options = new ScaleOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (!arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Argumento invalido: '{arg}'.");
            }

            string key = arg[2..].Trim().ToLowerInvariant();

            if (key == "ui")
            {
                options.UseUi = true;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Falta valor para el argumento '{arg}'.");
            }

            string value = args[++i].Trim();

            switch (key)
            {
                case "port":
                    options.PortName = value;
                    break;

                case "baud":
                case "baudrate":
                    options.BaudRate = ParsePositiveInt(value, "baudrate");
                    options.BaudRateWasSpecified = true;
                    break;

                case "databits":
                    options.DataBits = ParsePositiveInt(value, "databits");
                    options.DataBitsWasSpecified = true;
                    break;

                case "stopbits":
                    options.StopBits = ParsePositiveInt(value, "stopbits");
                    options.StopBitsWasSpecified = true;
                    break;

                case "parity":
                    options.Parity = value;
                    options.ParityWasSpecified = true;
                    break;

                case "interval-ms":
                    options.IntervalMs = ParsePositiveInt(value, "interval-ms");
                    break;

                case "file":
                    options.FilePath = value;
                    break;

                case "newline":
                    options.NewLineMode = value.ToLowerInvariant();
                    options.NewLineWasSpecified = true;
                    break;

                case "button-line":
                    options.ButtonLine = ButtonLineHelper.Parse(value);
                    break;

                case "scale-protocol":
                    options.ScaleProtocol = ScaleProtocolKindHelper.Parse(value);
                    break;

                case "tare":
                    options.Tare = ParseTare(value);
                    break;

                default:
                    throw new ArgumentException($"Argumento no reconocido: '{arg}'.");
            }
        }

        Validate(options);
        return options;
    }

    private static void Validate(ScaleOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PortName))
        {
            throw new ArgumentException("Debe indicar --port COMx.");
        }

        if (options.IntervalMs <= 0)
        {
            throw new ArgumentException("El valor de --interval-ms debe ser mayor que cero.");
        }

        if (!string.IsNullOrWhiteSpace(options.FilePath) && !File.Exists(options.FilePath))
        {
            throw new FileNotFoundException($"No existe el archivo indicado en --file: {options.FilePath}");
        }

        if (options.Tare < 0 || options.Tare > MaxTareValue)
        {
            throw new ArgumentException($"El valor de --tare debe estar entre 0 y {MaxTareValue}.");
        }

        if (options.ScaleProtocol == ScaleProtocolKind.SimpleAscii)
        {
            if (!NewLineHelper.IsSupportedMode(options.NewLineMode))
            {
                throw new ArgumentException("El valor de --newline debe ser 'crlf' o 'lf'.");
            }

            SerialProfile.ParseParity(options.Parity);
            SerialProfile.ParseStopBits(options.StopBits);
        }
    }

    private static int ParsePositiveInt(string value, string argumentName)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException($"El valor de --{argumentName} debe ser un entero mayor que cero.");
        }

        return result;
    }

    private static long ParseTare(string value)
    {
        if (!long.TryParse(value, out long result) || result < 0 || result > MaxTareValue)
        {
            throw new ArgumentException($"El valor de --tare debe estar entre 0 y {MaxTareValue}.");
        }

        return result;
    }

    public static string GetUsage()
    {
        return
@"ScaleSimulator - Simulador local de balanza y pulsador por puerto serie

Uso:
  ScaleSimulator.exe --port COM3 [--scale-protocol simple-ascii|w180-t] [--tare 0] [--interval-ms 1000] [--file weights.txt] [--newline crlf|lf] [--ui] [--button-line rts|dtr]
  dotnet run --project src/ScaleSimulator -- --port COM3 [--scale-protocol simple-ascii|w180-t] [--tare 0] [--interval-ms 1000] [--file weights.txt] [--newline crlf|lf] [--ui] [--button-line rts|dtr]

Parametros:
  --port           Puerto COM del lado simulador. Obligatorio. Ej: COM3
  --scale-protocol Protocolo de balanza: simple-ascii o w180-t. Default: simple-ascii
  --tare           Tara a usar con protocolos que la soportan. Default: 0
  --interval-ms    Intervalo entre envios en milisegundos. Default: 1000
  --file           Archivo con pesos en gramos, un numero por linea. Opcional.
  --newline        crlf o lf. Solo aplica a simple-ascii. Default: crlf
  --ui             Abre la mini UI para disparar el pulsador y controlar la balanza.
  --button-line    rts o dtr. Default: rts

Opcionales avanzados para simple-ascii:
  --baud           Baud rate. Default: 9600
  --databits       Data bits. Default: 8
  --stopbits       Stop bits. Default: 1
  --parity         None, Odd, Even, Mark, Space. Default: None

Perfil efectivo de W180-T:
  9600 baud, 7 data bits, parity Even, 2 stop bits, CRLF fijo, sin handshake

Ejemplos:
  ScaleSimulator.exe --port COM3
  ScaleSimulator.exe --port COM3 --ui --button-line rts
  ScaleSimulator.exe --port COM3 --scale-protocol w180-t --tare 0
  ScaleSimulator.exe --port COM3 --interval-ms 1000 --file .\samples\weights.txt --newline crlf
";
    }
}
