namespace TouchpadGestureControl.Input;

/// <summary>
/// A snapshot of all simultaneous touch contacts at a single moment.
/// Analogous to a "frame" of multi-touch data.
/// </summary>
/// <param name="Points">All active contact points in this frame.</param>
/// <param name="TimestampMs">Capture time in milliseconds.</param>
public sealed record TouchFrame(IReadOnlyList<TouchPoint> Points, long TimestampMs)
{
    /// <summary>Number of active contacts in this frame.</summary>
    public int Count => Points.Count;

    /// <summary>True if exactly 3 fingers are in contact.</summary>
    public bool IsThreeFinger => Points.Count == Settings.RequiredFingers;

    /// <summary>Returns a debug string for diagnostic display.</summary>
    public override string ToString() =>
        $"Frame(t={TimestampMs}ms, contacts={Count}: {string.Join(", ", Points)})";
}
