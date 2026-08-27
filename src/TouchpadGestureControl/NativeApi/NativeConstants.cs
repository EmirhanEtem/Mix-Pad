namespace TouchpadGestureControl.NativeApi;

/// <summary>
/// Windows API constants for touch input, pointer messages, and window styles.
/// </summary>
internal static class NativeConstants
{
    // ──────────────────────────────────────────────────────────────
    // Window Messages
    // ──────────────────────────────────────────────────────────────

    /// <summary>WM_TOUCH: Sent to a window when one or more touch points are detected.</summary>
    public const int WM_TOUCH = 0x0240;

    /// <summary>WM_INPUT: Raw input from HID device — arrives BEFORE Windows gesture recognition.</summary>
    public const int WM_INPUT = 0x00FF;

    /// <summary>WM_POINTERUPDATE: Pointer moved/updated while in contact.</summary>
    public const int WM_POINTERUPDATE = 0x0245;

    /// <summary>WM_POINTERDOWN: A new pointer made contact.</summary>
    public const int WM_POINTERDOWN = 0x0246;

    /// <summary>WM_POINTERUP: Pointer lifted from contact.</summary>
    public const int WM_POINTERUP = 0x0247;

    /// <summary>WM_POINTERCAPTURECHANGED: Pointer capture changed.</summary>
    public const int WM_POINTERCAPTURECHANGED = 0x024C;

    // ──────────────────────────────────────────────────────────────
    // RegisterTouchWindow flags
    // ──────────────────────────────────────────────────────────────

    /// <summary>TWF_FINETOUCH: Fine-grained touch for high resolution.</summary>
    public const uint TWF_FINETOUCH = 0x00000001;

    /// <summary>TWF_WANTPALM: Disables palm rejection. Useful to receive all contacts.</summary>
    public const uint TWF_WANTPALM = 0x00000002;

    // ──────────────────────────────────────────────────────────────
    // Pointer Input Types (POINTER_INPUT_TYPE)
    // ──────────────────────────────────────────────────────────────

    public const int PT_POINTER = 0x00000001;
    public const int PT_TOUCH   = 0x00000002;
    public const int PT_PEN     = 0x00000003;
    public const int PT_MOUSE   = 0x00000004;

    // ──────────────────────────────────────────────────────────────
    // Pointer Flags (POINTER_FLAGS)
    // ──────────────────────────────────────────────────────────────

    public const uint POINTER_FLAG_NONE       = 0x00000000;
    public const uint POINTER_FLAG_NEW        = 0x00000001;
    public const uint POINTER_FLAG_INRANGE    = 0x00000002;
    public const uint POINTER_FLAG_INCONTACT  = 0x00000004;
    public const uint POINTER_FLAG_PRIMARY    = 0x00002000;
    public const uint POINTER_FLAG_CONFIDENCE = 0x00004000;
    public const uint POINTER_FLAG_CANCELED   = 0x00008000;
    public const uint POINTER_FLAG_DOWN       = 0x00010000;
    public const uint POINTER_FLAG_UPDATE     = 0x00020000;
    public const uint POINTER_FLAG_UP         = 0x00040000;

    // ──────────────────────────────────────────────────────────────
    // Touch Event Flags (TOUCHINPUT.dwFlags)
    // ──────────────────────────────────────────────────────────────

    public const int TOUCHEVENTF_MOVE   = 0x0001;
    public const int TOUCHEVENTF_DOWN   = 0x0002;
    public const int TOUCHEVENTF_UP     = 0x0004;
    public const int TOUCHEVENTF_INRANGE = 0x0008;
    public const int TOUCHEVENTF_PRIMARY = 0x0010;
    public const int TOUCHEVENTF_NOCOALESCE = 0x0020;
    public const int TOUCHEVENTF_PALM   = 0x0080;

    // ──────────────────────────────────────────────────────────────
    // Window Styles
    // ──────────────────────────────────────────────────────────────

