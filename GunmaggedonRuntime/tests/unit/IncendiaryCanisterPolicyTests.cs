using System;
using GunmageddonAudio;
public static class IncendiaryCanisterPolicyTests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Main()
    {
        foreach (var carrier in new[] { new OpticalVector(0, 0, 950), new OpticalVector(0, 950, 0), new OpticalVector(300, -200, 700) })
        foreach (int seed in new[] { 0, 1, -1, 1234567 })
        for (int i = 0; i < IncendiaryCanisterPolicy.FragmentCount; i++)
        {
            var a = IncendiaryCanisterPolicy.Velocity(carrier, i, seed); var b = IncendiaryCanisterPolicy.Velocity(carrier, i, seed);
            Check(a.X == b.X && a.Y == b.Y && a.Z == b.Z, "Deterministic remote presentation");
            double angle = Math.Acos(Math.Min(1, OpticalVector.Dot(a, carrier) / (a.Length * carrier.Length))) * 180 / Math.PI;
            Check(angle <= IncendiaryCanisterPolicy.ConeDegrees + .0001 && angle > 0, "Fragments remain inside forward cone");
            Check(a.Length >= 400 && a.Length < 10000 && !double.IsNaN(a.Length), "Finite fragment velocity");
        }
        Check(IncendiaryCanisterPolicy.Fuse(10, 0) < 10, "Opens ahead of intercept");
        Check(IncendiaryCanisterPolicy.Fuse(.1, 0) == .5, "Close-range fuse safety");
        Check(IncendiaryCanisterPolicy.Fuse(50, 0) == 24, "Native fuse upper bound");
        Check(IncendiaryCanisterPolicy.Energy(500, 1000) == .25, "Energy falls with velocity squared");
        Check(IncendiaryCanisterPolicy.Energy(2000, 1000) == 1 && IncendiaryCanisterPolicy.Energy(0, 0) == 0, "Bounded energy");
        Console.WriteLine("Passed " + checks + " incendiary canister policy assertions.");
    }
}
