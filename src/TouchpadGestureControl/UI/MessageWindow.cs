using TouchpadGestureControl.Input;
using TouchpadGestureControl.NativeApi;
using System.Runtime.InteropServices;

namespace TouchpadGestureControl.UI;

/// <summary>
/// A minimal native window (NativeWindow subclass) that:
///   1. Registers for touch/pointer/HID input on creation.
///   2. Receives WM_INPUT, WM_POINTER, and WM_TOUCH messages in WndProc.
///   3. Dispatches messages to the active ITouchInputProvider.
///
/// INPUT STRATEGY:
///   - Primary: RawHidProvider (WM_INPUT with RIDEV_INPUTSINK) — captures raw touch data
///     BEFORE Windows 10/11 handles gestures (Alt-Tab, swipe down to desktop, etc.)
///   - Secondary: WmPointerProvider (RegisterPointerInputTarget)
///   - Fallback: WmTouchProvider (with transparent click-through overlay)
/// </summary>
public sealed class MessageWindow : NativeWindow, IDisposable
{
    private ITouchInputProvider? _provider;
    private RawHidProvider?      _rawHidProvider;
    private WmPointerProvider?  _wmPointerProvider;
    private OverlayForm?         _overlay;

    public ITouchInputProvider? ActiveProvider => _provider;

    /// <summary>Called by the provider on every touch frame.</summary>
    public event EventHandler<TouchFrame>? FrameReceived;

    /// <summary>Log messages for the diagnostic window.</summary>
    public event Action<string>? LogMessage;

    public void Initialize()
    {
        var cp = new CreateParams
        {
            Caption   = "TGC_MessageWindow",
            Style     = NativeConstants.WS_OVERLAPPED,
            ExStyle   = NativeConstants.WS_EX_TOOLWINDOW | NativeConstants.WS_EX_NOACTIVATE,
            Width     = 1,
            Height    = 1,
            X         = -32000,
            Y         = -32000,
        };
        CreateHandle(cp);
        NativeMethods.ShowWindow(Handle, NativeConstants.SW_HIDE);

        Log($"MessageWindow created. HWND=0x{Handle:X}");

        TryStartProvider();
    }

    private void TryStartProvider()
    {
        string pref = Settings.PreferredProvider;

        if (pref == "WmTouch")
        {
            StartWmTouch();
            return;
        }

        if (pref == "WmPointer")
        {
            StartWmPointer();
            return;
        }

        // Auto / RawHID mode:
        // Try RawHID first because it intercepts input before Windows handles OS gestures!
        _rawHidProvider = new RawHidProvider();
        if (_rawHidProvider.Initialize(Handle))
        {
            SetProvider(_rawHidProvider);
            Log($"✅ Input provider: {_rawHidProvider.ProviderName} (Direct HID Digitizer — Bypasses OS Gestures)");

            // Also initialize WmPointer in the background as backup receiver
            _wmPointerProvider = new WmPointerProvider();
            _wmPointerProvider.Initialize(Handle);
            return;
        }

        Log("⚠️  RawHidProvider could not register — trying WmPointerProvider.");
        StartWmPointer();
    }

    private void StartWmPointer()
    {
        _wmPointerProvider ??= new WmPointerProvider();
        if (_wmPointerProvider.Initialize(Handle))
        {
            SetProvider(_wmPointerProvider);
            Log($"✅ Input provider: {_wmPointerProvider.ProviderName} (RegisterPointerInputTarget succeeded)");
            return;
        }

        Log("⚠️  WmPointerProvider failed — falling back to WmTouchProvider with overlay.");
        StartWmTouch();
    }

    private void StartWmTouch()
    {
        _overlay = new OverlayForm();
        var touchProvider = new WmTouchProvider();

        if (touchProvider.Initialize(_overlay.Handle))
        {
            touchProvider.FrameReceived += (s, f) => FrameReceived?.Invoke(s, f);
            _overlay.TouchProvider = touchProvider;
            _provider = touchProvider;
            _overlay.Show();
            Log($"✅ Input provider: {touchProvider.ProviderName} (overlay window)");
        }
        else
        {
            Log("❌ WmTouchProvider also failed. Touch input is unavailable on this device/configuration.");
            touchProvider.Dispose();
        }
    }

