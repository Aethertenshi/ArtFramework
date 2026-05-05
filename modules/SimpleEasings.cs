using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ArtFramework.SimpleEasings
{
    // Enums
    public enum Easing
    {
        Linear,
        Quadratic,
        Cubic,
        Quartic,
        Exponential,
        Sine
    }
    public enum Direction
    {
        In,
        Out,
        InOut
    }

    // Methods
    public class FloatTweener
    {
        private float _elapsed;
        private float _duration;
        private float _startValue;
        private float _endValue;
        private Easing _easing;
        private Direction _direction;

        public bool IsPlaying { get; private set; }
        public float CurrentValue { get; private set; }

        // Your exact requested signature!
        public void Start(float duration, float startValue, float endValue,
                              Easing easing = Easing.Linear, Direction direction = Direction.In)
        {
            _elapsed = 0f;
            _duration = duration;
            _startValue = startValue;
            _endValue = endValue;
            _easing = easing;
            _direction = direction;

            CurrentValue = startValue;
            IsPlaying = true;
        }

        public void Restart(float duration, float targetValue,
                       Easing easing = Easing.Linear, Direction direction = Direction.In)
        {
            // 1. Capture the EXACT current value to prevent snapping
            _startValue = CurrentValue;

            // 2. Set the new destination
            _endValue = targetValue;

            // 3. Reset the timer and update settings
            _elapsed = 0f;
            _duration = duration;
            _easing = easing;
            _direction = direction;

            IsPlaying = true;
        }

        // Optional but highly recommended: A way to instantly snap the value without fading
        public void SetValue(float value)
        {
            _startValue = value;
            _endValue = value;
            CurrentValue = value;
            _elapsed = _duration; // Force it to be "finished"
            IsPlaying = false;
        }

        public void Update(float dt)
        {
            if (!IsPlaying) return;

            _elapsed += dt;

            // If we reach the end, snap to the exact end value and stop.
            if (_elapsed >= _duration)
            {
                _elapsed = _duration;
                CurrentValue = _endValue;
                IsPlaying = false;
                return;
            }

            // 1. Calculate a "Normalized Time" (a percentage from 0.0 to 1.0)
            float t = _elapsed / _duration;

            // 2. Warp that percentage using our Easing Math
            float easedT = ApplyEasing(t, _easing, _direction);

            // 3. Lerp (Linear Interpolate) between Start and End using the warped percentage
            CurrentValue = _startValue + (_endValue - _startValue) * easedT;
        }

        // --- THE MATH ENGINE ---

        private float ApplyEasing(float t, Easing easing, Direction dir)
        {
            if (easing == Easing.Linear) return t;

            // The Magic Mirror Trick: We use the "In" math to calculate all directions!
            switch (dir)
            {
                case Direction.In:
                    return GetEaseIn(t, easing);

                case Direction.Out:
                    // Run the math backwards to get an "Out" effect
                    return 1f - GetEaseIn(1f - t, easing);

                case Direction.InOut:
                    // Run it half-speed forward, then half-speed backward
                    if (t < 0.5f) return GetEaseIn(t * 2f, easing) / 2f;
                    return 1f - GetEaseIn((1f - t) * 2f, easing) / 2f;

                default:
                    return t;
            }
        }

        // We only ever have to define the basic "In" curves here.
        private float GetEaseIn(float t, Easing easing)
        {
            switch (easing)
            {
                case Easing.Quadratic: return t * t;
                case Easing.Cubic: return t * t * t;
                case Easing.Quartic: return t * t * t * t;
                case Easing.Sine: return 1f - (float)Math.Cos((t * Math.PI) / 2f);
                case Easing.Exponential: return t == 0f ? 0f : (float)Math.Pow(2, 10 * (t - 1));
                default: return t;
            }
        }
    }
}
