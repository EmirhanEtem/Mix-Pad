using System.Drawing;
using System.Drawing.Drawing2D;
using TouchpadGestureControl.Gesture;
using TouchpadGestureControl.Input;
using static TouchpadGestureControl.Gesture.RotationCalculator;

namespace TouchpadGestureControl.UI;

/// <summary>
/// Live diagnostic window with a real-time visual touch canvas and live sensitivity controls.
/// </summary>
public sealed class DiagnosticForm : Form
{
    // ─────────────────────────────────────────────────────────────────────────
    // Controls
    // ─────────────────────────────────────────────────────────────────────────

    private readonly Label _lblProvider     = new();
    private readonly Label _lblFingerCount  = new();
    private readonly Label _lblGestureState = new();
    private readonly Label _lblVolume       = new();
    private readonly Label _lblRawDelta     = new();
    private readonly Label _lblSmoothed     = new();
    private readonly Label _lblAccumulated  = new();
    private readonly Label _lblCentroid     = new();
    private readonly Label _lblAngles       = new();
    private readonly Label _lblArea         = new();
    private readonly Label _lblSettings     = new();
    private readonly RichTextBox _logBox     = new();
    private readonly Panel _headerPanel     = new();
    private readonly Panel _leftPanel       = new();
    private readonly TouchpadCanvas _canvas = new();

    // Live Tuning Controls
    private readonly Label _lblStepSizeValue = new();
    private readonly TrackBar _tbVolumeStep = new();
    private readonly Label _lblDegPerStepValue = new();
    private readonly TrackBar _tbDegPerStep = new();
    private readonly Label _lblThresholdValue = new();
    private readonly TrackBar _tbThreshold = new();
    private readonly Button _btnToggleCw = new();

    // Simulation buttons
    private readonly Button _btnSimCw    = new();
    private readonly Button _btnSimCcw   = new();
    private readonly Button _btnSimSwipe = new();
    private readonly Button _btnSimNoise = new();
    private readonly System.Windows.Forms.Timer _simTimer = new();

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private readonly Queue<string> _logLines = new();
    private int _frameCount;
    private long _lastFrameMs;
    private DiagnosticSnapshot? _latestSnapshot;

    // Simulation state
    private int _simStep;
    private int _simMode; // 1 = CW, 2 = CCW, 3 = Swipe, 4 = Noise
    private double _simAngle;
    private double _simOffsetX;

    /// <summary>Action to inject a simulated TouchFrame into the gesture detector.</summary>
    public Action<TouchFrame>? OnSimulatedFrame { get; set; }

