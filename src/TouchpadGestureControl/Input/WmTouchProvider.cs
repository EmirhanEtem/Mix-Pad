using TouchpadGestureControl.NativeApi;
using System.Runtime.InteropServices;

namespace TouchpadGestureControl.Input;

/// <summary>
/// Touch input provider using WM_TOUCH messages (Windows 7+).
///
/// WM_TOUCH is widely compatible but requires the window to be in the foreground.
/// Used as a fallback when WM_POINTER / RegisterPointerInputTarget fails.
///
/// TOUCHINPUT coordinates are in hundredths of a pixel (divide by 100 for pixels).
/// </summary>
public sealed class WmTouchProvider : ITouchInputProvider
{
    public string ProviderName => "WmTouch";
    public bool IsActive { get; private set; }

    public event EventHandler<TouchFrame>? FrameReceived;

    private IntPtr _hwnd;

    // Live set of contacts: cleared on UP events, updated on DOWN/MOVE.
    private readonly Dictionary<uint, TouchPoint> _activeContacts = new();

    public bool Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // TWF_WANTPALM disables palm rejection so we get all raw contacts.
        // TWF_FINETOUCH requests high-resolution coordinates.
        bool ok = NativeMethods.RegisterTouchWindow(hwnd,
            NativeConstants.TWF_FINETOUCH | NativeConstants.TWF_WANTPALM);

        if (ok)
        {
            IsActive = true;
            DiagnosticLog("[WmTouchProvider] RegisterTouchWindow succeeded.");
        }
        else
        {
            string err = NativeMethods.GetLastErrorMessage();
            DiagnosticLog($"[WmTouchProvider] RegisterTouchWindow FAILED: {err}");
            IsActive = false;
        }

        return IsActive;
    }

    /// <summary>
    /// Called by MessageWindow.WndProc for WM_TOUCH messages.
    /// wParam low word = touch point count, lParam = HTOUCHINPUT handle.
    /// </summary>
    public void ProcessMessage(IntPtr wParam, IntPtr lParam)
    {
        int count = NativeConstants.LOWORD(wParam);
        if (count <= 0) return;

        var inputs = new TOUCHINPUT[count];
        int structSize = Marshal.SizeOf<TOUCHINPUT>();

        bool got = NativeMethods.GetTouchInputInfo(lParam, count, inputs, structSize);

        // Must close handle regardless of success.
        NativeMethods.CloseTouchInputHandle(lParam);

        if (!got) return;

        long now = Environment.TickCount64;

        foreach (var ti in inputs)
        {
            // Convert from hundredths-of-a-pixel to pixels.
            double x = ti.x / 100.0;
            double y = ti.y / 100.0;

            bool isDown = (ti.dwFlags & NativeConstants.TOUCHEVENTF_DOWN) != 0;
            bool isMove = (ti.dwFlags & NativeConstants.TOUCHEVENTF_MOVE) != 0;
            bool isUp   = (ti.dwFlags & NativeConstants.TOUCHEVENTF_UP)   != 0;

            if (isUp)
            {
                _activeContacts.Remove(ti.dwID);
            }
            else if (isDown || isMove)
            {
                _activeContacts[ti.dwID] = new TouchPoint(ti.dwID, x, y, now);
            }
        }

        // Emit a frame with the current state of all contacts.
        var points = _activeContacts.Values.ToList();
        var frame = new TouchFrame(points, now);
        FrameReceived?.Invoke(this, frame);
    }

    private static void DiagnosticLog(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero && IsActive)
        {
            NativeMethods.UnregisterTouchWindow(_hwnd);
        }
        _activeContacts.Clear();
        IsActive = false;
    }
}
