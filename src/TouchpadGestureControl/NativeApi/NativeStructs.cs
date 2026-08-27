using System.Runtime.InteropServices;

namespace TouchpadGestureControl.NativeApi;

// ─────────────────────────────────────────────────────────────────────────────
// Basic geometry
// ─────────────────────────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
}

// ─────────────────────────────────────────────────────────────────────────────
// WM_POINTER structures
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// POINTER_INFO — Common pointer information shared by all pointer types.
/// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-pointer_info
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINTER_INFO
{
    public int    pointerType;
    public uint   pointerId;
    public uint   frameId;
    public uint   pointerFlags;
    public IntPtr sourceDevice;
    public IntPtr hwndTarget;
    public POINT  ptPixelLocation;
    public POINT  ptHimetricLocation;
    public POINT  ptPixelLocationRaw;
    public POINT  ptHimetricLocationRaw;
    public uint   dwTime;
    public uint   historyCount;
    public int    InputData;
    public uint   dwKeyStates;
    public ulong  PerformanceCount;
    public int    ButtonChangeType;
}

/// <summary>
/// POINTER_TOUCH_INFO — Extended information for touch contacts.
/// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-pointer_touch_info
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINTER_TOUCH_INFO
{
    public POINTER_INFO pointerInfo;
    public uint         touchFlags;
    public uint         touchMask;
    public RECT         rcContact;
    public RECT         rcContactRaw;
    public uint         orientation;
    public uint         pressure;
}

// ─────────────────────────────────────────────────────────────────────────────
// WM_TOUCH structures
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// TOUCHINPUT — Represents a single touch point from a WM_TOUCH message.
/// Coordinates are in hundredths of a pixel (divide by 100 to get pixels).
/// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-touchinput
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TOUCHINPUT
{
    /// <summary>X coordinate in hundredths of a pixel.</summary>
    public int    x;
    /// <summary>Y coordinate in hundredths of a pixel.</summary>
    public int    y;
    public IntPtr hSource;
    /// <summary>Unique touch contact ID (consistent across frames for the same finger).</summary>
    public uint   dwID;
    /// <summary>Flags: TOUCHEVENTF_MOVE | TOUCHEVENTF_DOWN | TOUCHEVENTF_UP etc.</summary>
    public uint   dwFlags;
    public uint   dwMask;
    public uint   dwTime;
    public IntPtr dwExtraInfo;
    public uint   cxContact;
    public uint   cyContact;
}

// ─────────────────────────────────────────────────────────────────────────────
// Raw Input structures (for HID diagnostic)
// ─────────────────────────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICELIST
{
    public IntPtr hDevice;
    public uint   dwType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_HID
{
    public uint dwVendorId;
    public uint dwProductId;
    public uint dwVersionNumber;
    public ushort usUsagePage;
    public ushort usUsage;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_MOUSE
{
    public uint dwId;
    public uint dwNumberOfButtons;
    public uint dwSampleRate;
    public bool fHasHorizontalWheel;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_KEYBOARD
{
    public uint dwType;
    public uint dwSubType;
    public uint dwKeyboardMode;
    public uint dwNumberOfFunctionKeys;
    public uint dwNumberOfIndicators;
    public uint dwNumberOfKeysTotal;
}

[StructLayout(LayoutKind.Explicit)]
internal struct RID_DEVICE_INFO
{
    [FieldOffset(0)]  public uint                 cbSize;
    [FieldOffset(4)]  public uint                 dwType;
    [FieldOffset(8)]  public RID_DEVICE_INFO_MOUSE    mouse;
    [FieldOffset(8)]  public RID_DEVICE_INFO_KEYBOARD keyboard;
    [FieldOffset(8)]  public RID_DEVICE_INFO_HID      hid;
}

// ─────────────────────────────────────────────────────────────────────────────
// Raw Input registration
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Device registration for RegisterRawInputDevices.
/// Tells Windows which HID device class to receive WM_INPUT messages from.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICE
{
    public ushort usUsagePage;   // HID Usage Page (0x000D = Digitizer)
    public ushort usUsage;       // HID Usage (0x0005 = TouchPad)
    public uint   dwFlags;       // RIDEV_INPUTSINK etc.
    public IntPtr hwndTarget;    // Target window HWND
}

/// <summary>Raw input message header. Size differs between 32-bit (16) and 64-bit (24).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTHEADER
{
    public uint   dwType;      // RIM_TYPEHID = 2
    public uint   dwSize;      // Total size of the RAWINPUT structure
    public IntPtr hDevice;     // Device handle
    public IntPtr wParam;      // wParam from WM_INPUT message
}

// ─────────────────────────────────────────────────────────────────────────────
// HID Parsing (hidpi.h)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// HIDP_CAPS — capabilities of a HID preparsed data block.
/// From hidpi.h. Total size is 64 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HIDP_CAPS
{
    public ushort Usage;
    public ushort UsagePage;
    public ushort InputReportByteLength;
    public ushort OutputReportByteLength;
    public ushort FeatureReportByteLength;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    public ushort[] Reserved;
    public ushort NumberLinkCollectionNodes;
    public ushort NumberInputButtonCaps;
    public ushort NumberInputValueCaps;
    public ushort NumberInputDataIndices;
    public ushort NumberOutputButtonCaps;
    public ushort NumberOutputValueCaps;
    public ushort NumberOutputDataIndices;
    public ushort NumberFeatureButtonCaps;
    public ushort NumberFeatureValueCaps;
    public ushort NumberFeatureDataIndices;
}

/// <summary>
/// HIDP_VALUE_CAPS — describes a value capability for a HID report.
/// Contains range/location/units info for a single Usage in a report.
/// Size: 72 bytes (all primitive types, no pointers).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HIDP_VALUE_CAPS
{
    public ushort UsagePage;
    public byte   ReportID;
    [MarshalAs(UnmanagedType.U1)] public bool IsAlias;
    public ushort BitField;
    public ushort LinkCollection;   // Which collection (contact slot) this belongs to
    public ushort LinkUsage;
    public ushort LinkUsagePage;
    [MarshalAs(UnmanagedType.U1)] public bool IsRange;
    [MarshalAs(UnmanagedType.U1)] public bool IsStringRange;
    [MarshalAs(UnmanagedType.U1)] public bool IsDesignatorRange;
    [MarshalAs(UnmanagedType.U1)] public bool IsAbsolute;
    [MarshalAs(UnmanagedType.U1)] public bool HasNull;
    public byte   Reserved;
    public ushort BitSize;
    public ushort ReportCount;
    public ushort Reserved2_0, Reserved2_1, Reserved2_2, Reserved2_3, Reserved2_4;
    public uint   UnitsExp;
    public uint   Units;
    public int    LogicalMin;
    public int    LogicalMax;
    public int    PhysicalMin;
    public int    PhysicalMax;
    // Union NotRange: Usage, Reserved, StringIndex, Reserved, DesignatorIndex, Reserved, DataIndex, Reserved
    public ushort UsageMin;  // = Usage when IsRange=false
    public ushort UsageMax;
    public ushort StringMin;
    public ushort StringMax;
    public ushort DesignatorMin;
    public ushort DesignatorMax;
    public ushort DataIndexMin;
    public ushort DataIndexMax;

    /// <summary>The single Usage when IsRange=false.</summary>
    public ushort Usage => UsageMin;
    /// <summary>The single DataIndex when IsRange=false.</summary>
    public ushort DataIndex => DataIndexMin;
}
