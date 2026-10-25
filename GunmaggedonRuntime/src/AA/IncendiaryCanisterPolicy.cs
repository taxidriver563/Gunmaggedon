using System;

namespace GunmageddonAudio
{
    // Game balance values, not a reconstruction of a historical shell.
    public static class IncendiaryCanisterPolicy
    {
        public const int FragmentCount = 96;
        public const double Lifetime = 2.8, ConeDegrees = 14, Drag = .8;
        public const double BurstLeadSeconds = .28;
        public static double Fuse(double flight, double error, double maximumFlight = 24)
        { return Math.Max(.5, Math.Min(maximumFlight, flight + error - BurstLeadSeconds)); }
        public static OpticalVector Velocity(OpticalVector carrier, int index, int seed)
        {
            double magnitude = Math.Sqrt(carrier.X * carrier.X + carrier.Y * carrier.Y + carrier.Z * carrier.Z);
            var forward = magnitude > .001 ? carrier * (1 / magnitude) : new OpticalVector(0, 0, 1);
            var axis = Math.Abs(forward.Y) < .95 ? new OpticalVector(0, 1, 0) : new OpticalVector(1, 0, 0);
            var right = Cross(axis, forward); right = right * (1 / Math.Sqrt(Dot(right, right)));
            var up = Cross(forward, right);
            // Deterministic, even disk coverage with a randomized rotation.
            double phase = (uint)seed * (Math.PI * 2 / uint.MaxValue);
            double angle = phase + index * 2.399963229728653;
            double radius = Math.Sqrt((index + .5) / FragmentCount) * Math.Tan(ConeDegrees * Math.PI / 180);
            var direction = forward + right * (Math.Cos(angle) * radius) + up * (Math.Sin(angle) * radius);
            direction = direction * (1 / Math.Sqrt(Dot(direction, direction)));
            return direction * Math.Max(400, magnitude * .7 + 220);
        }
        public static double Energy(double speed, double launchSpeed)
        { return launchSpeed > 0 ? Math.Max(0, Math.Min(1, speed * speed / (launchSpeed * launchSpeed))) : 0; }
        static double Dot(OpticalVector a, OpticalVector b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        static OpticalVector Cross(OpticalVector a, OpticalVector b)
        { return new OpticalVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
    }
}
