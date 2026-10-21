using System;

namespace GunmageddonAudio
{
    public static class FlareBallistics
    {
        // Matches Bullet.TrajectoryTrace's gravity and speed-dependent drag step.
        public static bool Solve(float range, float height, float speed, float drag, float gravity,
            out float elevationDegrees, out float flightSeconds)
        {
            elevationDegrees = flightSeconds = 0f;
            if (range < 50f || speed < 1f || gravity <= 0f) return false;
            float previousAngle = 0f, previousError = 0f;
            bool previousValid = false;
            for (float angle = 0f; angle <= 80f; angle += 1f)
            {
                float y, seconds;
                bool valid = HeightAtRange(angle, range, speed, drag, gravity, out y, out seconds);
                float error = y - height;
                if (valid && previousValid && previousError <= 0f && error >= 0f)
                {
                    float low = previousAngle, high = angle;
                    for (int i = 0; i < 14; i++)
                    {
                        float mid = (low + high) * .5f;
                        HeightAtRange(mid, range, speed, drag, gravity, out y, out seconds);
                        if (y < height) low = mid; else high = mid;
                    }
                    elevationDegrees = (low + high) * .5f;
                    HeightAtRange(elevationDegrees, range, speed, drag, gravity, out y, out flightSeconds);
                    return true;
                }
                previousValid = valid;
                previousAngle = angle;
                previousError = error;
            }
            return false;
        }

        public static bool HeightAtRange(float angle, float range, float speed, float drag, float gravity,
            out float height, out float seconds)
        {
            const float step = .02f;
            double radians = angle * Math.PI / 180;
            float vx = speed * (float)Math.Cos(radians), vy = speed * (float)Math.Sin(radians);
            float x = 0f, y = 0f;
            height = seconds = 0f;
            for (int i = 1; i <= 9000; i++)
            {
                vy -= gravity * step;
                float magnitude = (float)Math.Sqrt(vx * vx + vy * vy);
                float multiplier = Math.Max(0f, 1f - magnitude * drag * step / speed);
                vx *= multiplier;
                vy *= multiplier;
                float nextX = x + vx * step, nextY = y + vy * step;
                if (nextX >= range)
                {
                    float fraction = (range - x) / (nextX - x);
                    height = y + (nextY - y) * fraction;
                    seconds = (i - 1 + fraction) * step;
                    return true;
                }
                x = nextX;
                y = nextY;
                if (y < -1000f || magnitude < speed * .1f || vx < 1f) return false;
            }
            return false;
        }
    }
}
