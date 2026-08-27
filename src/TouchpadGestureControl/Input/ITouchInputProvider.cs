namespace TouchpadGestureControl.Input;

/// <summary>
/// Abstraction for any touch input source.
/// Implementations include WM_POINTER and WM_TOUCH providers.
/// </summary>
public interface ITouchInputProvider : IDisposable
{
    /// <summary>Friendly name for diagnostic display (e.g. "WmPointer", "WmTouch").</summary>
    string ProviderName { get; }

    /// <summary>True when the provider has successfully registered and is receiving events.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Fired on the UI thread whenever a new touch frame is available.
    /// Subscribers should process this quickly or marshal to a background thread.
    /// </summary>
    event EventHandler<TouchFrame> FrameReceived;

    /// <summary>
    /// Initialize the provider against the given HWND.
    /// Returns true on success. On failure, IsActive remains false.
    /// </summary>
    bool Initialize(IntPtr hwnd);
}
