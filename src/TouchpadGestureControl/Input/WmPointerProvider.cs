using TouchpadGestureControl.NativeApi;

namespace TouchpadGestureControl.Input;

/// <summary>
/// Touch input provider using WM_POINTER messages (Windows 8+).
///
/// Strategy:
///   1. Calls RegisterPointerInputTarget() to receive touch events even when
///      the window is not in focus (background operation).
///   2. For each WM_POINTERDOWN / WM_POINTERUPDATE / WM_POINTERUP message,
///      calls GetPointerFrameTouchInfo() to retrieve ALL simultaneous contacts
///      in the same hardware frame — not just the one that triggered the message.
///   3. Emits a TouchFrame event with all contacts.
///
/// This is the preferred provider. Falls back to WmTouchProvider if unavailable.
/// </summary>
public sealed class WmPointerProvider : ITouchInputProvider
{
    public string ProviderName => "WmPointer";
    public bool IsActive { get; private set; }

    public event EventHandler<TouchFrame>? FrameReceived;

    private IntPtr _hwnd;

    // Track the last frame ID to avoid re-processing duplicate WM_POINTER
    // messages that arrive for the same hardware frame but different fingers.
    private uint _lastFrameId = uint.MaxValue;

    // Current active contacts by pointer ID.
    private readonly Dictionary<uint, TouchPoint> _activeContacts = new();

    public bool Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // Attempt to claim all PT_TOUCH input regardless of focus.
        bool success = NativeMethods.RegisterPointerInputTarget(hwnd, NativeConstants.PT_TOUCH);

        if (success)
        {
            IsActive = true;
            DiagnosticLog($"[WmPointerProvider] RegisterPointerInputTarget succeeded.");
        }
        else
        {
            string err = NativeMethods.GetLastErrorMessage();
            DiagnosticLog($"[WmPointerProvider] RegisterPointerInputTarget FAILED: {err}");
            IsActive = false;
        }

        return IsActive;
    }

    /// <summary>
    /// Called by MessageWindow.WndProc for WM_POINTER* messages.
    /// </summary>
    public void ProcessMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        uint pointerId = NativeConstants.GET_POINTER_ID(wParam);

        switch (msg)
        {
            case NativeConstants.WM_POINTERDOWN:
                HandleContactChange(pointerId, isUp: false);
                break;

            case NativeConstants.WM_POINTERUPDATE:
                HandleUpdate(pointerId);
                break;

            case NativeConstants.WM_POINTERUP:
                HandleContactChange(pointerId, isUp: true);
                break;

            case NativeConstants.WM_POINTERCAPTURECHANGED:
                // Pointer capture lost — treat as all fingers lifted.
                _activeContacts.Clear();
                _lastFrameId = uint.MaxValue;
                EmitFrame();
                break;
        }
    }

    private void HandleUpdate(uint pointerId)
    {
        // Get the full frame of touch contacts.
        var frame = GetPointerFrame(pointerId);
        if (frame == null) return;

        // Check if this is a new frame (avoid duplicate processing).
        // Multiple WM_POINTERUPDATE messages can arrive for the same hardware
        // frame (one per active contact). We only process each frame once.
        uint frameId = frame[0].pointerInfo.frameId;
        if (frameId == _lastFrameId) return;
        _lastFrameId = frameId;

        // Update the active contacts dictionary.
        // Keep contacts that are still in range/contact; remove lifted ones.
        _activeContacts.Clear();
        foreach (var ti in frame)
        {
            uint flags = ti.pointerInfo.pointerFlags;
            bool inContact = (flags & NativeConstants.POINTER_FLAG_INCONTACT) != 0;
            bool isUp      = (flags & NativeConstants.POINTER_FLAG_UP) != 0;

            if (inContact && !isUp)
            {
                var pt = new TouchPoint(
                    ti.pointerInfo.pointerId,
                    ti.pointerInfo.ptPixelLocation.X,
                    ti.pointerInfo.ptPixelLocation.Y,
                    Environment.TickCount64);
                _activeContacts[pt.Id] = pt;
            }
        }

        EmitFrame();
    }

    private void HandleContactChange(uint pointerId, bool isUp)
    {
        var frame = GetPointerFrame(pointerId);
        if (frame == null)
        {
            if (isUp) _activeContacts.Remove(pointerId);
            EmitFrame();
            return;
        }

        uint frameId = frame[0].pointerInfo.frameId;
        if (frameId == _lastFrameId) return;
        _lastFrameId = frameId;

        _activeContacts.Clear();
        foreach (var ti in frame)
        {
            uint flags = ti.pointerInfo.pointerFlags;
            bool inContact = (flags & NativeConstants.POINTER_FLAG_INCONTACT) != 0;
            bool tiIsUp    = (flags & NativeConstants.POINTER_FLAG_UP) != 0;

            if (inContact && !tiIsUp)
            {
                var pt = new TouchPoint(
                    ti.pointerInfo.pointerId,
                    ti.pointerInfo.ptPixelLocation.X,
                    ti.pointerInfo.ptPixelLocation.Y,
                    Environment.TickCount64);
                _activeContacts[pt.Id] = pt;
            }
        }

        EmitFrame();
    }

    private POINTER_TOUCH_INFO[]? GetPointerFrame(uint pointerId)
    {
        // Step 1: get count
        uint count = 0;
        if (!NativeMethods.GetPointerFrameTouchInfo(pointerId, ref count, null) || count == 0)
            return null;

        // Step 2: get data
        var infos = new POINTER_TOUCH_INFO[count];
        if (!NativeMethods.GetPointerFrameTouchInfo(pointerId, ref count, infos))
            return null;

        return infos;
    }

    private void EmitFrame()
    {
        var points = _activeContacts.Values.ToList();
        var tf = new TouchFrame(points, Environment.TickCount64);
        FrameReceived?.Invoke(this, tf);
    }

    private static void DiagnosticLog(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero && IsActive)
        {
            NativeMethods.UnregisterPointerInputTarget(_hwnd, NativeConstants.PT_TOUCH);
        }
        IsActive = false;
    }
}
