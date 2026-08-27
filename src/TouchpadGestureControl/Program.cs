using TouchpadGestureControl.UI;

namespace TouchpadGestureControl;

/// <summary>
/// Application entry point.
/// Usage:
///   TouchpadGestureControl.exe              → Normal mode (tray only)
///   TouchpadGestureControl.exe --diagnostic → Open diagnostic window immediately
///   TouchpadGestureControl.exe -d           → Same as --diagnostic
/// </summary>
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Prevent multiple instances.
        using var mutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "TouchpadGestureControl_SingleInstance",
            out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "TouchpadGestureControl is already running.\n\nCheck the system tray (notification area).",
                "Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        bool diagnostic = args.Contains("--diagnostic", StringComparer.OrdinalIgnoreCase)
                       || args.Contains("-d", StringComparer.OrdinalIgnoreCase);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Global exception handling — log and show balloon tip instead of crashing.
        Application.ThreadException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Exception}");
            // Continue running — don't crash on a single bad frame.
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Fatal exception: {e.ExceptionObject}");
        };

        using var app = new TrayApplication(diagnostic);
        Application.Run(); // Message pump — runs until Application.Exit() is called.
    }
}
