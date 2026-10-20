using System;

namespace GunmageddonAudio
{
    public static class ArtilleryBallistics
    {
        // BulletSim always uses WeaponInfo.muzzleVelocity as the drag reference,
        // even when a reduced propellant charge launches the shell more slowly.
        public static bool HeightAtRange(float angle, float range, float chargeSpeed, float referenceSpeed,
            float drag, float gravity, float step, out float height, out float seconds)
        {
            height = seconds = 0f;
            if (range <= 0f || chargeSpeed <= 0f || referenceSpeed <= 0f || gravity <= 0f || step <= 0f) return false;
            double radians = angle * Math.PI / 180;
            float vx = chargeSpeed * (float)Math.Cos(radians), vy = chargeSpeed * (float)Math.Sin(radians);
            float x = 0f, y = 0f;
            for (int i = 1; i * step <= 175f; i++)
            {
                vy -= gravity * step;
                float magnitude = (float)Math.Sqrt(vx * vx + vy * vy);
                float multiplier = Math.Max(0f, 1f - magnitude * drag * step / referenceSpeed);
                vx *= multiplier; vy *= multiplier;
                float nextX = x + vx * step, nextY = y + vy * step;
                if (nextX >= range)
                {
                    float fraction = (range - x) / (nextX - x);
                    height = y + (nextY - y) * fraction;
                    seconds = (i - 1 + fraction) * step;
                    return true;
                }
                x = nextX; y = nextY;
                if (vx < .1f || y < -10000f) return false;
            }
            return false;
        }

        public static bool Solve(float range, float height, float maximumSpeed, float drag, float gravity,
            float preferredAngle, float minimumSpeed, float step, out float angle, out float speed, out float seconds, float maximumAngle = 65f)
        {
            angle = speed = seconds = 0f;
            if (range < 25f || maximumSpeed <= 0f || gravity <= 0f || drag < 0f ||
                preferredAngle < 10f || preferredAngle > maximumAngle || maximumAngle > 65f || minimumSpeed > maximumSpeed || minimumSpeed <= 0f) return false;
            // First vary the charge at the desired elevation, rather than lobbing
            // full-velocity shells nearly vertically over nearby targets.
            foreach (float elevation in new[] { preferredAngle, 25f, 45f, 50f, 55f, 60f, 65f })
            {
                if (elevation < preferredAngle || elevation > maximumAngle) continue;
                float lowY, lowTime, highY, highTime;
                bool lowValid = HeightAtRange(elevation, range, minimumSpeed, maximumSpeed, drag, gravity, step, out lowY, out lowTime);
                bool highValid = HeightAtRange(elevation, range, maximumSpeed, maximumSpeed, drag, gravity, step, out highY, out highTime);
                if (!highValid || highY < height) continue;
                if (lowValid && lowY > height)
                {
                    // Point-blank targets require a shallower safe arc. Below
                    // this minimum charge BulletSim would cull the projectile.
                    if (SolveAngle(range, height, minimumSpeed, maximumSpeed, drag, gravity, elevation, step, out angle, out seconds))
                    { speed = minimumSpeed; return true; }
                    continue;
                }
                float low = minimumSpeed, high = maximumSpeed;
                for (int i = 0; i < 20; i++)
                {
                    float mid = (low + high) * .5f, y, t;
                    if (!HeightAtRange(elevation, range, mid, maximumSpeed, drag, gravity, step, out y, out t) || y < height) low = mid;
                    else high = mid;
                }
                angle = elevation; speed = (low + high) * .5f;
                float actual;
                return HeightAtRange(angle, range, speed, maximumSpeed, drag, gravity, step, out actual, out seconds) && Math.Abs(actual - height) < 2f;
            }
            return false;
        }

        private static bool SolveAngle(float range, float height, float speed, float referenceSpeed, float drag,
            float gravity, float upperAngle, float step, out float angle, out float seconds)
        {
            angle = seconds = 0f;
            float previous = -45f, previousY, t;
            bool previousValid = HeightAtRange(previous, range, speed, referenceSpeed, drag, gravity, step, out previousY, out t);
            for (float current = -44f; current <= upperAngle; current += 1f)
            {
                float y;
                bool valid = HeightAtRange(current, range, speed, referenceSpeed, drag, gravity, step, out y, out t);
                if (valid && previousValid && previousY <= height && y >= height)
                {
                    float low = previous, high = current;
                    for (int i = 0; i < 18; i++)
                    {
                        float mid = (low + high) * .5f;
                        HeightAtRange(mid, range, speed, referenceSpeed, drag, gravity, step, out y, out t);
                        if (y < height) low = mid; else high = mid;
                    }
                    angle = (low + high) * .5f;
                    HeightAtRange(angle, range, speed, referenceSpeed, drag, gravity, step, out y, out seconds);
                    return true;
                }
                previous = current; previousY = y; previousValid = valid;
            }
            return false;
        }
    }
}
