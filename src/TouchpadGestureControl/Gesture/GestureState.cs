namespace TouchpadGestureControl.Gesture;

/// <summary>
/// State machine states for the 3-finger triangle rotation gesture.
/// </summary>
public enum GestureState
{
    /// <summary>No gesture active. Waiting for 3 fingers.</summary>
    Idle,

    /// <summary>3 fingers detected and being tracked. Accumulating rotation.</summary>
    Tracking,

    /// <summary>Enough rotation accumulated — volume adjustment was applied.</summary>
    AdjustingVolume
}
