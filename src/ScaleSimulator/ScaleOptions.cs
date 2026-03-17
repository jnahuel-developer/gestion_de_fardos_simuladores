namespace ScaleSimulator;

internal sealed class ScaleOptions
{
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public int StopBits { get; set; } = 1;
    public string Parity { get; set; } = "None";

    public int IntervalMs { get; set; } = 1000;
    public string? FilePath { get; set; }
    public string NewLineMode { get; set; } = "crlf";

    public static ScaleOptions Parse(string[] args)
    {
        var options = new ScaleOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (!arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Argumento inválido: '{arg}'.");
            }

            string key = arg[2..].Trim().ToLowerInvariant();

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
                    break;

                case "databits":
                    options.DataBits = ParsePositiveInt(value, "databits");
                    break;

                case "stopbits":
                    options.StopBits = ParsePositiveInt(value, "stopbits");
                    break;

                case "parity":
                    options.Parity = value;
                    break;

                case "interval-ms":
                    options.IntervalMs = ParsePositiveInt(value, "interval-ms");
                    break;

                case "file":
                    options.FilePath = value;
                    break;

                case "newline":
                    options.NewLineMode = value.ToLowerInvariant();
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

        if (!NewLineHelper.IsSupportedMode(options.NewLineMode))
        {
            throw new ArgumentException("El valor de --newline debe ser 'crlf' o 'lf'.");
        }

        if (!string.IsNullOrWhiteSpace(options.FilePath) && !File.Exists(options.FilePath))
        {
            throw new FileNotFoundException($"No existe el archivo indicado en --file: {options.FilePath}");
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

    public static string GetUsage()
    {
        return
@"ScaleSimulator - Simulador local de balanza por puerto serie

Uso:
  ScaleSimulator.exe --port COM11 [--interval-ms 1000] [--file weights.txt] [--newline crlf|lf]
  dotnet run --project src/ScaleSimulator -- --port COM11 [--interval-ms 1000] [--file weights.txt] [--newline crlf|lf]

Parámetros:
  --port         Puerto COM del lado simulador. Obligatorio. Ej: COM11
  --interval-ms  Intervalo entre envíos en milisegundos. Default: 1000
  --file         Archivo con pesos en gramos, un número por línea. Opcional.
  --newline      crlf o lf. Default: crlf

Opcionales avanzados:
  --baud         Baud rate. Default: 9600
  --databits     Data bits. Default: 8
  --stopbits     Stop bits. Default: 1
  --parity       None, Odd, Even, Mark, Space. Default: None

Ejemplos:
  ScaleSimulator.exe --port COM11
  ScaleSimulator.exe --port COM11 --interval-ms 1000 --file .\samples\weights.txt --newline crlf
";
    }
}