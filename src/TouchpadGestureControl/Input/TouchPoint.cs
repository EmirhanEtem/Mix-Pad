namespace TouchpadGestureControl.Input;

/// <summary>
/// Represents a single touch contact point at a moment in time.
/// Coordinates are in screen pixels (already converted from raw API values).
/// </summary>
/// <param name="Id">Unique contact identifier — stable across frames for the same finger.</param>
/// <param name="X">X screen coordinate in pixels.</param>
/// <param name="Y">Y screen coordinate in pixels (increases downward in Windows).</param>
/// <param name="TimestampMs">Milliseconds timestamp from the system.</param>
public sealed record TouchPoint(uint Id, double X, double Y, long TimestampMs)
{
    /// <summary>Returns a debug string for diagnostic display.</summary>
    public override string ToString() =>
        $"[id={Id} x={X:F1} y={Y:F1}]";
}
