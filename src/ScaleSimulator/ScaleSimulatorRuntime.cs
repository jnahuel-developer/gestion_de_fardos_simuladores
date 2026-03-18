using System.IO.Ports;

namespace ScaleSimulator;

internal sealed class ScaleSimulatorRuntime : IDisposable
{
    private const int ButtonPulseDurationMs = 500;
    private const int MaxLogEntries = 200;

    private readonly object _serialSync = new();
    private readonly object _logSync = new();
    private readonly ScaleOptions _options;
    private readonly WeightSource _weightSource;
    private readonly List<SimulatorLogEntry> _recentLogEntries = new();

    private SerialPort? _serialPort;
    private CancellationTokenSource? _runtimeCts;
    private Task? _weightLoopTask;
    private Task<bool>? _pulseTask;
    private ScaleProtocolKind _selectedScaleProtocol;
    private long _tareValue;
    private int _pulseInProgress;
    private bool _disposed;

    public ScaleSimulatorRuntime(ScaleOptions options, WeightSource weightSource)
    {
        _options = options;
        _weightSource = weightSource;
        _selectedScaleProtocol = options.ScaleProtocol;
        _tareValue = options.Tare;
    }

    public event Action<SimulatorLogEntry>? LogEmitted;
    public event Action<bool>? PulseStateChanged;
    public event Action<bool>? PortStateChanged;

    public string PortName => _options.PortName;
    public ButtonLineMode ButtonLine => _options.ButtonLine;
    public IReadOnlyList<IScaleProtocol> AvailableScaleProtocols => ScaleProtocolCatalog.All;

    public bool IsPortOpen
    {
        get
        {
            lock (_serialSync)
            {
                return IsPortOpenUnsafe();
            }
        }
    }

    public bool IsPulseInProgress => Volatile.Read(ref _pulseInProgress) == 1;

    public ScaleProtocolKind SelectedScaleProtocolKind
    {
        get
        {
            lock (_serialSync)
            {
                return _selectedScaleProtocol;
            }
        }
    }

    public IScaleProtocol SelectedScaleProtocol => ScaleProtocolCatalog.Get(SelectedScaleProtocolKind);

    public long TareValue
    {
        get
        {
            lock (_serialSync)
            {
                return _tareValue;
            }
        }
    }

    public void Start()
    {
        ThrowIfDisposed();

        IScaleProtocol protocol;
        SerialProfile profile;
        SerialPort serialPort;
        CancellationTokenSource cts;

        lock (_serialSync)
        {
            if (IsPortOpenUnsafe())
            {
                throw new InvalidOperationException("La comunicacion de balanza ya esta activa.");
            }

            protocol = ScaleProtocolCatalog.Get(_selectedScaleProtocol);
            protocol.ValidateConfiguration(_weightSource, _tareValue);
            profile = protocol.ResolveSerialSettings(_options);
            serialPort = profile.CreatePort(_options.PortName);
            serialPort.Open();
            _serialPort = serialPort;
            ResetControlLinesUnsafe();

            cts = new CancellationTokenSource();
            _runtimeCts = cts;
            _weightLoopTask = Task.Run(() => RunWeightLoopAsync(cts.Token));
        }

        LogProtocolWarnings(protocol);
        PortStateChanged?.Invoke(true);
        WriteHeader(protocol);
    }

    public void Stop()
    {
        ThrowIfDisposed();

        CancellationTokenSource? cts;
        Task? weightLoopTask;
        Task<bool>? pulseTask;
        SerialPort? portToDispose = null;

        lock (_serialSync)
        {
            if (!IsPortOpenUnsafe() && _runtimeCts is null && _weightLoopTask is null)
            {
                return;
            }

            cts = _runtimeCts;
            weightLoopTask = _weightLoopTask;
            pulseTask = _pulseTask;
            _runtimeCts = null;
            _weightLoopTask = null;
        }

        cts?.Cancel();

        try
        {
            pulseTask?.GetAwaiter().GetResult();
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
            weightLoopTask?.GetAwaiter().GetResult();
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
            if (_serialPort is not null)
            {
                TryResetControlLinesUnsafe();

                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                portToDispose = _serialPort;
                _serialPort = null;
            }
        }

        portToDispose?.Dispose();
        cts?.Dispose();

        PortStateChanged?.Invoke(false);
        LogInfo($"Puerto {PortName} cerrado. Comunicacion detenida.");
    }

    public Task<bool> PulseButtonAsync()
    {
        ThrowIfDisposed();

        CancellationToken cancellationToken;
        string lineName = ButtonLineHelper.Describe(_options.ButtonLine);

        lock (_serialSync)
        {
            if (!IsPortOpenUnsafe() || _runtimeCts is null)
            {
                return Task.FromResult(false);
            }

            cancellationToken = _runtimeCts.Token;
        }

        if (Interlocked.CompareExchange(ref _pulseInProgress, 1, 0) != 0)
        {
            return Task.FromResult(false);
        }

        Task<bool> pulseTask = ExecutePulseAsync(cancellationToken, lineName);

        lock (_serialSync)
        {
            _pulseTask = pulseTask;
        }

        return pulseTask;
    }

    public IReadOnlyList<SimulatorLogEntry> GetRecentLogs()
    {
        lock (_logSync)
        {
            return _recentLogEntries.ToArray();
        }
    }

    public SerialProfile GetEffectiveSerialProfile()
    {
        return SelectedScaleProtocol.ResolveSerialSettings(_options);
    }

