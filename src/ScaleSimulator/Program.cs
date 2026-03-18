using System.Windows.Forms;
using ScaleSimulator;

internal static class Program
{
    private static volatile bool _stopRequested;
    private static Form? _simulatorForm;

    [STAThread]
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

        using var runtime = new ScaleSimulatorRuntime(options, weightSource);
        runtime.LogEmitted += WriteLog;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;

            if (_stopRequested)
            {
                return;
            }

            _stopRequested = true;
            WriteInfo("Se recibio Ctrl+C. Iniciando cierre limpio...");

            if (_simulatorForm is not null && !_simulatorForm.IsDisposed)
            {
                try
                {
                    _simulatorForm.BeginInvoke(new Action(() => _simulatorForm.Close()));
                }
                catch
                {
                    // Se prioriza el cierre limpio del proceso.
                }
            }
        };

        return options.UseUi
            ? RunUi(runtime)
            : RunHeadless(runtime);
    }

    private static int RunHeadless(ScaleSimulatorRuntime runtime)
    {
        int exitCode = TryStartRuntime(runtime);
        if (exitCode != 0)
        {
            return exitCode;
        }

        try
        {
            while (!_stopRequested)
            {
                Thread.Sleep(100);
            }

            return 0;
        }
        finally
        {
            runtime.Stop();
        }
    }

    private static int RunUi(ScaleSimulatorRuntime runtime)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new SimulatorForm(runtime);
        _simulatorForm = form;
        form.FormClosed += (_, _) => _stopRequested = true;

        int exitCode = TryStartRuntime(runtime);
        if (exitCode != 0)
        {
            _simulatorForm = null;
            return exitCode;
        }

        try
        {
            Application.Run(form);
            return 0;
        }
        finally
        {
            _simulatorForm = null;
            runtime.Stop();
        }
    }

    private static int TryStartRuntime(ScaleSimulatorRuntime runtime)
    {
        try
        {
            runtime.Start();
            return 0;
        }
        catch (ArgumentException ex)
        {
            WriteError(ex.Message);
            return 6;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            return 6;
        }
        catch (UnauthorizedAccessException)
        {
            WriteError($"No se pudo abrir el puerto {runtime.PortName}. Posible causa: puerto ocupado o sin permisos.");
            return 4;
        }
        catch (IOException ex)
        {
            WriteError($"Error de I/O al abrir el puerto {runtime.PortName}: {ex.Message}");
            return 5;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 6;
        }
    }

    private static void WriteLog(SimulatorLogEntry entry)
    {
        switch (entry.Level)
        {
            case SimulatorLogLevel.Warning:
                WriteColoredLine(entry.Timestamp, "WARN ", entry.Message, ConsoleColor.Yellow);
                break;

            case SimulatorLogLevel.Error:
                WriteColoredLine(entry.Timestamp, "ERROR", entry.Message, ConsoleColor.Red);
                break;

            case SimulatorLogLevel.Sent:
                Console.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.Message}");
                break;

            default:
                WriteColoredLine(entry.Timestamp, "INFO ", entry.Message);
                break;
        }
    }

    private static void WriteInfo(string message)
    {
        WriteColoredLine(DateTime.Now, "INFO ", message);
    }

    private static void WriteError(string message)
    {
        WriteColoredLine(DateTime.Now, "ERROR", message, ConsoleColor.Red);
    }

    private static void WriteColoredLine(DateTime timestamp, string level, string message, ConsoleColor? color = null)
    {
        var previousColor = Console.ForegroundColor;

        if (color.HasValue)
        {
            Console.ForegroundColor = color.Value;
        }

        Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}");

        if (color.HasValue)
        {
            Console.ForegroundColor = previousColor;
        }
    }
}
