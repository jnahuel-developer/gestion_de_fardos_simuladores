using System.IO.Ports;

namespace ScaleSimulator;

internal sealed class ScaleSimulatorRuntime : IDisposable
{
    private const int ButtonPulseDurationMs = 500;
    private const int MaxLogEntries = 200;

    private readonly object _serialSync = new();
    private readonly object _logSync = new();
    private readonly SerialPort _serialPort;
    private readonly ScaleOptions _options;
    private readonly WeightSource _weightSource;
    private readonly string _newLine;
    private readonly CancellationTokenSource _runtimeCts = new();
    private readonly List<SimulatorLogEntry> _recentLogEntries = new();

    private Task? _weightLoopTask;
    private Task<bool>? _pulseTask;
    private bool _started;
    private int _pulseInProgress;

    public ScaleSimulatorRuntime(ScaleOptions options, WeightSource weightSource)
    {
        _options = options;
        _weightSource = weightSource;
        _newLine = NewLineHelper.Resolve(options.NewLineMode);
        _serialPort = new SerialPort
        {
            PortName = options.PortName,
            BaudRate = options.BaudRate,
            DataBits = options.DataBits,
            Parity = ParseParity(options.Parity),
            StopBits = ParseStopBits(options.StopBits),
            Handshake = Handshake.None,
            Encoding = System.Text.Encoding.ASCII,
            NewLine = _newLine,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 500,
            WriteTimeout = 2000
        };
    }

    public event Action<SimulatorLogEntry>? LogEmitted;
    public event Action<bool>? PulseStateChanged;
    public event Action<bool>? PortStateChanged;

    public string PortName => _options.PortName;
    public ButtonLineMode ButtonLine => _options.ButtonLine;

    public bool IsPortOpen
    {
        get
        {
            lock (_serialSync)
            {
                return _serialPort.IsOpen;
            }
        }
    }

