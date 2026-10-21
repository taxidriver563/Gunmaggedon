using System;
using GunmageddonAudio;

public static class ArmorPiercingPolicyTests
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static void Main()
    {
        foreach (CoastalAmmoMode mode in Enum.GetValues(typeof(CoastalAmmoMode)))
            Check(!ArmorPiercingPolicy.UseAP(mode, false, true, 200, 6), "SPECIAL/unsupported gun received AP");
        Check(!ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, true, 30, 2), "Light patrol boat should retain HE");
        Check(ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, true, 100, 2), "Large ship did not select AP");
        Check(ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, true, 30, 3), "Armored ship did not select AP");
        Check(!ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, false, 200, 6), "Ground/air target selected AP automatically");
        Check(!ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, false, 0, 0), "Manual Auto fire selected AP");
        Check(ArmorPiercingPolicy.UseAP(CoastalAmmoMode.AP, true, false, 0, 0), "Forced AP not available for manual fire");
        Check(!ArmorPiercingPolicy.UseAP(CoastalAmmoMode.HE, true, true, 200, 6), "Forced HE changed to AP");
        Check(!ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, true, 64.99f, 2.99f), "Auto threshold too broad");
        Check(ArmorPiercingPolicy.UseAP(CoastalAmmoMode.Auto, true, true, 65f, 2), "Length boundary incorrect");
        foreach (float speed in new[] { 153f, 166f, 377f, 804f, 850f })
        {
            float native = ArmorPiercingPolicy.NativePenetration(12000f, 850f, speed);
            float launchDamage = native * speed * speed / (850f * 850f);
            Check(Math.Abs(launchDamage - 12000f) < .01f, "Reduced charge lost AP penetration");
            float impact = native * (speed * .8f) * (speed * .8f) / (850f * 850f);
            Check(Math.Abs(impact - 7680f) < .01f, "Flight-speed loss was erased");
        }
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            Check(ArmorPiercingPolicy.NativePenetration(12000, 850, invalid) == 0, "Invalid launch speed accepted");
            Check(ArmorPiercingPolicy.NativePenetration(12000, invalid, 400) == 0, "Invalid reference speed accepted");
            Check(ArmorPiercingPolicy.NativePenetration(invalid, 850, 400) == 0, "Invalid damage accepted");
        }
        Console.WriteLine("Passed {0} AP selection and reduced-charge damage assertions.", checks);
    }
}
