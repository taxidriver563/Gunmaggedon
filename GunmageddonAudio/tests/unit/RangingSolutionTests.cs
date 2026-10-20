using System;
using GunmageddonAudio;

public static class RangingSolutionTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    public static void Main()
    {
        var solution = new RangingSolution();
        solution.Reset(100, 240f, 80f);
        int generation = solution.Generation;
        for (int i = 0; i < 3; i++)
        {
            int shot = solution.Fire();
            float f = solution.ForwardBias, r = solution.RightBias;
            float miss = (float)Math.Sqrt(f * f + r * r);
            solution.Observe(generation, shot, f, r, miss, 60f, 2);
        }
        Check(solution.Established, "Feedback should converge to a close impact.");
        Check(solution.LastObservedShot == 3, "Solution should establish on the third impact for this bracket.");
        Check(Math.Abs(solution.ForwardBias) < 60f, "Correction should reduce the original ranging error.");

        solution.Reset(200, -200f, 50f);
        Check(!solution.Established && solution.ShotsFired == 0, "A new target must reset ranging.");
        Check(!solution.Observe(generation, 99, 0, 0, 0, 60, 2), "A shell for the previous target cannot solve the new target.");
        Check(solution.LastObservedShot == 0, "Stale feedback must not consume observations.");

        int current = solution.Generation;
        solution.Fire();
        Check(!solution.Observe(current, 1, 0, 0, 0, 60, 2), "A lucky first shot still requires the configured minimum.");
        solution.Fire();
        Check(!solution.Observe(current, 2, 100, 0, 100, 60, 2), "Two shots alone cannot unlock normal ammunition.");
        solution.Fire();
        Check(!solution.Observe(current, 3, 150, 0, 150, 60, 2), "A third distant impact must remain a spotting round.");
        Check(!solution.Observe(current, 2, 0, 0, 0, 60, 2), "Out-of-order older rounds cannot overwrite newer corrections.");
        solution.Fire();
        Check(solution.Observe(current, 4, 20, 0, 20, 60, 2), "A sufficiently close impact should unlock normal shells.");
        float lockedBias = solution.ForwardBias;
        Check(!solution.Observe(current, 5, 900, 0, 900, 60, 2) && solution.ForwardBias == lockedBias,
            "Other spotting rounds already in flight must not spoil an established solution.");

        var secondGun = new RangingSolution();
        secondGun.Reset(200, 600, 100);
        Check(!secondGun.Established && solution.Established, "Guns must keep independent solutions for the same target.");
        for (int i = 0; i < 100; i++)
        {
            int shot = secondGun.Fire();
            secondGun.Observe(secondGun.Generation, shot, 10000, -10000, 10000, 60, 2);
        }
        Check(!secondGun.Established, "Never switch to normal shells without a qualifying impact.");
        Check(Math.Abs(secondGun.ForwardBias) <= 1200 && Math.Abs(secondGun.RightBias) <= 600,
            "Impossible observations must not cause unbounded aiming corrections.");
        Console.WriteLine("Passed " + assertions + " ranging assertions.");
    }
}
