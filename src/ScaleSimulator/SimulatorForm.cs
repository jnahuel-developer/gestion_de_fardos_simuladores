using System.Drawing;
using System.Windows.Forms;

namespace ScaleSimulator;

internal sealed class SimulatorForm : Form
{
    private readonly ScaleSimulatorRuntime _runtime;
    private readonly Label _portStatusValue;
    private readonly Label _buttonLineValue;
    private readonly Label _pulseStatusValue;
    private readonly Button _pulseButton;
    private readonly ListBox _eventList;

    public SimulatorForm(ScaleSimulatorRuntime runtime)
    {
        _runtime = runtime;

        Text = "ScaleSimulator - Pulsador";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 460);
        Size = new Size(760, 520);

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
        _buttonLineValue = AddRow(summaryPanel, 2, "Linea del pulsador", ButtonLineHelper.Describe(_runtime.ButtonLine));
        _pulseStatusValue = AddRow(summaryPanel, 3, "Estado del pulsador", "Reposo");
        _pulseStatusValue.Font = new Font(_pulseStatusValue.Font, FontStyle.Bold);

        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.Controls.Add(actionPanel, 0, 1);

        _pulseButton = new Button
        {
            Text = "Pulsar 500 ms",
            Dock = DockStyle.Top,
            Height = 44,
            AutoSize = false,
            BackColor = Color.FromArgb(233, 247, 239),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 8)
        };
        _pulseButton.Click += HandlePulseButtonClick;
        actionPanel.Controls.Add(_pulseButton, 0, 0);

        var hintLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "El boton activa solo la linea configurada durante 500 ms. La balanza sigue enviando pesos por datos."
        };
        actionPanel.Controls.Add(hintLabel, 0, 1);

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

        _portStatusValue.Text = isPortOpen ? "Abierto" : "Cerrado";
        _portStatusValue.ForeColor = isPortOpen ? Color.DarkGreen : Color.DarkRed;
        _buttonLineValue.Text = ButtonLineHelper.Describe(_runtime.ButtonLine);

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
