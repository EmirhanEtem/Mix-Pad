namespace TouchpadGestureControl;

/// <summary>
/// Central configuration for gesture sensitivity and behavior.
/// All values are designed to be easily tunable at runtime.
/// </summary>
public static class Settings
{
    // ─────────────────────────────────────────────────────────────────────────
    // Gesture Thresholds
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum accumulated rotation (in degrees) before any volume change occurs.
    /// Lower = quicker to respond. Higher = requires deliberate rotation.
    /// Range: 5° – 45°. Default: 12.0°.
    /// </summary>
    public static double RotationThresholdDegrees = 12.0;

    /// <summary>
    /// Below this per-frame angular delta (degrees), movement is treated as jitter
    /// and ignored. Prevents noise from stationary fingers.
    /// Range: 0.1° – 1.0°. Default: 0.3°.
    /// </summary>
    public static double JitterThresholdDegrees = 0.3;

    /// <summary>
    /// Minimum area of the 3-finger triangle in pixels².
    /// Kept very low so any 3-finger alignment (even almost in a line) works naturally.
    /// </summary>
    public static double MinTriangleAreaPixels = 10.0;

    /// <summary>
    /// Minimum distance between any two fingers in pixels.
    /// Prevents duplicate coordinate reports.
    /// </summary>
    public static double MinFingerDistancePixels = 5.0;

    // ─────────────────────────────────────────────────────────────────────────
    // Volume Control Settings (Live tunable in UI)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Volume change applied per step (0.01 to 0.20 = 1% to 20%).
    /// Default: 0.04 (4% per step).
    /// </summary>
    public static double VolumeStepSize = 0.04;

    /// <summary>
    /// How many degrees of cumulative rotation equals one volume step.
    /// Lower = faster volume ramp per degree. Range: 10° – 60°.
    /// Default: 20.0°.
    /// </summary>
    public static double DegreesPerVolumeStep = 20.0;

    /// <summary>
    /// Maximum total volume change allowed per second (0.0 to 1.0).
    /// Rate-limits rapid gestures to prevent sudden volume spikes.
    /// Default: 0.40 (40% per second).
    /// </summary>
    public static double MaxVolumeChangePerSecond = 0.40;

    /// <summary>
    /// If true: clockwise rotation on screen → volume UP.
    /// If false: clockwise rotation on screen → volume DOWN.
    /// </summary>
    public static bool ClockwiseIsVolumeUp = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Smoothing
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exponential Moving Average (EMA) alpha for rotation smoothing.
    /// 0.1 = very smooth, 0.9 = instantaneous response.
    /// Default: 0.40.
    /// </summary>
    public static double RotationSmoothingAlpha = 0.40;

    // ─────────────────────────────────────────────────────────────────────────
    // Input Provider
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Required number of simultaneous touch contacts to activate gesture.
    /// </summary>
    public const int RequiredFingers = 3;

    /// <summary>
    /// Preferred input provider: "Auto", "RawHID", "WmPointer", "WmTouch".
    /// </summary>
    public static string PreferredProvider = "Auto";

    // ─────────────────────────────────────────────────────────────────────────
    // UI & Diagnostics
    // ─────────────────────────────────────────────────────────────────────────

    public static bool DiagnosticMode = false;
    public static int DiagnosticMaxLogLines = 200;
    public static double OverlayOpacity = 0.01;
}