    public bool IsPulseInProgress => Volatile.Read(ref _pulseInProgress) == 1;

    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException("El simulador ya esta iniciado.");
        }

        lock (_serialSync)
        {
            _serialPort.Open();
            ResetControlLinesUnsafe();
        }

        _started = true;
        PortStateChanged?.Invoke(true);
        WriteHeader();
        _weightLoopTask = Task.Run(() => RunWeightLoopAsync(_runtimeCts.Token));
    }

    public Task<bool> PulseButtonAsync()
    {
        if (!_started || _runtimeCts.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        if (Interlocked.CompareExchange(ref _pulseInProgress, 1, 0) != 0)
        {
            return Task.FromResult(false);
        }

        var pulseTask = ExecutePulseAsync();
        _pulseTask = pulseTask;
        return pulseTask;
    }

    public IReadOnlyList<SimulatorLogEntry> GetRecentLogs()
    {
        lock (_logSync)
        {
            return _recentLogEntries.ToArray();
        }
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _runtimeCts.Cancel();

        try
        {
            _pulseTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Cancelacion esperada durante el cierre.
        }
        catch (Exception ex)
        {
            LogError($"Error finalizando el pulso del pulsador: {ex.Message}");
        }

        try
        {
            _weightLoopTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Cancelacion esperada durante el cierre.
        }
        catch (Exception ex)
        {
            LogError($"Error finalizando el loop de balanza: {ex.Message}");
        }

        lock (_serialSync)
        {
            TryResetControlLinesUnsafe();

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        _started = false;
        PortStateChanged?.Invoke(false);
        LogInfo($"Puerto {PortName} cerrado. Fin del simulador.");
    }

    public void Dispose()
    {
        Stop();
        _serialPort.Dispose();
        _runtimeCts.Dispose();
    }

    private async Task RunWeightLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string weight = _weightSource.Next();

            try
            {
                WriteWeight(weight);
            }
            catch (TimeoutException ex)
            {
                LogError($"Timeout escribiendo en {PortName}: {ex.Message}");
            }
            catch (IOException ex)
            {
                LogError($"Error de I/O escribiendo en {PortName}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                LogError($"Puerto no disponible al escribir en {PortName}: {ex.Message}");
            }

            try
            {
                await Task.Delay(_options.IntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> ExecutePulseAsync()
    {
        string lineName = ButtonLineHelper.Describe(_options.ButtonLine);
        bool lineActivated = false;

        PulseStateChanged?.Invoke(true);

        try
        {
            SetSelectedButtonLine(active: true);
            lineActivated = true;
            LogInfo($"Pulsador: inicio de pulso de {ButtonPulseDurationMs} ms sobre {lineName}.");

            try
            {
                await Task.Delay(ButtonPulseDurationMs, _runtimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancelacion esperada si el simulador cierra durante el pulso.
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"No se pudo iniciar el pulso sobre {lineName}: {ex.Message}");
            throw;
        }
        finally
        {
            if (lineActivated)
            {
                TryResetControlLines();
                LogInfo($"Pulsador: fin de pulso sobre {lineName}.");
            }

            _pulseTask = null;
            Interlocked.Exchange(ref _pulseInProgress, 0);
            PulseStateChanged?.Invoke(false);
        }
    }

    private void WriteWeight(string weight)
    {
        string payload = weight + _newLine;

        lock (_serialSync)
        {
            EnsurePortOpen();
            _serialPort.Write(payload);
        }

        LogSent($"PORT={PortName} SENT={weight}");
    }

    private void SetSelectedButtonLine(bool active)
    {
        lock (_serialSync)
        {
            EnsurePortOpen();

            _serialPort.DtrEnable = _options.ButtonLine == ButtonLineMode.Dtr && active;
            _serialPort.RtsEnable = _options.ButtonLine == ButtonLineMode.Rts && active;
        }
    }

    private void TryResetControlLines()
    {
        lock (_serialSync)
        {
            TryResetControlLinesUnsafe();
        }
    }

    private void TryResetControlLinesUnsafe()
    {
        try
        {
            if (_serialPort.IsOpen)
            {
                ResetControlLinesUnsafe();
            }
        }
        catch (Exception ex)
        {
            LogError($"No se pudieron restaurar las lineas de control en {PortName}: {ex.Message}");
        }
    }

    private void ResetControlLinesUnsafe()
    {
        _serialPort.DtrEnable = false;
        _serialPort.RtsEnable = false;
    }

    private void EnsurePortOpen()
    {
        if (!_serialPort.IsOpen)
        {
            throw new InvalidOperationException($"El puerto {PortName} no esta abierto.");
        }
    }

    private void WriteHeader()
    {
        LogInfo("==============================================");
        LogInfo("ScaleSimulator - Simulador de balanza y pulsador");
        LogInfo("==============================================");
        LogInfo($"Puerto       : {_options.PortName}");
        LogInfo($"BaudRate     : {_options.BaudRate}");
        LogInfo($"DataBits     : {_options.DataBits}");
        LogInfo($"Parity       : {_options.Parity}");
        LogInfo($"StopBits     : {_options.StopBits}");
        LogInfo($"Intervalo    : {_options.IntervalMs} ms");
        LogInfo($"NewLine      : {NewLineHelper.Describe(_options.NewLineMode)}");
        LogInfo($"Fuente       : {(string.IsNullOrWhiteSpace(_options.FilePath) ? "lista interna" : _options.FilePath)}");
        LogInfo($"Interfaz     : {(_options.UseUi ? "mini UI" : "consola")}");
        LogInfo($"Pulsador     : linea {ButtonLineHelper.Describe(_options.ButtonLine)}");
        LogInfo($"Pulso        : {ButtonPulseDurationMs} ms");
        LogInfo("Balanza      : gramos ASCII en loop");
        LogInfo("Pulsador     : sin tramas, solo lineas de control");
        LogInfo("Detencion    : Ctrl+C o cierre de ventana");
        LogInfo("==============================================");
    }

    private void LogInfo(string message)
    {
        EmitLog(SimulatorLogLevel.Info, message);
    }

    private void LogError(string message)
    {
        EmitLog(SimulatorLogLevel.Error, message);
    }

    private void LogSent(string message)
    {
        EmitLog(SimulatorLogLevel.Sent, message);
    }

    private void EmitLog(SimulatorLogLevel level, string message)
    {
        var entry = new SimulatorLogEntry(DateTime.Now, level, message);

        lock (_logSync)
        {
            _recentLogEntries.Add(entry);

            if (_recentLogEntries.Count > MaxLogEntries)
            {
                _recentLogEntries.RemoveAt(0);
            }
        }

        LogEmitted?.Invoke(entry);
    }

    private static Parity ParseParity(string value)
    {
        if (Enum.TryParse<Parity>(value, ignoreCase: true, out var parity))
        {
            return parity;
        }

        throw new ArgumentException(
            $"Valor de paridad invalido: '{value}'. Valores soportados: None, Odd, Even, Mark, Space.");
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
}
