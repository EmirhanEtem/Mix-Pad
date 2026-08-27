using System.Runtime.InteropServices;
using TouchpadGestureControl.NativeApi;

namespace TouchpadGestureControl.Input;

/// <summary>
/// Raw HID touch input provider using WM_INPUT (Windows Raw Input API).
/// Intercepts raw digitizer packets directly from hardware before OS gestures.
/// </summary>
public sealed class RawHidProvider : ITouchInputProvider
{
    public string ProviderName => "RawHID";
    public bool IsActive { get; private set; }
    public event EventHandler<TouchFrame>? FrameReceived;

    private IntPtr _hwnd;
    private readonly Dictionary<IntPtr, DeviceContext> _devices = new();
    private readonly object _deviceLock = new();
    private readonly Dictionary<ushort, TouchPoint> _activeContactsBySlot = new();

    private double _targetWidth = 1920;
    private double _targetHeight = 1080;

    public bool Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;

        try
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
            if (bounds.HasValue && bounds.Value.Width > 0 && bounds.Value.Height > 0)
            {
                _targetWidth = bounds.Value.Width;
                _targetHeight = bounds.Value.Height;
            }
        }
        catch
        {
            _targetWidth = 1920;
            _targetHeight = 1080;
        }

        var devicesToRegister = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = NativeConstants.HID_USAGE_PAGE_DIGITIZER,
                usUsage     = NativeConstants.HID_USAGE_TOUCHPAD, // 0x0005
                dwFlags     = NativeConstants.RIDEV_INPUTSINK,
                hwndTarget  = hwnd,
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = NativeConstants.HID_USAGE_PAGE_DIGITIZER,
                usUsage     = 0x0004, // Touch Screen
                dwFlags     = NativeConstants.RIDEV_INPUTSINK,
                hwndTarget  = hwnd,
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = NativeConstants.HID_USAGE_PAGE_DIGITIZER,
                usUsage     = 0x0001, // Digitizer
                dwFlags     = NativeConstants.RIDEV_INPUTSINK,
                hwndTarget  = hwnd,
            }
        };

        bool ok = RegisterRawInputDevices(
            devicesToRegister, (uint)devicesToRegister.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        if (ok)
        {
            IsActive = true;
            Log("[RawHID] Registered for WM_INPUT Digitizer/Touchpad. Intercept active.");
        }
        else
        {
            string err = NativeMethods.GetLastErrorMessage();
            Log($"[RawHID] RegisterRawInputDevices failed: {err}");
            IsActive = false;
        }

        return IsActive;
    }

    public void ProcessRawInput(IntPtr hRawInput)
    {
        uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        uint size = 0;

        GetRawInputData(hRawInput, NativeConstants.RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (size == 0) return;

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            uint got = GetRawInputData(hRawInput, NativeConstants.RID_INPUT, buf, ref size, headerSize);
            if (got == 0 || got == uint.MaxValue) return;

            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buf);
            if (header.dwType != NativeConstants.RIM_TYPEHID) return;

            int headerBytes = Marshal.SizeOf<RAWINPUTHEADER>();
            uint dwSizeHid = (uint)Marshal.ReadInt32(buf, headerBytes);
            uint dwCount   = (uint)Marshal.ReadInt32(buf, headerBytes + 4);
            if (dwSizeHid == 0 || dwCount == 0) return;

            IntPtr reportPtr = buf + headerBytes + 8;
            DeviceContext? ctx = GetOrBuildDeviceContext(header.hDevice);
            if (ctx == null) return;

            for (uint i = 0; i < dwCount; i++)
            {
                IntPtr rp = reportPtr + (int)(i * dwSizeHid);
                ParseReport(ctx, rp, dwSizeHid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private void ParseReport(DeviceContext ctx, IntPtr reportPtr, uint reportLen)
    {
        long now = Environment.TickCount64;

        // Iterate through ALL available contact slots (never truncate by contactCount
        // because active fingers may occupy non-contiguous collection slots e.g. slot 0, 2, 4)
        for (int i = 0; i < ctx.ContactCollections.Count; i++)
        {
            ushort col = ctx.ContactCollections[i];

            // Contact ID
            uint contactId = (uint)(i + 1);
            int idHr = HidP_GetUsageValue(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                NativeConstants.HID_USAGE_PAGE_DIGITIZER, col,
                NativeConstants.HID_USAGE_CONTACT_ID, out contactId,
                ctx.PreparsedData, reportPtr, reportLen);

            if (idHr != NativeConstants.HIDP_STATUS_SUCCESS)
            {
                contactId = (uint)(i + 1);
            }

            // Tip Switch (Value first, then Button)
            bool isDown = true;
            uint tipVal = 0;
            int tipHr = HidP_GetUsageValue(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                NativeConstants.HID_USAGE_PAGE_DIGITIZER, col,
                NativeConstants.HID_USAGE_TIP_SWITCH, out tipVal,
                ctx.PreparsedData, reportPtr, reportLen);

            if (tipHr == NativeConstants.HIDP_STATUS_SUCCESS)
            {
                isDown = tipVal != 0;
            }
            else
            {
                ushort[] usageList = new ushort[8];
                uint usageLen = (uint)usageList.Length;
                int btnHr = HidP_GetUsages(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                    NativeConstants.HID_USAGE_PAGE_DIGITIZER, col,
                    usageList, ref usageLen, ctx.PreparsedData, reportPtr, reportLen);

                if (btnHr == NativeConstants.HIDP_STATUS_SUCCESS && usageLen > 0)
                {
                    isDown = usageList.Take((int)usageLen).Contains(NativeConstants.HID_USAGE_TIP_SWITCH);
                }
            }

            // X Coordinate
            uint rawX = 0;
            int xHr = HidP_GetUsageValue(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                NativeConstants.HID_USAGE_PAGE_GENERIC, col,
                NativeConstants.HID_USAGE_X, out rawX,
                ctx.PreparsedData, reportPtr, reportLen);

            if (xHr != NativeConstants.HIDP_STATUS_SUCCESS)
            {
                HidP_GetUsageValue(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                    NativeConstants.HID_USAGE_PAGE_DIGITIZER, col,
                    NativeConstants.HID_USAGE_X, out rawX,
                    ctx.PreparsedData, reportPtr, reportLen);
            }

            // Y Coordinate
            uint rawY = 0;
            int yHr = HidP_GetUsageValue(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                NativeConstants.HID_USAGE_PAGE_GENERIC, col,
                NativeConstants.HID_USAGE_Y, out rawY,
                ctx.PreparsedData, reportPtr, reportLen);

            if (yHr != NativeConstants.HIDP_STATUS_SUCCESS)
            {
                HidP_GetUsageValue(NativeConstants.HIDP_REPORT_TYPE_INPUT,
                    NativeConstants.HID_USAGE_PAGE_DIGITIZER, col,
                    NativeConstants.HID_USAGE_Y, out rawY,
                    ctx.PreparsedData, reportPtr, reportLen);
            }

            if (isDown && (rawX > 0 || rawY > 0))
            {
                double normX = ctx.XMax > 0 ? (rawX / ctx.XMax) * _targetWidth : rawX;
                double normY = ctx.YMax > 0 ? (rawY / ctx.YMax) * _targetHeight : rawY;

                _activeContactsBySlot[col] = new TouchPoint(contactId, normX, normY, now);
            }
            else
            {
                _activeContactsBySlot.Remove(col);
            }
        }

        var pts = _activeContactsBySlot.Values.ToList();
        FrameReceived?.Invoke(this, new TouchFrame(pts, now));
    }

    private DeviceContext? GetOrBuildDeviceContext(IntPtr hDevice)
    {
        lock (_deviceLock)
        {
            if (_devices.TryGetValue(hDevice, out var existing)) return existing;

            uint ppSize = 0;
            GetRawInputDeviceInfo(hDevice, NativeConstants.RIDI_PREPARSEDDATA, IntPtr.Zero, ref ppSize);
            if (ppSize == 0) return null;

            IntPtr ppData = Marshal.AllocHGlobal((int)ppSize);
            GetRawInputDeviceInfo(hDevice, NativeConstants.RIDI_PREPARSEDDATA, ppData, ref ppSize);

            int capsHr = HidP_GetCaps(ppData, out HIDP_CAPS caps);
            if (capsHr != NativeConstants.HIDP_STATUS_SUCCESS)
            {
                Marshal.FreeHGlobal(ppData);
                return null;
            }

            ushort numValueCaps = caps.NumberInputValueCaps;
            if (numValueCaps == 0)
            {
                Marshal.FreeHGlobal(ppData);
                return null;
            }

            var valueCaps = new HIDP_VALUE_CAPS[numValueCaps];
            HidP_GetValueCaps(NativeConstants.HIDP_REPORT_TYPE_INPUT, valueCaps, ref numValueCaps, ppData);

            var xCols = new HashSet<ushort>();
            var yCols = new HashSet<ushort>();
            var idCols = new HashSet<ushort>();

            double xMax = 4000;
            double yMax = 3000;

            foreach (var vc in valueCaps)
            {
                if ((vc.UsagePage == NativeConstants.HID_USAGE_PAGE_GENERIC || vc.UsagePage == NativeConstants.HID_USAGE_PAGE_DIGITIZER) &&
                    vc.Usage == NativeConstants.HID_USAGE_X)
                {
                    xCols.Add(vc.LinkCollection);
                    if (vc.LogicalMax > 0) xMax = Math.Max(xMax, vc.LogicalMax);
                }
                if ((vc.UsagePage == NativeConstants.HID_USAGE_PAGE_GENERIC || vc.UsagePage == NativeConstants.HID_USAGE_PAGE_DIGITIZER) &&
                    vc.Usage == NativeConstants.HID_USAGE_Y)
                {
                    yCols.Add(vc.LinkCollection);
                    if (vc.LogicalMax > 0) yMax = Math.Max(yMax, vc.LogicalMax);
                }
                if (vc.UsagePage == NativeConstants.HID_USAGE_PAGE_DIGITIZER && vc.Usage == NativeConstants.HID_USAGE_CONTACT_ID)
                {
                    idCols.Add(vc.LinkCollection);
                }
            }

            var contactCols = xCols.Intersect(yCols).OrderBy(c => c).ToList();
            if (contactCols.Count == 0)
            {
                contactCols = xCols.Count > 0 ? xCols.OrderBy(c => c).ToList() : new List<ushort> { 0, 1, 2, 3, 4 };
            }

            var ctx = new DeviceContext
            {
                PreparsedData = ppData,
                ContactCollections = contactCols,
                XMax = xMax,
                YMax = yMax
            };

            _devices[hDevice] = ctx;
            Log($"[RawHID] Device 0x{hDevice:X} configured: {contactCols.Count} finger slots (XMax={xMax}, YMax={yMax})");
            return ctx;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS caps);

    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(int reportType, [Out] HIDP_VALUE_CAPS[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint usageValue, IntPtr preparsedData, IntPtr report, uint reportLength);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection, [Out] ushort[] usageList, ref uint usageLength, IntPtr preparsedData, IntPtr report, uint reportLength);

    private static void Log(string msg) => System.Diagnostics.Debug.WriteLine(msg);

    public void Dispose()
    {
        lock (_deviceLock)
        {
            foreach (var ctx in _devices.Values)
            {
                if (ctx.PreparsedData != IntPtr.Zero)
                    Marshal.FreeHGlobal(ctx.PreparsedData);
            }
            _devices.Clear();
        }
        IsActive = false;
    }

    private sealed class DeviceContext
    {
        public IntPtr PreparsedData { get; init; }
        public List<ushort> ContactCollections { get; init; } = new();
        public double XMax { get; init; }
        public double YMax { get; init; }
    }
}
