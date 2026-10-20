using System;
using GunmageddonAudio;

public static class ArtilleryBallisticsTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    // Independent flight simulation using Bullet.TrajectoryTrace's force ordering.
    private static float Land(float angle, float speed, float reference, float drag, float gravity, float targetHeight, float step, out float apex, out float flight)
    {
        double x = 0, y = 0, t = 0;
        double vx = speed * Math.Cos(angle * Math.PI / 180), vy = speed * Math.Sin(angle * Math.PI / 180);
        apex = 0; flight = 0;
        for (int i = 0; i < 180f / step; i++)
        {
            double oldX = x, oldY = y;
            vy -= gravity * step;
            double magnitude = Math.Sqrt(vx * vx + vy * vy);
            // Match the game's early projectile cull as well as its drag.
            Check(magnitude * magnitude >= reference * reference * .02, "Reduced charge was culled by the game before impact.");
            double ratio = Math.Max(0, 1 - magnitude * drag * step / reference);
            vx *= ratio; vy *= ratio;
            x += vx * step; y += vy * step; t += step;
            apex = Math.Max(apex, (float)y);
            if (y <= targetHeight && vy < 0 && i > 0)
            {
                double fraction = (oldY - targetHeight) / (oldY - y);
                flight = (float)(t - step + fraction * step);
                return (float)(oldX + (x - oldX) * fraction);
            }
        }
        throw new Exception("Shell did not land.");
    }

    public static void Main()
    {
        foreach (float gravity in new[] { 9.81f, 19.62f, 29.43f })
        foreach (float step in new[] { .01f, .02f, .04f })
        foreach (float range in new[] { 500f, 1000f, 2000f, 5000f, 10000f, 20000f })
        {
            float angle, speed, flight;
            bool reachable = ArtilleryBallistics.Solve(range, 0f, 850f, .01f, gravity, 23f, 153f, step, out angle, out speed, out flight, 25f);
            if (!reachable) { Check(gravity > 20f && range >= 20000f, "Unexpected unreachable target."); continue; }
            float apex, measuredFlight;
            float hit = Land(angle, speed, 850f, .01f, gravity, 0f, step, out apex, out measuredFlight);
            Check(Math.Abs(hit - range) < 3f, "Ballistic impact missed: " + range + " -> " + hit);
            Check(Math.Abs(measuredFlight - flight) < .1f, "Flight-time prediction is wrong.");
            Check(speed >= 153f && speed <= 850f && angle <= 25f, "Unsafe launch charge/elevation.");
            if (range >= 4000f || (gravity == 19.62f && range >= 1000f))
                Check(angle >= 22.9f && apex > range * .09f, "Arc is too flat at " + range + " m, gravity " + gravity);
            if (gravity == 19.62f && step == .02f)
                Console.WriteLine("{0} m: {1:F1} deg, {2:F1} m/s, {3:F1} s, apex {4:F0} m, miss {5:F2} m", range, angle, speed, flight, apex, hit - range);
        }
        foreach (float height in new[] { -200f, 250f, 600f })
        {
            float angle, speed, flight, actual, measured;
            Check(ArtilleryBallistics.Solve(4000f, height, 850f, .01f, 19.62f, 23f, 153f, .02f, out angle, out speed, out flight, 25f), "Elevated target unreachable.");
            Check(ArtilleryBallistics.HeightAtRange(angle, 4000f, speed, 850f, .01f, 19.62f, .02f, out actual, out measured) && Math.Abs(actual - height) < 1f,
                "Altitude or flare airburst aim is wrong.");
        }
        float a, v, t;
        Check(!ArtilleryBallistics.Solve(100000f, 0, 850, .01f, 19.62f, 23, 153, .02f, out a, out v, out t), "Unreachable range accepted.");
        Check(!ArtilleryBallistics.Solve(4000, 0, 850, .01f, 0, 23, 153, .02f, out a, out v, out t), "Invalid gravity accepted.");
        Console.WriteLine("Passed {0} trajectory and projectile-survival assertions.", checks);
    }
}
