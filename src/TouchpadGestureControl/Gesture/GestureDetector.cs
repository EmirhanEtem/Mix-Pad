using TouchpadGestureControl.Audio;
using TouchpadGestureControl.Input;

namespace TouchpadGestureControl.Gesture;

/// <summary>
/// Gesture state machine that processes touch frames and drives volume control.
/// Includes contact dropout grace periods and robust multi-finger tracking.
/// </summary>
public sealed class GestureDetector : IDisposable
{
    private readonly VolumeController _volume;

    public GestureState State { get; private set; } = GestureState.Idle;

    private Dictionary<uint, double> _prevAngles = new();
    private (double X, double Y) _prevCentroid;
    private double _accumulatedRotationDeg;
    private double _smoothedDeltaDeg;

    private double _volumeChangedThisSecond;
    private long _rateWindowStartMs;
    private long _lastThreeFingerTimeMs;

    public Action<DiagnosticSnapshot>? DiagnosticCallback { get; set; }

    public GestureDetector(VolumeController volumeController)
    {
        _volume = volumeController;
    }

    public void OnFrame(object? sender, TouchFrame frame)
    {
        long now = Environment.TickCount64;

        var snap = new DiagnosticSnapshot
        {
            Frame          = frame,
            StateBefore    = State,
            FingerCount    = frame.Count,
            Timestamp      = frame.TimestampMs,
        };

        if (frame.Count >= Settings.RequiredFingers)
        {
            _lastThreeFingerTimeMs = now;
            ProcessThreeOrMoreFingers(frame, snap);
        }
        else
        {
            // If we temporarily have fewer than 3 fingers, apply a 200ms grace period
            // before resetting accumulated rotation (prevents micro-dropouts during rapid rotation)
            long elapsedSince3Fingers = now - _lastThreeFingerTimeMs;
            if (elapsedSince3Fingers > 250 && State != GestureState.Idle)
            {
                Reset();
                snap.ResetReason = "Contacts released";
            }
            else if (State != GestureState.Idle)
            {
                snap.ResetReason = $"Awaiting contact ({250 - elapsedSince3Fingers}ms grace period)";
            }
        }

        snap.StateAfter           = State;
        snap.AccumulatedDegrees   = _accumulatedRotationDeg;
        snap.SmoothedDeltaDegrees = _smoothedDeltaDeg;
        snap.CurrentVolume        = _volume.GetCurrentVolume();

        DiagnosticCallback?.Invoke(snap);
    }

    private void ProcessThreeOrMoreFingers(TouchFrame frame, DiagnosticSnapshot snap)
    {
        // Take top 3 contacts
        var points = frame.Points.Take(3).ToList();
        var centroid = RotationCalculator.Centroid(points);
        var newAngles = RotationCalculator.ComputeAngles(points, centroid);

        snap.Centroid  = centroid;
        snap.NewAngles = newAngles;

        if (State == GestureState.Idle)
        {
            State = GestureState.Tracking;
            _prevAngles = newAngles;
            _prevCentroid = centroid;
            _accumulatedRotationDeg = 0;
            _smoothedDeltaDeg = 0;
            snap.ResetReason = "3 contacts detected — tracking active";
            return;
        }

        // ── TRACKING ────────────────────────────────────────────────────────
        double rawDeltaRad = RotationCalculator.MeanRotationDelta(_prevAngles, newAngles);
        double rawDeltaDeg = RotationCalculator.ToDegrees(rawDeltaRad);

        snap.RawDeltaDegrees = rawDeltaDeg;

        // Jitter filter
        if (Math.Abs(rawDeltaDeg) < Settings.JitterThresholdDegrees)
        {
            rawDeltaDeg = 0.0;
        }

        // EMA Smoothing
        double alpha = Settings.RotationSmoothingAlpha;
        _smoothedDeltaDeg = alpha * rawDeltaDeg + (1.0 - alpha) * _smoothedDeltaDeg;

        _accumulatedRotationDeg += _smoothedDeltaDeg;

        double threshold = Settings.RotationThresholdDegrees;
        if (Math.Abs(_accumulatedRotationDeg) >= threshold)
        {
            double steps = _accumulatedRotationDeg / Settings.DegreesPerVolumeStep;
            double volumeDelta = steps * Settings.VolumeStepSize;

            if (!Settings.ClockwiseIsVolumeUp)
                volumeDelta = -volumeDelta;

            // Rate limiting
            long now = Environment.TickCount64;
            if (now - _rateWindowStartMs >= 1000)
            {
                _volumeChangedThisSecond = 0;
                _rateWindowStartMs = now;
            }

            double maxChange = Settings.MaxVolumeChangePerSecond;
            double allowed = Math.Max(0.01, maxChange - _volumeChangedThisSecond);
            volumeDelta = Math.Clamp(volumeDelta, -allowed, allowed);

            if (Math.Abs(volumeDelta) > 0.001)
            {
                _volume.AdjustVolume((float)volumeDelta);
                _volumeChangedThisSecond += Math.Abs(volumeDelta);
                State = GestureState.AdjustingVolume;
                snap.VolumeApplied = volumeDelta;
            }

            // Remainder carry-over
            double remainder = _accumulatedRotationDeg - (steps * Settings.DegreesPerVolumeStep);
            _accumulatedRotationDeg = remainder;
        }
        else
        {
            if (State == GestureState.AdjustingVolume)
                State = GestureState.Tracking;
        }

        _prevAngles   = newAngles;
        _prevCentroid = centroid;
    }

    private void Reset()
    {
        State = GestureState.Idle;
        _prevAngles.Clear();
        _accumulatedRotationDeg = 0;
        _smoothedDeltaDeg       = 0;
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Snapshot of all intermediate values for a single processed frame.
/// </summary>
public sealed class DiagnosticSnapshot
{
    public TouchFrame? Frame { get; init; }
    public GestureState StateBefore { get; set; }
    public GestureState StateAfter { get; set; }
    public int FingerCount { get; init; }
    public long Timestamp { get; init; }
    public (double X, double Y) Centroid { get; set; }
    public Dictionary<uint, double>? NewAngles { get; set; }
    public double RawDeltaDegrees { get; set; }
    public double SmoothedDeltaDegrees { get; set; }
    public double AccumulatedDegrees { get; set; }
    public double VolumeApplied { get; set; }
    public float CurrentVolume { get; set; }
    public string? ResetReason { get; set; }
}
