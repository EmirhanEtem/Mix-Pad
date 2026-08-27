using TouchpadGestureControl.Audio;
using TouchpadGestureControl.Gesture;
using Microsoft.Win32;

namespace TouchpadGestureControl.UI;

/// <summary>
/// Top-level application controller. Manages:
///   - System tray icon and context menu
///   - MessageWindow (touch input)
///   - GestureDetector (algorithm)
///   - VolumeController (Core Audio)
///   - DiagnosticForm (debug window)
///
/// Lifetime: created in Program.cs, disposed on exit.
/// </summary>
public sealed class TrayApplication : IDisposable
{
    // ─────────────────────────────────────────────────────────────────────────
    // Components
    // ─────────────────────────────────────────────────────────────────────────

    private readonly NotifyIcon      _trayIcon;
    private readonly MessageWindow   _msgWindow;
    private readonly VolumeController _volume;
    private readonly GestureDetector  _gesture;
    private readonly DiagnosticForm   _diagnosticForm;

    private bool _disposed;

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public TrayApplication(bool startDiagnostic)
    {
        Settings.DiagnosticMode = startDiagnostic;

        // 1. Core Audio
        _volume = new VolumeController();

        // 2. Gesture detector
        _gesture = new GestureDetector(_volume);

        // 3. Diagnostic form (created but not shown yet)
        _diagnosticForm = new DiagnosticForm();

        // 4. Message window (input host)
        _msgWindow = new MessageWindow();
        _msgWindow.LogMessage += msg =>
        {
            System.Diagnostics.Debug.WriteLine(msg);
            _diagnosticForm.AppendLog(msg);
        };
        _msgWindow.Initialize();

        // 5. Wire touch frames → gesture detector
        _msgWindow.FrameReceived += _gesture.OnFrame;
        _diagnosticForm.OnSimulatedFrame = frame => _gesture.OnFrame(this, frame);

        // 6. Wire gesture diagnostics → diagnostic form
        _gesture.DiagnosticCallback += snap =>
        {
            // Always update diagnostic form if it's visible.
            if (_diagnosticForm.Visible)
                _diagnosticForm.UpdateSnapshot(snap);
        };

        // 7. Tray icon
        _trayIcon = BuildTrayIcon();

        // 8. Show provider info in diagnostic form
        string providerName = _msgWindow.ActiveProvider?.ProviderName ?? "None";
        var hidDevices = MessageWindow.EnumerateHidDevices();
        _diagnosticForm.SetProviderInfo(providerName, hidDevices);

        // 9. Open diagnostic window immediately if requested
        if (startDiagnostic)
            _diagnosticForm.Show();

        // ShowBalloonTip named params changed in .NET 6+ — use properties + int overload.
        _trayIcon.BalloonTipTitle = "TouchpadGestureControl";
        _trayIcon.BalloonTipText  = $"Running ({providerName}). Use 3 fingers to rotate for volume.";
        _trayIcon.BalloonTipIcon  = ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(3000);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tray icon
    // ─────────────────────────────────────────────────────────────────────────

    private NotifyIcon BuildTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Text    = "TouchpadGestureControl",
            Icon    = BuildIcon(),
            Visible = true,
        };

        var menu = new ContextMenuStrip();

        // Title (non-clickable)
        var title = new ToolStripMenuItem("TouchpadGestureControl") { Enabled = false };
        menu.Items.Add(title);
        menu.Items.Add(new ToolStripSeparator());

        // Diagnostic window toggle
        var diagItem = new ToolStripMenuItem("Diagnostic Window");
        diagItem.Click += (_, _) =>
        {
            if (_diagnosticForm.Visible)
                _diagnosticForm.Hide();
            else
                _diagnosticForm.Show();
        };
        menu.Items.Add(diagItem);

        menu.Items.Add(new ToolStripSeparator());

        // Settings sub-menu (live tweak without restart)
        var settingsMenu = new ToolStripMenuItem("Settings");

        var cwToggle = new ToolStripMenuItem(
            $"Clockwise = Volume Up: {Settings.ClockwiseIsVolumeUp}");
        cwToggle.Click += (_, _) =>
        {
            Settings.ClockwiseIsVolumeUp = !Settings.ClockwiseIsVolumeUp;
            cwToggle.Text = $"Clockwise = Volume Up: {Settings.ClockwiseIsVolumeUp}";
        };
        settingsMenu.DropDownItems.Add(cwToggle);

        var sensitivityLabel = new ToolStripMenuItem("Threshold (degrees):") { Enabled = false };
        settingsMenu.DropDownItems.Add(sensitivityLabel);

        foreach (var deg in new[] { 10.0, 15.0, 20.0, 30.0, 45.0 })
        {
            var d = deg; // capture
            var item = new ToolStripMenuItem($"  {d}°")
            {
                Checked = Math.Abs(Settings.RotationThresholdDegrees - d) < 0.5
            };
            item.Click += (_, _) =>
            {
                Settings.RotationThresholdDegrees = d;
                // Uncheck all siblings
                foreach (ToolStripMenuItem si in settingsMenu.DropDownItems)
                    if (si.Text?.StartsWith("  ") == true) si.Checked = false;
                item.Checked = true;
            };
            settingsMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(settingsMenu);
        menu.Items.Add(new ToolStripSeparator());

        // Windows startup toggle
        var startupItem = new ToolStripMenuItem("Run at Windows Startup")
        {
            Checked = IsInStartup()
        };
        startupItem.Click += (_, _) =>
        {
            bool wasChecked = startupItem.Checked;
            if (wasChecked) RemoveFromStartup();
            else            AddToStartup();
            startupItem.Checked = !wasChecked;
        };
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        };
        menu.Items.Add(exitItem);

        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (_, _) =>
        {
            if (_diagnosticForm.Visible) _diagnosticForm.Hide();
            else _diagnosticForm.Show();
        };

        return icon;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup (registry)
    // ─────────────────────────────────────────────────────────────────────────

    private const string StartupRegKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TouchpadGestureControl";

    private static bool IsInStartup()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey(StartupRegKey, writable: false);
        return key?.GetValue(AppName) != null;
    }

    private static void AddToStartup()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey(StartupRegKey, writable: true);
        key?.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
    }

    private static void RemoveFromStartup()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey(StartupRegKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Icon generation (drawn programmatically — no .ico file required)
    // ─────────────────────────────────────────────────────────────────────────

    private static System.Drawing.Icon BuildIcon()
    {
        // Draw a simple speaker icon at 16x16.
        using var bmp = new System.Drawing.Bitmap(16, 16);
        using var g   = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);

        // Draw three finger dots in a triangle pattern (blue)
        var brush = System.Drawing.Brushes.DodgerBlue;
        g.FillEllipse(brush, 3,  1, 5, 5);  // top
        g.FillEllipse(brush, 0,  9, 5, 5);  // bottom-left
        g.FillEllipse(brush, 10, 9, 5, 5);  // bottom-right

        // Draw circular arrows (white arc)
        using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.5f);
        g.DrawArc(pen,
            new System.Drawing.Rectangle(2, 2, 12, 12),
            startAngle: 30, sweepAngle: 300);

        IntPtr hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IDisposable
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _msgWindow.Dispose();
        _gesture.Dispose();
        _volume.Dispose();

        if (!_diagnosticForm.IsDisposed)
            _diagnosticForm.Dispose();
    }
}