    public const int WS_OVERLAPPED    = 0x00000000;
    public const int WS_POPUP         = unchecked((int)0x80000000);
    public const int WS_VISIBLE       = 0x10000000;

    public const int WS_EX_TOOLWINDOW  = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED     = 0x00080000;
    public const int WS_EX_NOACTIVATE  = 0x08000000;
    public const int WS_EX_TOPMOST     = 0x00000008;
    public const int WS_EX_APPWINDOW   = 0x00040000;

    // ──────────────────────────────────────────────────────────────
    // ShowWindow commands
    // ──────────────────────────────────────────────────────────────

    public const int SW_HIDE     = 0;
    public const int SW_SHOW     = 5;
    public const int SW_MINIMIZE = 6;

    // ──────────────────────────────────────────────────────────────
    // Raw Input / WM_INPUT flags
    // ──────────────────────────────────────────────────────────────

    /// <summary>RIDEV_INPUTSINK: Receive raw input even when not in the foreground (background capture).</summary>
    public const uint RIDEV_INPUTSINK = 0x00000100;

    /// <summary>RIDEV_REMOVE: Remove this device class from the raw input list.</summary>
    public const uint RIDEV_REMOVE = 0x00000001;

    /// <summary>RID_INPUT: Get the raw input data (used with GetRawInputData).</summary>
    public const uint RID_INPUT = 0x10000003;

    /// <summary>RIDI_PREPARSEDDATA: Get the preparsed HID data for a device.</summary>
    public const uint RIDI_PREPARSEDDATA = 0x20000005;

    /// <summary>RIDI_DEVICENAME: Get the device name string.</summary>
    public const uint RIDI_DEVICENAME = 0x20000007;

    /// <summary>RIDI_DEVICEINFO: Get the RID_DEVICE_INFO struct.</summary>
    public const uint RIDI_DEVICEINFO = 0x2000000b;

    /// <summary>RIM_TYPEHID: Raw input is from a HID device (not mouse/keyboard).</summary>
    public const uint RIM_TYPEHID = 2;

    // ──────────────────────────────────────────────────────────────
    // HID Usage Pages and Usages for Precision Touchpad
    // ──────────────────────────────────────────────────────────────

    public const ushort HID_USAGE_PAGE_GENERIC   = 0x0001; // Generic Desktop
    public const ushort HID_USAGE_PAGE_DIGITIZER = 0x000D; // Digitizer

    public const ushort HID_USAGE_TOUCHPAD        = 0x0005; // TouchPad (top-level collection)
    public const ushort HID_USAGE_FINGER          = 0x0022; // Finger collection
    public const ushort HID_USAGE_TIP_SWITCH      = 0x0042; // Tip Switch (finger down)
    public const ushort HID_USAGE_CONFIDENCE      = 0x0047; // Confidence (valid contact)
    public const ushort HID_USAGE_CONTACT_ID      = 0x0051; // Contact Identifier
    public const ushort HID_USAGE_CONTACT_COUNT   = 0x0054; // Contact Count
    public const ushort HID_USAGE_X               = 0x0030; // X (Generic Desktop)
    public const ushort HID_USAGE_Y               = 0x0031; // Y (Generic Desktop)

    // HidP return codes
    public const int HIDP_STATUS_SUCCESS          = 0x00110000;
    public const int HIDP_STATUS_INCOMPATIBLE_REPORT_ID = unchecked((int)0xC0110010);
    public const int HIDP_REPORT_TYPE_INPUT       = 0;


    // ──────────────────────────────────────────────────────────────
    // WParam helpers for WM_POINTER
    // ──────────────────────────────────────────────────────────────

    /// <summary>Extract pointer ID from WM_POINTER wParam.</summary>
    public static uint GET_POINTER_ID(IntPtr wParam) => (uint)((int)wParam & 0xFFFF);

    /// <summary>Extract pointer flags from WM_TOUCH wParam (count and handle).</summary>
    public static int LOWORD(IntPtr value) => (int)value & 0xFFFF;
    public static int HIWORD(IntPtr value) => ((int)value >> 16) & 0xFFFF;
}
