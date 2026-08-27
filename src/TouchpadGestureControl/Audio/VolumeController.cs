using System.Runtime.InteropServices;

namespace TouchpadGestureControl.Audio;

/// <summary>
/// Controls the Windows default audio endpoint volume via Core Audio API (COM).
/// Thread-safe for read; writes are marshaled through a lock.
/// Instantiate once and dispose when done.
/// </summary>
public sealed class VolumeController : IDisposable
{
    // ─────────────────────────────────────────────────────────────────────────
    // COM objects (held alive for the lifetime of this class)
    // ─────────────────────────────────────────────────────────────────────────

    private IMMDeviceEnumerator?  _enumerator;
    private IMMDevice?            _device;
    private IAudioEndpointVolume? _endpointVolume;
    private static readonly Guid  _iidAudioEndpointVolume =
        new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private readonly object _lock = new();
    private bool _disposed;
    private bool _initialized;

    // ─────────────────────────────────────────────────────────────────────────
    // eRender=0, eMultimedia=1  (EDataFlow / ERole)
    // ─────────────────────────────────────────────────────────────────────────
    private const int EDataFlowRender     = 0;
    private const int ERoleMultimedia     = 1;
    private const uint CLSCTX_INPROC_SERVER = 1;

    public VolumeController()
    {
        TryInitialize();
    }

    /// <summary>Whether the Core Audio API is available and initialized.</summary>
    public bool IsAvailable => _initialized && !_disposed;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets the current master volume scalar (0.0 to 1.0).</summary>
    public float GetCurrentVolume()
    {
        if (!IsAvailable) return -1f;
        lock (_lock)
        {
            try
            {
                _endpointVolume!.GetMasterVolumeLevelScalar(out float vol);
                return vol;
            }
            catch (Exception ex)
            {
                Log($"[VolumeController] GetCurrentVolume failed: {ex.Message}");
                return -1f;
            }
        }
    }

    /// <summary>
    /// Sets the master volume to an absolute scalar value (0.0 = mute, 1.0 = max).
    /// Value is clamped to [0.0, 1.0].
    /// </summary>
    public void SetVolume(float level)
    {
        if (!IsAvailable) return;
        level = Math.Clamp(level, 0f, 1f);

        lock (_lock)
        {
            try
            {
                Guid empty = Guid.Empty;
                int hr = _endpointVolume!.SetMasterVolumeLevelScalar(level, ref empty);
                if (hr != 0)
                    Log($"[VolumeController] SetMasterVolumeLevelScalar returned HRESULT 0x{hr:X8}");
            }
            catch (Exception ex)
            {
                Log($"[VolumeController] SetVolume failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Adjusts the master volume by a signed delta (e.g. +0.02 = +2%).
    /// The resulting value is clamped to [0.0, 1.0].
    /// </summary>
    public void AdjustVolume(float delta)
    {
        if (!IsAvailable) return;
        float current = GetCurrentVolume();
        if (current < 0) return;
        SetVolume(current + delta);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────────

    private void TryInitialize()
    {
        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

            int hr = _enumerator.GetDefaultAudioEndpoint(
                EDataFlowRender, ERoleMultimedia, out _device);

            if (hr != 0 || _device == null)
            {
                Log($"[VolumeController] GetDefaultAudioEndpoint failed. HRESULT: 0x{hr:X8}");
                return;
            }

            Guid iid = _iidAudioEndpointVolume;
            hr = _device.Activate(ref iid, CLSCTX_INPROC_SERVER,
                IntPtr.Zero, out object volObj);

            if (hr != 0 || volObj == null)
            {
                Log($"[VolumeController] Activate IAudioEndpointVolume failed. HRESULT: 0x{hr:X8}");
                return;
            }

            _endpointVolume = (IAudioEndpointVolume)volObj;
            _initialized = true;
            Log("[VolumeController] Core Audio initialized successfully.");
        }
        catch (Exception ex)
        {
            Log($"[VolumeController] Initialization exception: {ex.Message}");
            _initialized = false;
        }
    }

    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IDisposable
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            if (_endpointVolume != null)
            {
                Marshal.ReleaseComObject(_endpointVolume);
                _endpointVolume = null;
            }
            if (_device != null)
            {
                Marshal.ReleaseComObject(_device);
                _device = null;
            }
            if (_enumerator != null)
            {
                Marshal.ReleaseComObject(_enumerator);
                _enumerator = null;
            }
        }
    }
}
