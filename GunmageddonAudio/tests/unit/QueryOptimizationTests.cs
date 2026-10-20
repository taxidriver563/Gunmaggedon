using System;
using GunmageddonAudio;
using UnityEngine;

// Deterministic query transport for exercising production buffer management
// without a running Unity physics scene. These do not simulate native physics.
namespace UnityEngine
{
    public struct Vector3 { }
    public class Collider { public int Id; }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public static Collider[] Scene = new Collider[0];
        public static int AllocatingCalls;
        public static bool Fail;
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] hits, int mask, QueryTriggerInteraction triggers)
        {
            if (Fail) throw new InvalidOperationException("Injected query failure");
            int count = Math.Min(Scene.Length, hits.Length);
            Array.Copy(Scene, hits, count); return count;
        }
        public static Collider[] OverlapSphere(Vector3 position, float radius, int mask, QueryTriggerInteraction triggers)
        { AllocatingCalls++; return (Collider[])Scene.Clone(); }
    }
}
public class UnitPart { }

public static class QueryOptimizationTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Scene(int count)
    {
        Physics.Scene = new Collider[count];
        for (int i = 0; i < count; i++) Physics.Scene[i] = new Collider { Id = i };
    }

    private static bool Intersects(double[] from, double[] to, double[] center, double[] radii)
    {
        double a = 0, b = 0, c = -1;
        for (int i = 0; i < 3; i++)
        {
            double p = (from[i] - center[i]) / radii[i], d = (to[i] - from[i]) / radii[i];
            a += d * d; b += 2 * p * d; c += p * p;
        }
        if (c <= 0) return true;
        if (a <= 1e-12) return false;
        double t = Math.Max(0, Math.Min(1, -b / (2 * a)));
        return a * t * t + b * t + c <= 0;
    }

    public static void Main()
    {
        foreach (int size in new[] { 0, 1, 63, 64, 65, 127, 128, 511, 1023, 1024, 2048 })
        {
            ClusterDamageQuery.Clear(); Scene(size); Physics.AllocatingCalls = 0;
            var query = ClusterDamageQuery.Rent(new Vector3());
            Check(query.Count == size, "A full query buffer silently dropped colliders");
            for (int i = 0; i < size; i++) Check(query.Hits[i].Id == i, "Query hit missing");
            Check(Physics.AllocatingCalls == (size >= 1024 ? 1 : 0), "Unexpected fallback policy");
            var oldHits = query.Hits; query.Damaged.Add(new UnitPart()); query.Release(); query.Release();
            if (size < 1024) foreach (var hit in oldHits) Check(hit == null, "Released buffer retains collider");
            var next = ClusterDamageQuery.Rent(new Vector3());
            Check(ReferenceEquals(query, next) && next.Damaged.Count == 0, "Scratch not reused/reset");
            if (size < 1024) Check(ReferenceEquals(oldHits, next.Hits), "Warmed query allocated a new hit array");
            next.Release();
        }
        ClusterDamageQuery.Clear(); Scene(3);
        var outer = ClusterDamageQuery.Rent(new Vector3()); outer.Damaged.Add(new UnitPart());
        Scene(70); var inner = ClusterDamageQuery.Rent(new Vector3());
        Check(!ReferenceEquals(outer, inner) && !ReferenceEquals(outer.Hits, inner.Hits), "Nested damage shares scratch");
        Check(outer.Count == 3 && outer.Damaged.Count == 1 && outer.Hits[2].Id == 2, "Nested query damaged outer state");
        inner.Release(); outer.Release();
        Physics.Fail = true;
        try { ClusterDamageQuery.Rent(new Vector3()); throw new Exception("Expected query failure"); }
        catch (InvalidOperationException) { }
        Physics.Fail = false; Scene(1);
        var recovered = ClusterDamageQuery.Rent(new Vector3());
        Check(recovered.Count == 1 && recovered.Damaged.Count == 0, "Failure left query dirty"); recovered.Release();

        var random = new Random(203); int rejected = 0, intersections = 0;
        for (int test = 0; test < 100000; test++)
        {
            var from = new double[3]; var to = new double[3]; var center = new double[3]; var radii = new double[3];
            bool broad = true;
            for (int axis = 0; axis < 3; axis++)
            {
                from[axis] = (float)(random.NextDouble() * 2000 - 1000);
                to[axis] = (float)(random.NextDouble() * 2000 - 1000);
                center[axis] = (float)(random.NextDouble() * 2000 - 1000);
                radii[axis] = (float)(1 + random.NextDouble() * 200);
                broad &= QueryGeometry.AxisOverlaps((float)from[axis], (float)to[axis], (float)center[axis], (float)radii[axis]);
            }
            bool exact = Intersects(from, to, center, radii);
            if (exact) intersections++;
            if (!broad) rejected++;
            Check(!exact || broad, "Broad phase discarded a segment intersecting the cloud");
        }
        Check(QueryGeometry.AxisOverlaps(-2, 2, 0, 1), "Crossing with endpoints outside rejected");
        Check(QueryGeometry.AxisOverlaps(1, 1, 0, 1), "Tangent/zero-length segment rejected");
        Check(intersections > 0 && rejected > 0, "Randomized geometry test did not exercise both paths");
        Console.WriteLine("Passed {0} query assertions; {1}/100000 random segments rejected before exact cloud math; {2} intersections preserved.", checks, rejected, intersections);
    }
}
