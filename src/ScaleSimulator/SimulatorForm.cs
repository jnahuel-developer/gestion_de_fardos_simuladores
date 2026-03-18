using System.Drawing;
using System.Windows.Forms;

namespace ScaleSimulator;

internal sealed class SimulatorForm : Form
{
    private readonly ScaleSimulatorRuntime _runtime;
    private readonly Label _portStatusValue;
    private readonly Label _protocolValue;
    private readonly Label _serialProfileValue;
    private readonly Label _buttonLineValue;
    private readonly Label _pulseStatusValue;
    private readonly ComboBox _protocolComboBox;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly NumericUpDown _tareInput;
    private readonly Label _tareHintLabel;
    private readonly Button _pulseButton;
    private readonly ListBox _eventList;
    private bool _isRefreshingProtocolSelection;

    public SimulatorForm(ScaleSimulatorRuntime runtime)
    {
        _runtime = runtime;

        Text = "ScaleSimulator - Balanza y pulsador";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 600);
        Size = new Size(920, 660);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        var summaryPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
        summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.Controls.Add(summaryPanel, 0, 0);

        AddRow(summaryPanel, 0, "Puerto", _runtime.PortName);
        _portStatusValue = AddRow(summaryPanel, 1, "Estado del puerto", "Cerrado");
        _protocolValue = AddRow(summaryPanel, 2, "Protocolo activo", _runtime.SelectedScaleProtocol.DisplayName);
        _serialProfileValue = AddRow(summaryPanel, 3, "Perfil serie", _runtime.GetEffectiveSerialProfileSummary());
        _buttonLineValue = AddRow(summaryPanel, 4, "Linea del pulsador", ButtonLineHelper.Describe(_runtime.ButtonLine));
        _pulseStatusValue = AddRow(summaryPanel, 5, "Estado del pulsador", "Reposo");
        _pulseStatusValue.Font = new Font(_pulseStatusValue.Font, FontStyle.Bold);

        summaryPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var communicationLabel = new Label
        {
            Text = "Comunicacion",
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 8),
            Font = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size, FontStyle.Bold)
        };
        summaryPanel.Controls.Add(communicationLabel, 0, 6);

        var communicationButtonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        summaryPanel.Controls.Add(communicationButtonsPanel, 1, 6);

        _startButton = new Button
        {
            Text = "Iniciar balanza",
            AutoSize = false,
            Width = 140,
            Height = 38,
            BackColor = Color.FromArgb(233, 247, 239),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 0)
        };
        _startButton.Click += HandleStartButtonClick;
        communicationButtonsPanel.Controls.Add(_startButton);

        _stopButton = new Button
        {
            Text = "Detener balanza",
            AutoSize = false,
            Width = 140,
            Height = 38,
            BackColor = Color.FromArgb(252, 235, 233),
            FlatStyle = FlatStyle.Flat
        };
        _stopButton.Click += HandleStopButtonClick;
        communicationButtonsPanel.Controls.Add(_stopButton);

        var actionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.Controls.Add(actionsPanel, 0, 1);

        var scaleGroup = new GroupBox
        {
            Text = "Balanza",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 8, 0)
        };
        actionsPanel.Controls.Add(scaleGroup, 0, 0);

        var scaleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = true
        };
        scaleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160f));
        scaleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        scaleGroup.Controls.Add(scaleLayout);

        AddFixedLabel(scaleLayout, 0, "Protocolo");
        _protocolComboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (IScaleProtocol protocol in _runtime.AvailableScaleProtocols)
        {
            _protocolComboBox.Items.Add(protocol);
        }

        _protocolComboBox.SelectedIndexChanged += HandleProtocolSelectionChanged;
        scaleLayout.Controls.Add(_protocolComboBox, 1, 0);

        AddFixedLabel(scaleLayout, 1, "Tara");
        _tareInput = new NumericUpDown
        {
            Dock = DockStyle.Top,
            Minimum = 0,
            Maximum = ScaleOptions.MaxTareValue,
            ThousandsSeparator = true
        };
        _tareInput.ValueChanged += HandleTareValueChanged;
        scaleLayout.Controls.Add(_tareInput, 1, 1);

        _tareHintLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 8)
        };
        scaleLayout.Controls.Add(_tareHintLabel, 1, 2);

        var scaleHintLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Text = "La balanza arranca activa. Para cambiar de protocolo, presiona Detener balanza, cambia la seleccion y luego Iniciar balanza."
        };
        scaleLayout.Controls.Add(scaleHintLabel, 1, 3);

        var buttonGroup = new GroupBox
        {
            Text = "Pulsador",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(8, 0, 0, 0)
        };
        actionsPanel.Controls.Add(buttonGroup, 1, 0);

        var buttonLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        buttonGroup.Controls.Add(buttonLayout);

        _pulseButton = new Button
        {
            Text = "Pulsar 500 ms",
            AutoSize = false,
            Width = 180,
            Height = 38,
            BackColor = Color.FromArgb(233, 247, 239),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 8)
        };
        _pulseButton.Click += HandlePulseButtonClick;
        buttonLayout.Controls.Add(_pulseButton);

        var buttonHintLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Text = "El boton activa solo la linea configurada durante 500 ms. Si la balanza esta detenida, el pulsador tambien queda inactivo."
        };
        buttonLayout.Controls.Add(buttonHintLabel);

        var eventsGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Ultimos eventos",
            Padding = new Padding(12)
        };
        root.Controls.Add(eventsGroup, 0, 2);

        _eventList = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true
        };
        eventsGroup.Controls.Add(_eventList);

        _runtime.LogEmitted += HandleLogEntry;
        _runtime.PulseStateChanged += HandlePulseStateChanged;
        _runtime.PortStateChanged += HandlePortStateChanged;

        foreach (var entry in _runtime.GetRecentLogs())
        {
            AddLogEntry(entry);
        }

        RefreshState();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _runtime.LogEmitted -= HandleLogEntry;
        _runtime.PulseStateChanged -= HandlePulseStateChanged;
        _runtime.PortStateChanged -= HandlePortStateChanged;
        base.OnFormClosed(e);
    }

    private async void HandlePulseButtonClick(object? sender, EventArgs e)
    {
        _pulseButton.Enabled = false;

        try
        {
            bool started = await _runtime.PulseButtonAsync();

            if (!started)
            {
                RefreshState();
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                MessageBox.Show(
                    this,
                    $"No se pudo ejecutar el pulso del pulsador: {ex.Message}",
                    "ScaleSimulator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            RefreshState();
        }
    }

    private void HandleProtocolSelectionChanged(object? sender, EventArgs e)
    {
        if (_isRefreshingProtocolSelection || _protocolComboBox.SelectedItem is not IScaleProtocol protocol)
        {
            return;
        }

        try
        {
            _runtime.SetScaleProtocol(protocol.Kind);
            RefreshState();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "ScaleSimulator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            RefreshState();
        }
    }

    private void HandleTareValueChanged(object? sender, EventArgs e)
    {
        try
        {
            _runtime.SetTare((long)_tareInput.Value);
            RefreshState();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "ScaleSimulator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            RefreshState();
        }
    }

    private void HandleStartButtonClick(object? sender, EventArgs e)
    {
        try
        {
            _runtime.Start();
            RefreshState();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                MessageBox.Show(
                    this,
                    $"No se pudo iniciar la comunicacion de balanza: {ex.Message}",
                    "ScaleSimulator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            RefreshState();
        }
    }

    private void HandleStopButtonClick(object? sender, EventArgs e)
    {
        try
        {
            _runtime.Stop();
        }
        finally
        {
            RefreshState();
        }
    }

    private void HandleLogEntry(SimulatorLogEntry entry)
    {
        RunOnUi(() => AddLogEntry(entry));
    }

    private void HandlePulseStateChanged(bool isInProgress)
    {
        RunOnUi(() => SetPulseState(isInProgress));
    }

    private void HandlePortStateChanged(bool _)
    {
        RunOnUi(RefreshState);
    }

    private void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        bool isPortOpen = _runtime.IsPortOpen;
        bool isPulseInProgress = _runtime.IsPulseInProgress;
        IScaleProtocol selectedProtocol = _runtime.SelectedScaleProtocol;
        bool supportsTare = selectedProtocol.SupportsTare;

        _portStatusValue.Text = isPortOpen ? "Abierto" : "Cerrado";
        _portStatusValue.ForeColor = isPortOpen ? Color.DarkGreen : Color.DarkRed;
        _protocolValue.Text = selectedProtocol.DisplayName;
        _serialProfileValue.Text = _runtime.GetEffectiveSerialProfileSummary();
        _buttonLineValue.Text = ButtonLineHelper.Describe(_runtime.ButtonLine);

        _isRefreshingProtocolSelection = true;
        try
        {
            for (int index = 0; index < _protocolComboBox.Items.Count; index++)
            {
                if (_protocolComboBox.Items[index] is IScaleProtocol protocol && protocol.Kind == selectedProtocol.Kind)
                {
                    _protocolComboBox.SelectedIndex = index;
                    break;
                }
            }
        }
        finally
        {
            _isRefreshingProtocolSelection = false;
        }

        long tareValue = _runtime.TareValue;
        if ((long)_tareInput.Value != tareValue)
        {
            _tareInput.Value = tareValue;
        }

        _protocolComboBox.Enabled = !isPortOpen;
        _startButton.Enabled = !isPortOpen;
        _stopButton.Enabled = isPortOpen;

        _tareInput.Enabled = supportsTare;
        _tareHintLabel.Text = supportsTare
            ? "Tara aplicada a la siguiente trama del protocolo activo."
            : "La tara no aplica al protocolo seleccionado.";
        _tareHintLabel.ForeColor = supportsTare ? SystemColors.ControlText : Color.DimGray;

        SetPulseState(isPulseInProgress);
        _pulseButton.Enabled = isPortOpen && !isPulseInProgress;
    }

    private void SetPulseState(bool isInProgress)
    {
        if (IsDisposed)
        {
            return;
        }

        _pulseStatusValue.Text = isInProgress ? "Pulso en curso" : "Reposo";
        _pulseStatusValue.ForeColor = isInProgress ? Color.DarkOrange : Color.DarkGreen;
        _pulseButton.Enabled = _runtime.IsPortOpen && !isInProgress;
    }

    private void AddLogEntry(SimulatorLogEntry entry)
    {
        _eventList.Items.Add(entry.ToDisplayLine());

        while (_eventList.Items.Count > 200)
        {
            _eventList.Items.RemoveAt(0);
        }

        _eventList.TopIndex = _eventList.Items.Count - 1;
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    private static void AddFixedLabel(TableLayoutPanel panel, int row, string text)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 8),
            Font = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size, FontStyle.Bold)
        };

        panel.Controls.Add(titleLabel, 0, row);
    }

    private static Label AddRow(TableLayoutPanel panel, int row, string title, string value)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Margin = new Padding(0, 0, 12, 8),
            Font = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size, FontStyle.Bold)
        };

        var valueLabel = new Label
        {
            Text = value,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        panel.Controls.Add(titleLabel, 0, row);
        panel.Controls.Add(valueLabel, 1, row);

        return valueLabel;
    }
}
