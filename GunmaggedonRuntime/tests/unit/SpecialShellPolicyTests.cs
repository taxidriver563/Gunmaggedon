using System;
using GunmageddonAudio;

public static class SpecialShellPolicyTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        for (int i = 0; i < 5; i++)
            Check(SpecialShellPolicy.Choose(SpecialShellMode.Auto, 10000f, -1000f, -1000f, 90f, 70f, i, 5) == SpecialShellKind.Cluster,
                "Overdue support interrupted the opening cluster salvo.");
        Check(SpecialShellPolicy.Choose(SpecialShellMode.Auto, 100f, -1000f, 95f, 90f, 70f, 5, 5) == SpecialShellKind.Smoke,
            "Overdue smoke was starved by fresh chaff.");
        Check(SpecialShellPolicy.Choose(SpecialShellMode.Auto, 100f, 95f, -1000f, 90f, 70f, 5, 5) == SpecialShellKind.Chaff,
            "Overdue chaff was starved by fresh smoke.");
        Check(SpecialShellPolicy.Choose(SpecialShellMode.Auto, 100f, 95f, 95f, 90f, 70f, 5, 5) == SpecialShellKind.Cluster,
            "Fresh support clouds caused another support shot.");
        foreach (var mode in new[] { SpecialShellMode.Cluster, SpecialShellMode.Smoke, SpecialShellMode.Chaff, SpecialShellMode.TopAttack })
            Check((int)SpecialShellPolicy.Choose(mode, 0f, -1000f, -1000f, 90f, 70f, 0, 5) == (int)mode, "Forced mode changed.");

        foreach (float interval in new[] { 1f, 8f, 15f, 120f })
        {
            int cluster = 0, smoke = 0, chaff = 0, sinceSupport = 0;
            float lastSmoke = -1000f, lastChaff = -1000f;
            for (int shot = 0; shot < 120; shot++)
            {
                float now = shot * interval;
                var chosen = SpecialShellPolicy.Choose(SpecialShellMode.Auto, now, lastSmoke, lastChaff, 90f, 70f, sinceSupport, 5);
                if (chosen == SpecialShellKind.Cluster) { cluster++; sinceSupport++; }
                else
                {
                    Check(sinceSupport >= 5, "Support shells were not separated by five cluster rounds.");
                    sinceSupport = 0;
                    if (chosen == SpecialShellKind.Smoke) { smoke++; lastSmoke = now; }
                    else { chaff++; lastChaff = now; }
                }
            }
            Check(cluster >= 100, "Cluster share fell below five out of six payloads.");
            Check(smoke > 0 && chaff > 0, "A support type was permanently starved.");
            Console.WriteLine("Interval {0}s: {1} cluster, {2} smoke, {3} chaff", interval, cluster, smoke, chaff);
        }
        Console.WriteLine("Passed {0} shell-priority assertions.", checks);
    }
}
