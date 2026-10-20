using System;
using GunmageddonAudio;

public static class FlareBallisticsTests
{
    public static void Main()
    {
        int count = 0;
        foreach (float range in new[] { 1000f, 2000f, 4000f })
        {
            float elevated, time, ground, groundTime, height, measuredTime;
            if (!FlareBallistics.Solve(range, 600, 850, .2f, 9.81f, out elevated, out time))
                throw new Exception("Flare area should be reachable at " + range);
            if (!FlareBallistics.Solve(range, 0, 850, .2f, 9.81f, out ground, out groundTime))
                throw new Exception("Ground reference should be reachable.");
            if (elevated <= ground) throw new Exception("Illumination must aim above the ordinary firing solution.");
            if (!FlareBallistics.HeightAtRange(elevated, range, 850, .2f, 9.81f, out height, out measuredTime)
                || Math.Abs(height - 600) > 1f || Math.Abs(measuredTime - time) > .1f)
                throw new Exception("Fuse-area ballistics do not meet the deployment altitude.");
            count += 3;
            Console.WriteLine(range + "m: elevation=" + elevated.ToString("F2") + "deg, arrival=" + time.ToString("F2") + "s, altitude=" + height.ToString("F1") + "m");
        }
        float angle, seconds;
        if (FlareBallistics.Solve(50000, 600, 850, .2f, 9.81f, out angle, out seconds))
            throw new Exception("An unreachable illumination area must not be treated as illuminated.");
        if (FlareBallistics.Solve(1000, 600, 0, .2f, 9.81f, out angle, out seconds))
            throw new Exception("Invalid shell velocity accepted.");
        Console.WriteLine("Passed " + (count + 2) + " illumination ballistics assertions.");
    }
}
