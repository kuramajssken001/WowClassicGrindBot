﻿using System.Drawing;

using static System.MathF;

namespace SharedLib.Extensions;

public static class PointExt
{
    public static Point Scale(this Point p, float scale)
    {
        return new Point((int)(p.X * scale), (int)(p.Y * scale));
    }

    public static Point Scale(this Point p, float scaleX, float scaleY)
    {
        return new Point((int)(p.X * scaleX), (int)(p.Y * scaleY));
    }

    public static float SqrDistance(in Point p1, in Point p2)
    {
        return Sqrt(((p1.X - p2.X) * (p1.X - p2.X)) + ((p1.Y - p2.Y) * (p1.Y - p2.Y)));
    }
}
