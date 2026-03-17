using System.IO.Ports;
using ScaleSimulator;

internal static class Program
{
    private static volatile bool _stopRequested;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        ScaleOptions options;

        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine(ScaleOptions.GetUsage());
                return 1;
            }

            options = ScaleOptions.Parse(args);
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            Console.WriteLine();
            Console.WriteLine(ScaleOptions.GetUsage());
            return 2;
        }

        string newLine = NewLineHelper.Resolve(options.NewLineMode);
        WeightSource weightSource;

        try
        {
            weightSource = WeightSource.Create(options.FilePath);
        }
        catch (Exception ex)
        {
            WriteError($"No se pudo inicializar la fuente de pesos: {ex.Message}");
            return 3;
        }

        using var serialPort = new SerialPort
        {
            PortName = options.PortName,
            BaudRate = options.BaudRate,
            DataBits = options.DataBits,
            Parity = ParseParity(options.Parity),
            StopBits = ParseStopBits(options.StopBits),
            Handshake = Handshake.None,
            Encoding = System.Text.Encoding.ASCII,
            NewLine = newLine,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 500,
            WriteTimeout = 2000
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _stopRequested = true;
            WriteInfo("Se recibió Ctrl+C. Iniciando cierre limpio...");
        };

        try
        {
            serialPort.Open();
        }
        catch (UnauthorizedAccessException)
        {
            WriteError($"No se pudo abrir el puerto {options.PortName}. Posible causa: puerto ocupado o sin permisos.");
            return 4;
        }
        catch (IOException ex)
        {
            WriteError($"Error de I/O al abrir el puerto {options.PortName}: {ex.Message}");
            return 5;
        }
        catch (Exception ex)
        {
            WriteError($"Error inesperado al abrir el puerto {options.PortName}: {ex.Message}");
            return 6;
        }

        WriteHeader(options);

        try
        {
            while (!_stopRequested)
            {
                string weight = weightSource.Next();
                string payload = weight + newLine;

                try
                {
                    serialPort.Write(payload);
                    WriteSent(options.PortName, weight);
                }
                catch (TimeoutException ex)
                {
                    WriteError($"Timeout escribiendo en {options.PortName}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    WriteError($"Error de I/O escribiendo en {options.PortName}: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    WriteError($"Puerto no disponible al escribir en {options.PortName}: {ex.Message}");
                }

                SleepRespectingStop(options.IntervalMs);
            }
        }
        finally
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            catch
            {
                // No se vuelve a lanzar para priorizar salida limpia.
            }

            WriteInfo($"Puerto {options.PortName} cerrado. Fin del simulador.");
        }

        return 0;
    }

    private static void SleepRespectingStop(int totalMs)
    {
        const int sliceMs = 100;

        int remaining = totalMs;
        while (!_stopRequested && remaining > 0)
        {
            int currentSlice = Math.Min(sliceMs, remaining);
            Thread.Sleep(currentSlice);
            remaining -= currentSlice;
        }
    }

    private static Parity ParseParity(string value)
    {
        if (Enum.TryParse<Parity>(value, ignoreCase: true, out var parity))
        {
            return parity;
        }

        throw new ArgumentException(
            $"Valor de paridad inválido: '{value}'. Valores soportados: None, Odd, Even, Mark, Space.");
    }

    private static StopBits ParseStopBits(int value)
    {
        return value switch
        {
            1 => StopBits.One,
            2 => StopBits.Two,
            _ => throw new ArgumentException("El valor de --stopbits debe ser 1 o 2.")
        };
    }

    private static void WriteHeader(ScaleOptions options)
    {
        WriteInfo("==============================================");
        WriteInfo("ScaleSimulator - Simulador local de balanza");
        WriteInfo("==============================================");
        WriteInfo($"Puerto       : {options.PortName}");
        WriteInfo($"BaudRate     : {options.BaudRate}");
        WriteInfo($"DataBits     : {options.DataBits}");
        WriteInfo($"Parity       : {options.Parity}");
        WriteInfo($"StopBits     : {options.StopBits}");
        WriteInfo($"Intervalo    : {options.IntervalMs} ms");
        WriteInfo($"NewLine      : {NewLineHelper.Describe(options.NewLineMode)}");
        WriteInfo($"Fuente       : {(string.IsNullOrWhiteSpace(options.FilePath) ? "lista interna" : options.FilePath)}");
        WriteInfo("Acción       : enviando gramos ASCII en loop");
        WriteInfo("Detención    : Ctrl+C");
        WriteInfo("==============================================");
    }

    private static void WriteSent(string portName, string weight)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] PORT={portName} SENT={weight}");
    }

    private static void WriteInfo(string message)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO  {message}");
    }

    private static void WriteError(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR {message}");
        Console.ForegroundColor = previousColor;
    }
}