    private void SetProvider(ITouchInputProvider provider)
    {
        _provider = provider;
        _provider.FrameReceived += (s, f) => FrameReceived?.Invoke(s, f);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeConstants.WM_INPUT:
                if (_rawHidProvider != null && _rawHidProvider.IsActive)
                {
                    _rawHidProvider.ProcessRawInput(m.LParam);
                }
                break;

            case NativeConstants.WM_POINTERDOWN:
            case NativeConstants.WM_POINTERUPDATE:
            case NativeConstants.WM_POINTERUP:
            case NativeConstants.WM_POINTERCAPTURECHANGED:
                if (_provider is WmPointerProvider pp)
                    pp.ProcessMessage(m.Msg, m.WParam, m.LParam);
                else if (_wmPointerProvider != null && _wmPointerProvider.IsActive && _provider is not RawHidProvider)
                    _wmPointerProvider.ProcessMessage(m.Msg, m.WParam, m.LParam);
                break;
        }

        base.WndProc(ref m);
    }

    public static List<string> EnumerateHidDevices()
    {
        var result = new List<string>();
        try
        {
            uint count = 0;
            uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
            NativeMethods.GetRawInputDeviceList(null, ref count, structSize);

            if (count == 0)
            {
                result.Add("No raw input devices found.");
                return result;
            }

            var devices = new RAWINPUTDEVICELIST[count];
            NativeMethods.GetRawInputDeviceList(devices, ref count, structSize);

            foreach (var dev in devices)
            {
                uint nameLen = 0;
                NativeMethods.GetRawInputDeviceInfo(dev.hDevice,
                    NativeConstants.RIDI_DEVICENAME, IntPtr.Zero, ref nameLen);

                string name = "(unknown)";
                if (nameLen > 0)
                {
                    IntPtr namePtr = Marshal.AllocHGlobal((int)(nameLen * 2));
                    try
                    {
                        NativeMethods.GetRawInputDeviceInfo(dev.hDevice,
                            NativeConstants.RIDI_DEVICENAME, namePtr, ref nameLen);
                        name = Marshal.PtrToStringUni(namePtr) ?? "(null)";
                    }
                    finally { Marshal.FreeHGlobal(namePtr); }
                }

                uint infoSize = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
                IntPtr infoPtr = Marshal.AllocHGlobal((int)infoSize);
                try
                {
                    Marshal.WriteInt32(infoPtr, 0, (int)infoSize);
                    NativeMethods.GetRawInputDeviceInfo(dev.hDevice,
                        NativeConstants.RIDI_DEVICEINFO, infoPtr, ref infoSize);
                    var info = Marshal.PtrToStructure<RID_DEVICE_INFO>(infoPtr);

                    string typeLabel = dev.dwType switch
                    {
                        0 => "MOUSE",
                        1 => "KEYBOARD",
                        2 => "HID",
                        _ => $"TYPE({dev.dwType})"
                    };

                    string detail = dev.dwType == 2
                        ? $"[HID UsagePage=0x{info.hid.usUsagePage:X4} Usage=0x{info.hid.usUsage:X4} " +
                          $"VID=0x{info.hid.dwVendorId:X4} PID=0x{info.hid.dwProductId:X4}]"
                        : "";

                    bool isTouchpad = dev.dwType == 2
                        && info.hid.usUsagePage == 0x000D
                        && (info.hid.usUsage == 0x0005 || info.hid.usUsage == 0x0004 || info.hid.usUsage == 0x0001);

                    string line = $"[{typeLabel}]{(isTouchpad ? " ★TOUCHPAD / DIGITIZER★" : "")} {detail} {name}";
                    result.Add(line);
                }
                finally { Marshal.FreeHGlobal(infoPtr); }
            }
        }
        catch (Exception ex)
        {
            result.Add($"Error enumerating devices: {ex.Message}");
        }
        return result;
    }

    private void Log(string msg) => LogMessage?.Invoke(msg);

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _rawHidProvider?.Dispose();
        _wmPointerProvider?.Dispose();
        _provider?.Dispose();
        _overlay?.Dispose();

        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}

internal sealed class OverlayForm : Form
{
    internal WmTouchProvider? TouchProvider { get; set; }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState     = FormWindowState.Maximized;
        TopMost         = true;
        ShowInTaskbar   = false;
        BackColor       = System.Drawing.Color.Black;
        Opacity         = Settings.OverlayOpacity;

        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeConstants.WS_EX_TRANSPARENT
                        | NativeConstants.WS_EX_LAYERED
                        | NativeConstants.WS_EX_NOACTIVATE
                        | NativeConstants.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeConstants.WM_TOUCH && TouchProvider != null)
        {
            TouchProvider.ProcessMessage(m.WParam, m.LParam);
            return;
        }
        base.WndProc(ref m);
    }
}
