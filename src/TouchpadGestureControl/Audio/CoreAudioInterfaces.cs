using System.Runtime.InteropServices;

namespace TouchpadGestureControl.Audio;

// ─────────────────────────────────────────────────────────────────────────────
// Windows Core Audio COM Interface Declarations
// IID and CLSID values from Windows SDK / mmdeviceapi.h + endpointvolume.h
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// IMMDeviceEnumerator — enumerates audio endpoint devices.
/// IID: {A95664D2-9614-4F35-A746-DE8DB63617E6}
/// </summary>
[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(
        int dataFlow,
        int dwStateMask,
        out IMMDeviceCollection ppDevices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(
        int dataFlow,
        int role,
        out IMMDevice ppEndpoint);

    [PreserveSig]
    int GetDevice(
        [MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
        out IMMDevice ppDevice);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr pClient);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

/// <summary>
/// IMMDeviceCollection — collection of audio endpoint devices.
/// IID: {0BD7A1BE-7A1A-44DB-8397-CC5392387B5E}
/// </summary>
[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out int pcDevices);

    [PreserveSig]
    int Item(int nDevice, out IMMDevice ppDevice);
}

/// <summary>
/// IMMDevice — represents an audio endpoint device.
/// IID: {D666063F-1587-4E43-81F1-B948E807363F}
/// </summary>
[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(
        ref Guid iid,
        uint dwClsCtx,
        IntPtr pActivationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

    [PreserveSig]
    int OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

    [PreserveSig]
    int GetState(out int pdwState);
}

/// <summary>
/// IAudioEndpointVolume — controls volume of an audio endpoint device.
/// IID: {5CDF2C82-841E-4546-9722-0CF74078229A}
///
/// vtable order must exactly match Windows SDK definition.
/// Methods are declared in Windows SDK vtable order (0-indexed):
///   0: RegisterControlChangeNotify
///   1: UnregisterControlChangeNotify
///   2: GetChannelCount
///   3: SetMasterVolumeLevel       (dB)
///   4: SetMasterVolumeLevelScalar (0.0-1.0) ← we use this
///   5: GetMasterVolumeLevel       (dB)
///   6: GetMasterVolumeLevelScalar (0.0-1.0) ← we use this
///   7-17: channel volume, mute, step, range (declared as placeholders)
/// </summary>
[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
    [PreserveSig] int GetChannelCount(out int pnChannelCount);

    // dB-based master volume (we don't use these directly)
    [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);

    // Per-channel (placeholders to maintain vtable alignment)
    [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

    // Mute
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);

    // Step
    [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
    [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
    [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
    [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
    [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

/// <summary>
/// COM co-class for MMDeviceEnumerator.
/// CLSID: {BCDE0395-E52F-467C-8E3D-C4579291692E}
/// </summary>
[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
[ClassInterface(ClassInterfaceType.None)]
internal class MMDeviceEnumeratorComObject { }