    public string GetEffectiveSerialProfileSummary()
    {
        string summary = GetEffectiveSerialProfile().Describe();

        if (SelectedScaleProtocolKind == ScaleProtocolKind.SimpleAscii)
        {
            summary += $" / NewLine {NewLineHelper.Describe(_options.NewLineMode)}";
        }
        else
        {
            summary += " / CRLF fijo";
        }

        return summary;
    }

    public void SetScaleProtocol(ScaleProtocolKind protocolKind)
    {
        ThrowIfDisposed();

        lock (_serialSync)
        {
            if (IsPortOpenUnsafe())
            {
                throw new InvalidOperationException("Debe detener la comunicacion antes de cambiar el protocolo.");
            }

            _selectedScaleProtocol = protocolKind;
        }

        LogInfo($"Balanza: protocolo seleccionado {ScaleProtocolKindHelper.Describe(protocolKind)}.");
    }

    public void SetTare(long tareValue)
    {
        ThrowIfDisposed();

        if (tareValue < 0 || tareValue > ScaleOptions.MaxTareValue)
        {
            throw new ArgumentException(
                $"La tara debe estar entre 0 y {ScaleOptions.MaxTareValue}.",
                nameof(tareValue));
        }

        lock (_serialSync)
        {
            _tareValue = tareValue;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private async Task RunWeightLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ScaleReading reading = _weightSource.Next(TareValue);
            IScaleProtocol protocol = SelectedScaleProtocol;

            try
            {
                WriteReading(protocol, reading);
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
            catch (Exception ex)
            {
                LogError($"Error enviando trama del protocolo {protocol.Id}: {ex.Message}");
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

    private async Task<bool> ExecutePulseAsync(CancellationToken cancellationToken, string lineName)
    {
        bool lineActivated = false;

        PulseStateChanged?.Invoke(true);

        try
        {
            SetSelectedButtonLine(active: true);
            lineActivated = true;
            LogInfo($"Pulsador: inicio de pulso de {ButtonPulseDurationMs} ms sobre {lineName}.");

            try
            {
                await Task.Delay(ButtonPulseDurationMs, cancellationToken);
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

            lock (_serialSync)
            {
                _pulseTask = null;
            }

            Interlocked.Exchange(ref _pulseInProgress, 0);
            PulseStateChanged?.Invoke(false);
        }
    }

    private void WriteReading(IScaleProtocol protocol, ScaleReading reading)
    {
        byte[] payload = protocol.EncodeFrame(reading, _options);

        lock (_serialSync)
        {
            EnsurePortOpen();
            _serialPort!.Write(payload, 0, payload.Length);
        }

        LogSent($"PORT={PortName} PROTOCOL={protocol.Id} {protocol.DescribeFrame(reading)}");
    }

    private void SetSelectedButtonLine(bool active)
    {
        lock (_serialSync)
        {
            EnsurePortOpen();

            _serialPort!.DtrEnable = _options.ButtonLine == ButtonLineMode.Dtr && active;
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
            if (IsPortOpenUnsafe())
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
        _serialPort!.DtrEnable = false;
        _serialPort.RtsEnable = false;
    }

    private void EnsurePortOpen()
    {
        if (!IsPortOpenUnsafe())
        {
            throw new InvalidOperationException($"El puerto {PortName} no esta abierto.");
        }
    }

    private bool IsPortOpenUnsafe()
    {
        return _serialPort?.IsOpen == true;
    }

    private void WriteHeader(IScaleProtocol protocol)
    {
        LogInfo("==============================================");
        LogInfo("ScaleSimulator - Simulador de balanza y pulsador");
        LogInfo("==============================================");
        LogInfo($"Puerto       : {_options.PortName}");
        LogInfo($"Protocolo    : {protocol.DisplayName}");
        LogInfo($"Perfil serie : {GetEffectiveSerialProfileSummary()}");
        LogInfo($"Intervalo    : {_options.IntervalMs} ms");
        LogInfo($"Fuente       : {(string.IsNullOrWhiteSpace(_options.FilePath) ? "lista interna" : _options.FilePath)}");
        LogInfo($"Interfaz     : {(_options.UseUi ? "mini UI" : "consola")}");
        LogInfo($"Pulsador     : linea {ButtonLineHelper.Describe(_options.ButtonLine)}");
        LogInfo($"Pulso        : {ButtonPulseDurationMs} ms");

        if (protocol.SupportsTare)
        {
            LogInfo($"Tara         : {TareValue}");
        }

        LogInfo("Balanza      : transmision continua");
        LogInfo("Pulsador     : sin tramas, solo lineas de control");
        LogInfo("Detencion    : Ctrl+C, Stop o cierre de ventana");
        LogInfo("==============================================");
    }

    private void LogProtocolWarnings(IScaleProtocol protocol)
    {
        if (protocol.Kind != ScaleProtocolKind.W180T)
        {
            return;
        }

        if (_options.BaudRateWasSpecified
            || _options.DataBitsWasSpecified
            || _options.StopBitsWasSpecified
            || _options.ParityWasSpecified
            || _options.NewLineWasSpecified)
        {
            LogWarning(
                "W180-T ignora --baud, --databits, --stopbits, --parity y --newline. El perfil efectivo es 9600 / 7E2 / CRLF fijo.");
        }
    }

    private void LogInfo(string message)
    {
        EmitLog(SimulatorLogLevel.Info, message);
    }

    private void LogWarning(string message)
    {
        EmitLog(SimulatorLogLevel.Warning, message);
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
