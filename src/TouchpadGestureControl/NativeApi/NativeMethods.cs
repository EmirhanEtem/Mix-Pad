using System.Runtime.InteropServices;

namespace TouchpadGestureControl.NativeApi;

/// <summary>
/// P/Invoke declarations for Windows touch, pointer, and device APIs.
/// </summary>
internal static class NativeMethods
{
    // ─────────────────────────────────────────────────────────────────────────
    // WM_TOUCH API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a window to receive WM_TOUCH messages.
    /// The window must be in the foreground to receive messages.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterTouchWindow(IntPtr hwnd, uint ulFlags);

    /// <summary>Unregisters a window from receiving WM_TOUCH messages.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterTouchWindow(IntPtr hwnd);

    /// <summary>
    /// Retrieves touch input information for a WM_TOUCH message.
    /// lParam from WM_TOUCH is passed as hTouchInput.
    /// Coordinates are in hundredths of a pixel.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetTouchInputInfo(
        IntPtr hTouchInput,
        int cInputs,
        [Out] TOUCHINPUT[] pInputs,
        int cbSize);

    /// <summary>Releases resources associated with a touch input handle.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool CloseTouchInputHandle(IntPtr lParam);

    // ─────────────────────────────────────────────────────────────────────────
    // WM_POINTER API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a window to receive all pointer input of the specified type,
    /// regardless of whether the window has focus. This is the key API for
    /// background touch capture.
    /// Requires Windows 8+ (user32.dll).
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterPointerInputTarget(IntPtr hwnd, int type);

    /// <summary>Unregisters a window from receiving pointer input.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterPointerInputTarget(IntPtr hwnd, int type);

    /// <summary>Gets basic pointer info for a given pointer ID.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetPointerInfo(uint pointerId, out POINTER_INFO pointerInfo);

    /// <summary>
    /// Gets info for ALL pointers in the same frame as the given pointer.
    /// Call twice: first with null to get count, then with array to get data.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetPointerFrameInfo(
        uint pointerId,
        ref uint pointerCount,
        [Out] POINTER_INFO[]? pointerInfos);

    /// <summary>Gets touch-specific info for a single pointer.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetPointerTouchInfo(uint pointerId, out POINTER_TOUCH_INFO info);

    /// <summary>
    /// Gets touch-specific info for ALL contacts in the same frame.
    /// Call twice: first with null to get count, then with array to get data.
    /// This is the primary API for multi-finger position tracking.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetPointerFrameTouchInfo(
        uint pointerId,
        ref uint pointerCount,
        [Out] POINTER_TOUCH_INFO[]? touchInfos);

    // ─────────────────────────────────────────────────────────────────────────
    // Raw Input / HID (for diagnostic device enumeration)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enumerates all raw input devices connected to the system.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputDeviceList(
        [Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    /// <summary>
    /// Retrieves information about a raw input device.
    /// uiCommand: RIDI_DEVICENAME (0x20000007) or RIDI_DEVICEINFO (0x2000000b).
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    // ─────────────────────────────────────────────────────────────────────────
    // Window helpers
    // ─────────────────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(
        IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>Gets the last Win32 error as a descriptive string.</summary>
    public static string GetLastErrorMessage()
    {
        int error = Marshal.GetLastWin32Error();
        return $"Win32 Error {error}: {new System.ComponentModel.Win32Exception(error).Message}";
    }

    // Window long indices
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE   = -16;

    // SetLayeredWindowAttributes flags
    public const uint LWA_COLORKEY = 0x1;
    public const uint LWA_ALPHA    = 0x2;
}
