using System.Numerics;

namespace FRP1.Plugin.RpBar;

public static class RpBarGeometry
{
    public static Vector2 ResetPosition => new(24f, 100f);

    public static float ValidateScale(float scale)
    {
        if (!float.IsFinite(scale) || scale is < 0.75f or > 1.75f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        return scale;
    }

    public static Vector2 Clamp(Vector2 relativePosition, Vector2 barSize, Vector2 viewportSize, float margin = 8f)
    {
        if (!float.IsFinite(margin) || margin < 0) margin = 8f;
        return new Vector2(Axis(relativePosition.X, barSize.X, viewportSize.X, margin),
            Axis(relativePosition.Y, barSize.Y, viewportSize.Y, margin));
    }

    private static float Axis(float position, float size, float viewport, float margin)
    {
        if (!float.IsFinite(viewport) || viewport <= 0) return 0;
        if (!float.IsFinite(size) || size < 0) size = 0;
        var minimum = Math.Min(margin, viewport / 2);
        var maximum = Math.Max(0, viewport - size - minimum);
        minimum = Math.Min(minimum, maximum);
        if (!float.IsFinite(position)) position = minimum;
        return Math.Clamp(position, minimum, maximum);
    }
}
