using TouchpadGestureControl;
using TouchpadGestureControl.Gesture;
using TouchpadGestureControl.Input;
using Xunit;

namespace GestureTests;

/// <summary>
/// Unit tests for the rotation detection algorithm.
/// All tests use synthetic coordinates — no Windows APIs or hardware required.
///
/// Coordinate convention (matches Windows screen coords):
///   X increases rightward, Y increases DOWNWARD.
///   Clockwise rotation (visually on screen) produces POSITIVE mean delta.
/// </summary>
public sealed class RotationAlgorithmTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates 3 touch points forming an equilateral triangle centered at (cx, cy)
    /// with given radius, rotated by 'angleDeg' degrees.
    /// The three vertices are at 120° intervals starting from angleDeg.
    /// </summary>
    private static List<TouchPoint> Triangle(double cx, double cy, double radius, double angleDeg)
    {
        var pts = new List<TouchPoint>();
        for (int i = 0; i < 3; i++)
        {
            double a = (angleDeg + i * 120.0) * Math.PI / 180.0;
            double x = cx + radius * Math.Cos(a);
            double y = cy + radius * Math.Sin(a);
            pts.Add(new TouchPoint((uint)i, x, y, 0));
        }
        return pts;
    }

    /// <summary>
    /// Rotates the triangle by stepDeg N times and returns the sum of all deltas.
    /// </summary>
    private static double AccumulateRotation(
        double cx, double cy, double radius,
        double startDeg, double stepDeg, int steps)
    {
        double total = 0;
        double angle = startDeg;

        var prevPts = Triangle(cx, cy, radius, angle);
        var centroid = RotationCalculator.Centroid(prevPts);
        var prevAngles = RotationCalculator.ComputeAngles(prevPts, centroid);

        for (int i = 0; i < steps; i++)
        {
            angle += stepDeg;
            var newPts = Triangle(cx, cy, radius, angle);
            centroid = RotationCalculator.Centroid(newPts);
            var newAngles = RotationCalculator.ComputeAngles(newPts, centroid);

            double delta = RotationCalculator.MeanRotationDelta(prevAngles, newAngles);
            total += delta;

            prevAngles = newAngles;
        }

        return RotationCalculator.ToDegrees(total);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Triangle validation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidTriangle_AnyThreePoints_ReturnsTrue()
    {
        var pts = new List<TouchPoint>
        {
            new(0, 100, 100, 0),
            new(1, 120, 100, 0),
            new(2, 140, 100, 0),
        };
        Assert.True(RotationCalculator.IsValidTriangle(pts));
    }

    [Fact]
    public void InvalidTriangle_FewerThanThreePoints_ReturnsFalse()
    {
        var pts = new List<TouchPoint>
        {
            new(0, 100, 100, 0),
            new(1, 200, 200, 0),
        };
        Assert.False(RotationCalculator.IsValidTriangle(pts));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Clockwise rotation (positive delta in Windows screen coords)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ClockwiseRotation_SingleStep_ProducesPositiveDelta()
    {
        var prev = Triangle(200, 200, 60, 0);
        var next = Triangle(200, 200, 60, 5); // +5° CW

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        double deltaDeg = RotationCalculator.ToDegrees(delta);

        Assert.True(deltaDeg > 0, $"Expected positive (CW) delta, got {deltaDeg:F4}°");
        Assert.True(Math.Abs(deltaDeg - 5.0) < 0.5,
            $"Expected ~5°, got {deltaDeg:F4}°");
    }

    [Fact]
    public void ClockwiseRotation_90Degrees_AccumulatesCorrectly()
    {
        // 90° CW in 9 steps of 10° each
        double total = AccumulateRotation(200, 200, 60, startDeg: 0, stepDeg: 10, steps: 9);
        Assert.True(Math.Abs(total - 90.0) < 2.0,
            $"Expected ~90° accumulated, got {total:F2}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Counter-clockwise rotation (negative delta)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CounterClockwiseRotation_SingleStep_ProducesNegativeDelta()
    {
        var prev = Triangle(200, 200, 60, 0);
        var next = Triangle(200, 200, 60, -5); // -5° CCW

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        Assert.True(delta < 0,
            $"Expected negative (CCW) delta, got {RotationCalculator.ToDegrees(delta):F4}°");
    }

    [Fact]
    public void CounterClockwiseRotation_90Degrees_AccumulatesCorrectly()
    {
        double total = AccumulateRotation(200, 200, 60, startDeg: 0, stepDeg: -10, steps: 9);
        Assert.True(Math.Abs(total + 90.0) < 2.0,
            $"Expected ~-90° accumulated, got {total:F2}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pure translation — should produce zero rotation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PureTranslation_Right_ProducesNearZeroRotation()
    {
        // All fingers move 20px to the right — no rotation
        var prev = Triangle(200, 200, 60, 0);
        var next = prev.Select(p => p with { X = p.X + 20 }).ToList();

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        double deltaDeg = Math.Abs(RotationCalculator.ToDegrees(delta));

        Assert.True(deltaDeg < 1.0,
            $"Expected ~0° for pure translation, got {deltaDeg:F4}°");
    }

    [Fact]
    public void PureTranslation_Down_ProducesNearZeroRotation()
    {
        var prev = Triangle(200, 200, 60, 30);
        var next = prev.Select(p => p with { Y = p.Y + 30 }).ToList();

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        double deltaDeg = Math.Abs(RotationCalculator.ToDegrees(delta));

        Assert.True(deltaDeg < 1.0,
            $"Expected ~0° for pure translation, got {deltaDeg:F4}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Jitter — tiny movement should produce tiny delta
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Jitter_TinyPixelNoise_ProducesSubThresholdDelta()
    {
        var rng = new Random(42);
        var prev = Triangle(200, 200, 60, 0);

        // Apply random ±0.5px noise to each finger
        var next = prev.Select(p => p with
        {
            X = p.X + (rng.NextDouble() - 0.5),
            Y = p.Y + (rng.NextDouble() - 0.5)
        }).ToList();

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        double deltaDeg = Math.Abs(RotationCalculator.ToDegrees(delta));

        // Should be below the jitter threshold (default 0.5°)
        Assert.True(deltaDeg < Settings.JitterThresholdDegrees * 2,
            $"Expected sub-threshold delta for jitter, got {deltaDeg:F4}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 180° wrap-around — should not produce sign flip
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rotation_CrossingPiAngle_WrapsCorrectly()
    {
        // Finger crossing 180° mark — should still give positive small delta
        var prev = Triangle(200, 200, 60, 178);
        var next = Triangle(200, 200, 60, 182); // crosses 180°

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        double deltaDeg = RotationCalculator.ToDegrees(delta);

        // Should be ~+4° (positive CW), not a massive negative wrap
        Assert.True(deltaDeg > 2.0 && deltaDeg < 8.0,
            $"Expected ~+4° after 180° wrap, got {deltaDeg:F4}°");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // One finger missing (only 2 matched IDs)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void OneFinger_Missing_StillComputesDelta()
    {
        var prev = Triangle(200, 200, 60, 0);
        var next = Triangle(200, 200, 60, 10); // +10° CW

        var centP = RotationCalculator.Centroid(prev);
        var centN = RotationCalculator.Centroid(next);
        var angP  = RotationCalculator.ComputeAngles(prev, centP);
        var angN  = RotationCalculator.ComputeAngles(next, centN);

        // Remove ID=2 from prev (simulate finger appearing mid-gesture)
        angP.Remove(2);

        double delta = RotationCalculator.MeanRotationDelta(angP, angN);
        double deltaDeg = RotationCalculator.ToDegrees(delta);

        // Should still compute from the 2 matched fingers
        Assert.True(deltaDeg > 5.0 && deltaDeg < 15.0,
            $"Expected ~10° from 2 matched fingers, got {deltaDeg:F4}°");
    }

    [Fact]
    public void UnstableContactIds_SlotMatchingFallback_ComputesDelta()
    {
        // prev has IDs 0,1,2; next has IDs 10,11,12 — slot matching should match by sorted angle
        var prev = new Dictionary<uint, double> { [0] = 0.0, [1] = 1.0, [2] = 2.0 };
        var next = new Dictionary<uint, double> { [10] = 0.1, [11] = 1.1, [12] = 2.1 };

        double delta = RotationCalculator.MeanRotationDelta(prev, next);
        Assert.True(Math.Abs(delta - 0.1) < 0.01, $"Expected delta ~0.1, got {delta}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Centroid
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Centroid_EquilateralTriangle_IsCenter()
    {
        var pts = Triangle(100, 150, 50, 0);
        var (cx, cy) = RotationCalculator.Centroid(pts);

        Assert.True(Math.Abs(cx - 100) < 0.01, $"Centroid X expected 100, got {cx:F3}");
        Assert.True(Math.Abs(cy - 150) < 0.01, $"Centroid Y expected 150, got {cy:F3}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Angle wrap utility
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI)]
    [InlineData(-Math.PI)]
    [InlineData(3 * Math.PI)]   // should wrap to ~π
    [InlineData(-3 * Math.PI)]  // should wrap to ~-π or π
    public void WrapAngle_AlwaysInMinusPiToPi(double input)
    {
        double result = RotationCalculator.WrapAngle(input);
        Assert.True(result >= -Math.PI && result <= Math.PI,
            $"WrapAngle({input:F3}) = {result:F3} is out of [-π, π]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Signed area
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SignedArea_RightTriangle_CorrectValue()
    {
        var a = new TouchPoint(0, 0, 0, 0);
        var b = new TouchPoint(1, 4, 0, 0);
        var c = new TouchPoint(2, 0, 3, 0);

        double area = Math.Abs(RotationCalculator.SignedTriangleArea(a, b, c));
        Assert.True(Math.Abs(area - 6.0) < 0.01, $"Expected area 6, got {area:F4}");
    }
}
