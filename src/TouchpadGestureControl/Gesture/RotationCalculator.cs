using TouchpadGestureControl.Input;

namespace TouchpadGestureControl.Gesture;

/// <summary>
/// Pure math helpers for 3-finger rotation detection.
/// Robust against unstable contact IDs, arbitrary finger arrangements, and rapid movement.
/// </summary>
public static class RotationCalculator
{
    /// <summary>
    /// Computes the centroid (center of mass) of a set of 2D touch points.
    /// </summary>
    public static (double X, double Y) Centroid(IReadOnlyList<TouchPoint> points)
    {
        if (points.Count == 0) return (0, 0);
        double x = points.Average(p => p.X);
        double y = points.Average(p => p.Y);
        return (x, y);
    }

    /// <summary>
    /// Computes the signed area of a triangle defined by three points.
    /// </summary>
    public static double SignedTriangleArea(TouchPoint a, TouchPoint b, TouchPoint c)
    {
        return 0.5 * ((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y));
    }

    /// <summary>
    /// Computes the angle of each point relative to the centroid in radians [-π, π].
    /// </summary>
    public static Dictionary<uint, double> ComputeAngles(
        IReadOnlyList<TouchPoint> points,
        (double X, double Y) centroid)
    {
        var result = new Dictionary<uint, double>(points.Count);
        foreach (var p in points)
        {
            double dx = p.X - centroid.X;
            double dy = p.Y - centroid.Y;
            result[p.Id] = Math.Atan2(dy, dx);
        }
        return result;
    }

    /// <summary>
    /// Calculates the mean angular change between two frames.
    /// Uses ID-based matching first; falls back to circular angular slot matching
    /// if the hardware does not report stable contact IDs.
    /// </summary>
    public static double MeanRotationDelta(
        Dictionary<uint, double> prevAngles,
        Dictionary<uint, double> newAngles)
    {
        if (prevAngles.Count == 0 || newAngles.Count == 0) return 0.0;

        // 1. Try ID-based matching
        var deltas = new List<double>(capacity: 3);
        foreach (var (id, newAngle) in newAngles)
        {
            if (prevAngles.TryGetValue(id, out double prevAngle))
            {
                deltas.Add(WrapAngle(newAngle - prevAngle));
            }
        }

        if (deltas.Count >= 2)
        {
            return deltas.Average();
        }

        // 2. Fallback: Proximity / Circular Slot Matching (when contact IDs are unstable)
        var prevList = prevAngles.Values.OrderBy(a => a).ToList();
        var newList  = newAngles.Values.OrderBy(a => a).ToList();

        if (prevList.Count >= 2 && newList.Count >= 2)
        {
            int count = Math.Min(prevList.Count, newList.Count);
            var slotDeltas = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                slotDeltas.Add(WrapAngle(newList[i] - prevList[i]));
            }
            return slotDeltas.Average();
        }

        return 0.0;
    }

    /// <summary>
    /// Wraps an angle to the range [-π, π] using atan2(sin, cos).
    /// </summary>
    public static double WrapAngle(double radians)
    {
        return Math.Atan2(Math.Sin(radians), Math.Cos(radians));
    }

    /// <summary>
    /// Validates whether touch contacts can be tracked.
    /// Accepts any 3 fingers on the surface.
    /// </summary>
    public static bool IsValidTriangle(IReadOnlyList<TouchPoint> points)
    {
        return points != null && points.Count >= 3;
    }

    /// <summary>Euclidean distance between two touch points.</summary>
    public static double Distance(TouchPoint a, TouchPoint b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Converts radians to degrees.</summary>
    public static double ToDegrees(double radians) => radians * (180.0 / Math.PI);

    /// <summary>Converts degrees to radians.</summary>
    public static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