    public DiagnosticForm()
    {
        InitializeLayout();
        this.Text = "TouchpadGestureControl — Diagnostic & Tuning Dashboard";
        this.Size = new Size(1060, 760);
        this.MinimumSize = new Size(850, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(14, 15, 20);
        this.ForeColor = Color.FromArgb(220, 220, 240);
        this.Font = new Font("Segoe UI", 9f);
        this.DoubleBuffered = true;

        _simTimer.Interval = 25; // 40 FPS simulation
        _simTimer.Tick += SimTimer_Tick;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void SetProviderInfo(string providerName, List<string> hidDevices)
    {
        if (IsDisposed || !IsHandleCreated) return;
        this.BeginInvoke(() =>
        {
            _lblProvider.Text = $"Input Provider: {providerName}";
            AppendLog("── Enumerated HID Devices ──────────────────────────────────");
            foreach (var d in hidDevices)
                AppendLog("  " + d);
            AppendLog("─────────────────────────────────────────────────────────────");
        });
    }

    public void UpdateSnapshot(DiagnosticSnapshot snap)
    {
        if (IsDisposed || !IsHandleCreated) return;
        this.BeginInvoke(() => ApplySnapshot(snap));
    }

    public void AppendLog(string message)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (this.InvokeRequired)
            this.BeginInvoke(() => AppendLogInternal(message));
        else
            AppendLogInternal(message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal update
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplySnapshot(DiagnosticSnapshot snap)
    {
        _latestSnapshot = snap;
        _frameCount++;
        long now = Environment.TickCount64;
        long elapsed = now - _lastFrameMs;
        _lastFrameMs = now;

        _lblFingerCount.Text = $"Finger Count: {snap.FingerCount}   (Frame #{_frameCount} | {elapsed}ms)";

        var stateColor = snap.StateAfter switch
        {
            GestureState.Idle           => Color.FromArgb(140, 140, 160),
            GestureState.Tracking       => Color.FromArgb(0, 200, 255),
            GestureState.AdjustingVolume => Color.FromArgb(50, 255, 120),
            _                           => Color.White
        };
        _lblGestureState.ForeColor = stateColor;
        _lblGestureState.Text = $"State: {snap.StateAfter.ToString().ToUpper()} " +
                                (snap.ResetReason != null ? $"({snap.ResetReason})" : "");

        float vol = snap.CurrentVolume;
        string volBar = vol >= 0 ? BuildBar(vol, 16) : "N/A";
        _lblVolume.Text = vol >= 0
            ? $"Volume: {vol * 100:F0}% {volBar} {(snap.VolumeApplied != 0 ? $"[Δ{snap.VolumeApplied * 100:+0.0;-0.0}%]" : "")}"
            : "Volume: Core Audio unavailable";

        _lblRawDelta.Text    = $"Raw Δθ:        {snap.RawDeltaDegrees,7:F2}°";
        _lblSmoothed.Text    = $"Smoothed Δθ:   {snap.SmoothedDeltaDegrees,7:F2}°";
        _lblAccumulated.Text = $"Accumulated:   {snap.AccumulatedDegrees,7:F1}° / {Settings.RotationThresholdDegrees:F0}°";
        _lblCentroid.Text    = $"Centroid:      ({snap.Centroid.X:F0}, {snap.Centroid.Y:F0})";

        if (snap.Frame != null && snap.Frame.Points.Count >= 3)
        {
            var p = snap.Frame.Points;
            double area = Math.Abs(SignedTriangleArea(p[0], p[1], p[2]));
            _lblArea.Text = $"Polygon Area:  {area:N0} px²";
        }
        else
        {
            _lblArea.Text = "Polygon Area:  -";
        }

        if (snap.NewAngles != null && snap.NewAngles.Count > 0)
        {
            var parts = snap.NewAngles.Select(kv =>
                $"  P#{kv.Key}: {ToDegrees(kv.Value),6:F1}°");
            _lblAngles.Text = "Angles:\n" + string.Join("\n", parts);
        }
        else
        {
            _lblAngles.Text = "Angles: (no gesture active)";
        }

        if (snap.VolumeApplied != 0)
        {
            string dir = snap.VolumeApplied > 0 ? "VOLUME INCREASE" : "VOLUME DECREASE";
            AppendLogInternal($"[AUDIO] {dir} {snap.VolumeApplied * 100:+0.0;-0.0}% -> {snap.CurrentVolume * 100:F0}%");
        }

        if (snap.ResetReason != null && snap.StateAfter == GestureState.Idle && snap.StateBefore != GestureState.Idle)
            AppendLogInternal($"[STATE] Completed: {snap.ResetReason}");

        if (snap.StateBefore == GestureState.Idle && snap.StateAfter == GestureState.Tracking)
            AppendLogInternal($"[STATE] 3 contacts detected — tracking active.");

        _canvas.UpdateFrame(snap);
    }

    private static string BuildBar(float value, int width)
    {
        int filled = (int)Math.Clamp(value * width, 0, width);
        return "[" + new string('█', filled) + new string('░', width - filled) + "]";
    }

    private void AppendLogInternal(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _logLines.Enqueue(line);
        while (_logLines.Count > Settings.DiagnosticMaxLogLines)
            _logLines.Dequeue();

        _logBox.SuspendLayout();
        _logBox.Text = string.Join(Environment.NewLine, _logLines);
        _logBox.SelectionStart = _logBox.Text.Length;
        _logBox.ScrollToCaret();
        _logBox.ResumeLayout();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Synthetic Simulation Engine
    // ─────────────────────────────────────────────────────────────────────────

    private void StartSimulation(int mode)
    {
        _simMode = mode;
        _simStep = 0;
        _simAngle = 0;
        _simOffsetX = 0;
        _simTimer.Start();

        string name = mode switch
        {
            1 => "Clockwise Rotation (Volume UP)",
            2 => "Counter-Clockwise Rotation (Volume DOWN)",
            3 => "Translational 3-Finger Swipe",
            4 => "Jitter Noise Test",
            _ => "Simulation"
        };
        AppendLogInternal($"[SIMULATION STARTED] {name}");
    }

    private void SimTimer_Tick(object? sender, EventArgs e)
    {
        _simStep++;
        long now = Environment.TickCount64;

        double cx = 960 + _simOffsetX;
        double cy = 540;
        double r = 240;

        if (_simMode == 1) // CW
        {
            _simAngle += 2.5;
            if (_simStep >= 50) { StopSimulation(); return; }
        }
        else if (_simMode == 2) // CCW
        {
            _simAngle -= 2.5;
            if (_simStep >= 50) { StopSimulation(); return; }
        }
        else if (_simMode == 3) // Swipe
        {
            _simOffsetX = Math.Sin(_simStep * 0.15) * 350;
            if (_simStep >= 60) { StopSimulation(); return; }
        }
        else if (_simMode == 4) // Noise
        {
            _simAngle += (Random.Shared.NextDouble() - 0.5) * 0.4;
            if (_simStep >= 40) { StopSimulation(); return; }
        }

        var points = new List<TouchPoint>();
        for (int i = 0; i < 3; i++)
        {
            double a = (_simAngle + i * 120.0) * Math.PI / 180.0;
            double px = cx + r * Math.Cos(a);
            double py = cy + r * Math.Sin(a);

            if (_simMode == 4)
            {
                px += (Random.Shared.NextDouble() - 0.5) * 2;
                py += (Random.Shared.NextDouble() - 0.5) * 2;
            }

            points.Add(new TouchPoint((uint)(i + 1), px, py, now));
        }

        var frame = new TouchFrame(points, now);
        OnSimulatedFrame?.Invoke(frame);
    }

    private void StopSimulation()
    {
        _simTimer.Stop();
        OnSimulatedFrame?.Invoke(new TouchFrame(new List<TouchPoint>(), Environment.TickCount64));
        AppendLogInternal("[SIMULATION ENDED] Contacts released.");
    }

    private void UpdateHeaderSettings()
    {
        _lblSettings.Text =
            $"Threshold: {Settings.RotationThresholdDegrees:F0}° | " +
            $"Step: {Settings.VolumeStepSize * 100:F0}% / {Settings.DegreesPerVolumeStep:F0}° | " +
            $"CW={(Settings.ClockwiseIsVolumeUp ? "Vol +" : "Vol -")}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Layout
    // ─────────────────────────────────────────────────────────────────────────

    private void InitializeLayout()
    {
        // Header
        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.Height = 42;
        _headerPanel.BackColor = Color.FromArgb(20, 25, 38);
        _headerPanel.Padding = new Padding(12, 8, 12, 0);

        var title = new Label
        {
            Text = "TouchpadGestureControl — Diagnostic & Tuning Dashboard",
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 210, 255),
        };
        _headerPanel.Controls.Add(title);

        UpdateHeaderSettings();
        _lblSettings.Dock = DockStyle.Right;
        _lblSettings.AutoSize = true;
        _lblSettings.ForeColor = Color.FromArgb(130, 160, 200);
        _lblSettings.Padding = new Padding(0, 4, 0, 0);
        _headerPanel.Controls.Add(_lblSettings);

        // Left Panel (Telemetry + Live Tuning + Simulation Controls)
        _leftPanel.Dock = DockStyle.Left;
        _leftPanel.Width = 340;
        _leftPanel.BackColor = Color.FromArgb(18, 20, 28);
        _leftPanel.Padding = new Padding(12, 10, 10, 10);
        _leftPanel.AutoScroll = true;

        // 1. Telemetry Section
        AddSectionLabel(_leftPanel, "LIVE TELEMETRY");

        foreach (var lbl in new[] {
            _lblProvider, _lblFingerCount, _lblGestureState,
            _lblVolume, _lblRawDelta, _lblSmoothed, _lblAccumulated,
            _lblCentroid, _lblArea, _lblAngles })
        {
            lbl.AutoSize = false;
            lbl.Height = lbl == _lblAngles ? 54 : 20;
            lbl.Dock = DockStyle.Top;
            lbl.ForeColor = Color.FromArgb(200, 215, 240);
            lbl.Font = new Font("Consolas", 8.5f);
            _leftPanel.Controls.Add(lbl);
        }

        _leftPanel.Controls.SetChildIndex(_lblAngles, 0);
        _leftPanel.Controls.SetChildIndex(_lblArea, 1);
        _leftPanel.Controls.SetChildIndex(_lblCentroid, 2);
        _leftPanel.Controls.SetChildIndex(_lblAccumulated, 3);
        _leftPanel.Controls.SetChildIndex(_lblSmoothed, 4);
        _leftPanel.Controls.SetChildIndex(_lblRawDelta, 5);
        _leftPanel.Controls.SetChildIndex(_lblVolume, 6);
        _leftPanel.Controls.SetChildIndex(_lblGestureState, 7);
        _leftPanel.Controls.SetChildIndex(_lblFingerCount, 8);
        _leftPanel.Controls.SetChildIndex(_lblProvider, 9);

        // 2. Live Tuner Settings Section
        var tuningPanel = new Panel { Dock = DockStyle.Top, Height = 250, Padding = new Padding(0, 10, 0, 0) };
        AddSectionLabel(tuningPanel, "VOLUME & SENSITIVITY CONTROLS");

        // Volume step slider (1% to 15%)
        _lblStepSizeValue.Text = $"Volume Delta per Step: {Settings.VolumeStepSize * 100:F0}%";
        _lblStepSizeValue.Dock = DockStyle.Top;
        _lblStepSizeValue.ForeColor = Color.FromArgb(255, 215, 0);
        _lblStepSizeValue.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _lblStepSizeValue.Height = 20;

        _tbVolumeStep.Dock = DockStyle.Top;
        _tbVolumeStep.Minimum = 1;
        _tbVolumeStep.Maximum = 15;
        _tbVolumeStep.Value = (int)(Settings.VolumeStepSize * 100);
        _tbVolumeStep.TickStyle = TickStyle.None;
        _tbVolumeStep.Height = 28;
        _tbVolumeStep.Scroll += (_, _) =>
        {
            Settings.VolumeStepSize = _tbVolumeStep.Value / 100.0;
            _lblStepSizeValue.Text = $"Volume Delta per Step: {_tbVolumeStep.Value}%";
            UpdateHeaderSettings();
            AppendLogInternal($"[CONFIG] Volume step updated: {_tbVolumeStep.Value}%");
        };

        // Degrees per step slider (10° to 50°)
        _lblDegPerStepValue.Text = $"Rotation per Volume Step: {Settings.DegreesPerVolumeStep:F0}°";
        _lblDegPerStepValue.Dock = DockStyle.Top;
        _lblDegPerStepValue.ForeColor = Color.FromArgb(0, 220, 255);
        _lblDegPerStepValue.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _lblDegPerStepValue.Height = 20;

        _tbDegPerStep.Dock = DockStyle.Top;
        _tbDegPerStep.Minimum = 10;
        _tbDegPerStep.Maximum = 50;
        _tbDegPerStep.Value = (int)Settings.DegreesPerVolumeStep;
        _tbDegPerStep.TickStyle = TickStyle.None;
        _tbDegPerStep.Height = 28;
        _tbDegPerStep.Scroll += (_, _) =>
        {
            Settings.DegreesPerVolumeStep = _tbDegPerStep.Value;
            _lblDegPerStepValue.Text = $"Rotation per Volume Step: {_tbDegPerStep.Value}°";
            UpdateHeaderSettings();
            AppendLogInternal($"[CONFIG] Rotation per step updated: {_tbDegPerStep.Value}°");
        };

        // Threshold slider (5° to 30°)
        _lblThresholdValue.Text = $"Trigger Deadzone Threshold: {Settings.RotationThresholdDegrees:F0}°";
        _lblThresholdValue.Dock = DockStyle.Top;
        _lblThresholdValue.ForeColor = Color.FromArgb(160, 220, 160);
        _lblThresholdValue.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _lblThresholdValue.Height = 20;

        _tbThreshold.Dock = DockStyle.Top;
        _tbThreshold.Minimum = 5;
        _tbThreshold.Maximum = 30;
        _tbThreshold.Value = (int)Settings.RotationThresholdDegrees;
        _tbThreshold.TickStyle = TickStyle.None;
        _tbThreshold.Height = 28;
        _tbThreshold.Scroll += (_, _) =>
        {
            Settings.RotationThresholdDegrees = _tbThreshold.Value;
            _lblThresholdValue.Text = $"Trigger Deadzone Threshold: {_tbThreshold.Value}°";
            UpdateHeaderSettings();
        };

        // Direction toggle button
        _btnToggleCw.Text = Settings.ClockwiseIsVolumeUp ? "Clockwise = Volume UP" : "Clockwise = Volume DOWN";
        _btnToggleCw.Dock = DockStyle.Top;
        _btnToggleCw.Height = 28;
        _btnToggleCw.FlatStyle = FlatStyle.Flat;
        _btnToggleCw.BackColor = Color.FromArgb(40, 60, 90);
        _btnToggleCw.ForeColor = Color.White;
        _btnToggleCw.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _btnToggleCw.Click += (_, _) =>
        {
            Settings.ClockwiseIsVolumeUp = !Settings.ClockwiseIsVolumeUp;
            _btnToggleCw.Text = Settings.ClockwiseIsVolumeUp ? "Clockwise = Volume UP" : "Clockwise = Volume DOWN";
            UpdateHeaderSettings();
            AppendLogInternal($"[CONFIG] Direction toggled: Clockwise = {(Settings.ClockwiseIsVolumeUp ? "Volume Up" : "Volume Down")}");
        };

        tuningPanel.Controls.Add(_btnToggleCw);
        tuningPanel.Controls.Add(_tbThreshold);
        tuningPanel.Controls.Add(_lblThresholdValue);
        tuningPanel.Controls.Add(_tbDegPerStep);
        tuningPanel.Controls.Add(_lblDegPerStepValue);
        tuningPanel.Controls.Add(_tbVolumeStep);
        tuningPanel.Controls.Add(_lblStepSizeValue);

        _leftPanel.Controls.Add(tuningPanel);
        _leftPanel.Controls.SetChildIndex(tuningPanel, 0);

        // 3. Test Simulation Section
        var simPanel = new Panel { Dock = DockStyle.Top, Height = 175, Padding = new Padding(0, 10, 0, 0) };
        AddSectionLabel(simPanel, "TEST SIMULATION");

        StyleButton(_btnSimCw, "Rotate Clockwise (Vol +)", Color.FromArgb(0, 120, 80));
        _btnSimCw.Click += (_, _) => StartSimulation(1);
        simPanel.Controls.Add(_btnSimCw);

        StyleButton(_btnSimCcw, "Rotate Counter-Clockwise (Vol -)", Color.FromArgb(140, 50, 40));
        _btnSimCcw.Click += (_, _) => StartSimulation(2);
        simPanel.Controls.Add(_btnSimCcw);

        StyleButton(_btnSimSwipe, "Translational Swipe Test", Color.FromArgb(40, 70, 120));
        _btnSimSwipe.Click += (_, _) => StartSimulation(3);
        simPanel.Controls.Add(_btnSimSwipe);

        StyleButton(_btnSimNoise, "Jitter Filter Noise Test", Color.FromArgb(80, 80, 90));
        _btnSimNoise.Click += (_, _) => StartSimulation(4);
        simPanel.Controls.Add(_btnSimNoise);

        _leftPanel.Controls.Add(simPanel);
        _leftPanel.Controls.SetChildIndex(simPanel, 0);

        // Center / Right: Radar Canvas + Event Log
        var rightContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 14, 18) };

        _canvas.Dock = DockStyle.Fill;
        _canvas.BackColor = Color.FromArgb(15, 17, 24);

        var bottomLogPanel = new Panel { Dock = DockStyle.Bottom, Height = 150, BackColor = Color.FromArgb(10, 12, 16) };

        var logHeader = new Label
        {
            Text = "SYSTEM & EVENT LOG",
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.FromArgb(22, 26, 36),
            ForeColor = Color.FromArgb(0, 220, 160),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Padding = new Padding(8, 3, 0, 0),
        };

        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = Color.FromArgb(8, 10, 14);
        _logBox.ForeColor = Color.FromArgb(120, 230, 150);
        _logBox.Font = new Font("Consolas", 8.5f);
        _logBox.ReadOnly = true;
        _logBox.BorderStyle = BorderStyle.None;
        _logBox.ScrollBars = RichTextBoxScrollBars.Vertical;

        bottomLogPanel.Controls.Add(_logBox);
        bottomLogPanel.Controls.Add(logHeader);

        rightContainer.Controls.Add(_canvas);
        rightContainer.Controls.Add(bottomLogPanel);

        Controls.Add(rightContainer);
        Controls.Add(_leftPanel);
        Controls.Add(_headerPanel);
    }

    private static void AddSectionLabel(Control parent, string title)
    {
        var lbl = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.FromArgb(0, 180, 255),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Padding = new Padding(0, 4, 0, 0)
        };
        parent.Controls.Add(lbl);
    }

    private static void StyleButton(Button btn, string text, Color bg)
    {
        btn.Text = text;
        btn.Dock = DockStyle.Top;
        btn.Height = 28;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = bg;
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btn.Margin = new Padding(0, 2, 0, 3);
        btn.Cursor = Cursors.Hand;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnFormClosing(e);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Custom Touchpad Surface Visualizer Canvas
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class TouchpadCanvas : Control
{
    private DiagnosticSnapshot? _snap;
    private readonly Color[] _fingerColors =
    {
        Color.FromArgb(0, 240, 255),   // Cyan (P1)
        Color.FromArgb(255, 0, 128),   // Magenta (P2)
        Color.FromArgb(255, 215, 0),   // Gold (P3)
        Color.FromArgb(50, 255, 120),  // Green (P4)
        Color.FromArgb(180, 100, 255), // Purple (P5)
    };

    private double _observedMaxX = 1920.0;
    private double _observedMaxY = 1080.0;

    public TouchpadCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
    }

    public void UpdateFrame(DiagnosticSnapshot snap)
    {
        _snap = snap;
        if (snap?.Frame?.Points != null)
        {
            foreach (var p in snap.Frame.Points)
            {
                if (p.X > _observedMaxX) _observedMaxX = p.X * 1.05;
                if (p.Y > _observedMaxY) _observedMaxY = p.Y * 1.05;
            }
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        int w = Width;
        int h = Height;

        int padMargin = 16;
        var padRect = new Rectangle(padMargin, padMargin, w - padMargin * 2, h - padMargin * 2);

        // Touchpad Surface
        using (var brush = new LinearGradientBrush(padRect,
            Color.FromArgb(24, 27, 36), Color.FromArgb(16, 18, 25), LinearGradientMode.Vertical))
        {
            DrawRoundedRectangle(g, brush, padRect, 16);
        }

        using (var borderPen = new Pen(Color.FromArgb(45, 55, 75), 2f))
        {
            DrawRoundedRectangle(g, borderPen, padRect, 16);
        }

        // Grid Overlay
        using (var gridPen = new Pen(Color.FromArgb(18, 30, 45, 60), 1f) { DashStyle = DashStyle.Dash })
        {
            for (int x = padRect.Left + 40; x < padRect.Right; x += 40)
                g.DrawLine(gridPen, x, padRect.Top, x, padRect.Bottom);
            for (int y = padRect.Top + 40; y < padRect.Bottom; y += 40)
                g.DrawLine(gridPen, padRect.Left, y, padRect.Right, y);
        }

        using (var fFont = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (var fBrush = new SolidBrush(Color.FromArgb(70, 90, 120)))
        {
            g.DrawString("TOUCHPAD SURFACE (REAL-TIME CONTACT TRACKING)", fFont, fBrush, padRect.Left + 14, padRect.Top + 12);
        }

        var points = _snap?.Frame?.Points;
        if (points == null || points.Count == 0)
        {
            using var font = new Font("Segoe UI", 12f);
            using var brush = new SolidBrush(Color.FromArgb(100, 120, 150));
            string msg = "Place fingers on the touchpad surface to track...";
            var size = g.MeasureString(msg, font);
            g.DrawString(msg, font, brush, (w - size.Width) / 2, (h - size.Height) / 2);
            return;
        }

        PointF MapToPad(double px, double py)
        {
            double scaleX = (padRect.Width - 60.0) / Math.Max(100.0, _observedMaxX);
            double scaleY = (padRect.Height - 60.0) / Math.Max(100.0, _observedMaxY);
            float mx = (float)(padRect.Left + 30 + px * scaleX);
            float my = (float)(padRect.Top + 30 + py * scaleY);
            return new PointF(
                Math.Clamp(mx, padRect.Left + 15, padRect.Right - 15),
                Math.Clamp(my, padRect.Top + 15, padRect.Bottom - 15)
            );
        }

        var mappedPts = points.Select(p => MapToPad(p.X, p.Y)).ToList();

        // If >= 3 fingers, draw Triangle Geometry & Centroid & Rotation Indicator
        if (points.Count >= 3)
        {
            var p0 = mappedPts[0];
            var p1 = mappedPts[1];
            var p2 = mappedPts[2];

            using (var triBrush = new LinearGradientBrush(padRect,
                Color.FromArgb(35, 0, 200, 255), Color.FromArgb(20, 255, 0, 128), LinearGradientMode.ForwardDiagonal))
            {
                g.FillPolygon(triBrush, new[] { p0, p1, p2 });
            }

            using (var edgePen = new Pen(Color.FromArgb(180, 0, 220, 255), 2.5f))
            {
                g.DrawPolygon(edgePen, new[] { p0, p1, p2 });
            }

            float cx = (p0.X + p1.X + p2.X) / 3f;
            float cy = (p0.Y + p1.Y + p2.Y) / 3f;

            using (var rayPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(rayPen, cx, cy, p0.X, p0.Y);
                g.DrawLine(rayPen, cx, cy, p1.X, p1.Y);
                g.DrawLine(rayPen, cx, cy, p2.X, p2.Y);
            }

            using (var cPen = new Pen(Color.FromArgb(255, 255, 255), 2f))
            {
                g.DrawLine(cPen, cx - 10, cy, cx + 10, cy);
                g.DrawLine(cPen, cx, cy - 10, cx, cy + 10);
            }
            using (var cBrush = new SolidBrush(Color.FromArgb(0, 255, 180)))
            {
                g.FillEllipse(cBrush, cx - 4, cy - 4, 8, 8);
            }

            if (_snap != null && Math.Abs(_snap.AccumulatedDegrees) > 0.5)
            {
                bool isCw = _snap.AccumulatedDegrees > 0;
                Color rotColor = (isCw == Settings.ClockwiseIsVolumeUp) ? Color.FromArgb(50, 255, 120) : Color.FromArgb(255, 100, 80);
                using var rotPen = new Pen(rotColor, 3f) { EndCap = LineCap.ArrowAnchor };

                float arcR = 36;
                float startAng = isCw ? 220 : 320;
                float sweepAng = isCw ? 100 : -100;
                g.DrawArc(rotPen, cx - arcR, cy - arcR, arcR * 2, arcR * 2, startAng, sweepAng);

                using var rotFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                using var rotBrush = new SolidBrush(rotColor);
                string act = isCw == Settings.ClockwiseIsVolumeUp ? "VOL +" : "VOL -";
                string degText = $"{_snap.AccumulatedDegrees:+0.0;-0.0}° {(isCw ? "CW" : "CCW")} ({act})";
                g.DrawString(degText, rotFont, rotBrush, cx - 45, cy + 42);
            }
        }

        // Draw Each Finger Beacon
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var mp = mappedPts[i];
            Color c = _fingerColors[i % _fingerColors.Length];

            using (var glowBrush = new SolidBrush(Color.FromArgb(40, c.R, c.G, c.B)))
            {
                g.FillEllipse(glowBrush, mp.X - 22, mp.Y - 22, 44, 44);
            }

            using (var ringPen = new Pen(Color.White, 2f))
            using (var bodyBrush = new SolidBrush(c))
            {
                g.FillEllipse(bodyBrush, mp.X - 12, mp.Y - 12, 24, 24);
                g.DrawEllipse(ringPen, mp.X - 12, mp.Y - 12, 24, 24);
            }

            using var lblFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var lblBrush = new SolidBrush(Color.White);
            using var bgBrush = new SolidBrush(Color.FromArgb(180, 15, 18, 25));

            string tag = $"P{p.Id} ({p.X:F0}, {p.Y:F0})";
            var tSize = g.MeasureString(tag, lblFont);
            g.FillRectangle(bgBrush, mp.X + 16, mp.Y - 10, tSize.Width + 6, tSize.Height + 2);
            g.DrawString(tag, lblFont, lblBrush, mp.X + 19, mp.Y - 9);
        }
    }

    private static void DrawRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectPath(bounds, radius);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectPath(bounds, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
