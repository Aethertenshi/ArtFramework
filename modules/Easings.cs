using System;

namespace ArtFramework;

public static class Easings
{
    public static float EaseCubicInOut(float t, float b, float c, float d)
    {
        if ((t /= d / 2.0f) < 1.0f) return c / 2.0f * t * t * t + b;
        return c / 2.0f * ((t -= 2.0f) * t * t + 2.0f) + b;
    }

    public static float EaseQuadInOut(float t, float b, float c, float d)
    {
        if ((t /= d / 2.0f) < 1.0f) return c / 2.0f * t * t + b;
        return -c / 2.0f * ((--t) * (t - 2.0f) - 1.0f) + b;
    }

    public static float EaseQuintIn(float t, float b, float c, float d)
    {
        t /= d;
        return c * t * t * t * t * t + b;
    }
}