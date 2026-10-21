using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClusterSaturationRegression
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        var assembly = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.5.4/GunmageddonAudio.dll")));
        var feature = assembly.GetType("GunmageddonAudio.SpecialShells");
        var carrierType = feature.GetNestedType("Carrier", BindingFlags.NonPublic);
        var kind = assembly.GetType("GunmageddonAudio.SpecialShellKind");
        var fuse = feature.GetMethod("Fuse", Static);
        var spread = feature.GetMethod("ClusterVelocity", Static);
        var game = typeof(Unit).Assembly;
        var datum = game.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var originPosition = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = originPosition.GetValue(null);
        var randomState = UnityEngine.Random.state;
        var global = game.GetType("GlobalPositionExtensions").GetMethod("ToGlobalPosition");
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Cluster saturation regression"); SceneManager.MoveGameObjectToScene(root, preview);
        var report = new StringBuilder();
        try
        {
            origin.SetValue(null, root.transform); originPosition.SetValue(null, Vector3.zero);
            UnityEngine.Random.InitState(203);
            foreach (float speed in new[] { 166f, 377f, 804f })
            {
                const float gravity = 19.62f;
                var launch = new Vector3(0, 100, 0);
                var initial = new Vector3(0, Mathf.Sin(23f * Mathf.Deg2Rad), Mathf.Cos(23f * Mathf.Deg2Rad)) * speed;
                float flight = 2f * initial.y / gravity;
                var aim = launch + Vector3.forward * initial.z * flight;
                var carrier = Activator.CreateInstance(carrierType, true);
                carrierType.GetField("Kind", Fields).SetValue(carrier, Enum.Parse(kind, "Cluster"));
                carrierType.GetField("Launch", Fields).SetValue(carrier, global.Invoke(null, new object[] { launch }));
                carrierType.GetField("Aim", Fields).SetValue(carrier, global.Invoke(null, new object[] { aim }));
                carrierType.GetField("Gravity", Fields).SetValue(carrier, gravity);
                Vector3 opening = Vector3.zero, velocity = Vector3.zero;
                float opened = -1;
                for (float time = .02f; time < flight; time += .02f)
                {
                    var p = launch + initial * time + Vector3.down * (.5f * gravity * time * time);
                    var v = initial + Vector3.down * gravity * time;
                    if ((bool)fuse.Invoke(null, new object[] { carrier, p, v, time })) { opening = p; velocity = v; opened = time; break; }
                }
                Check(opened > 0, "Cluster failed to open at " + speed);
                float lead = flight - opened;
                Check(lead >= 3f && lead <= 4.03f, "Unexpected release lead: " + lead);
                if (speed > 300) Check(opening.y - aim.y > 350f, "Long-range burst still too low.");
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                Vector3 average = Vector3.zero;
                for (int i = 0; i < 36; i++)
                {
                    var v = (Vector3)spread.Invoke(null, new object[] { velocity, i, 36, .7f });
                    Check(Vector3.Angle(velocity, v) <= 12.01f, "Clusterlet escaped cone.");
                    Check(Mathf.Abs(Vector3.Dot(v - velocity, velocity.normalized)) < .001f, "Forward momentum changed.");
                    float t = (v.y + Mathf.Sqrt(v.y * v.y + 2f * gravity * (opening.y - aim.y))) / gravity;
                    var hit = opening + v * t + Vector3.down * (.5f * gravity * t * t);
                    minX = Mathf.Min(minX, hit.x); maxX = Mathf.Max(maxX, hit.x);
                    minZ = Mathf.Min(minZ, hit.z); maxZ = Mathf.Max(maxZ, hit.z);
                    average += hit / 36f;
                }
                Check(maxX - minX > 150f && maxZ - minZ > 100f, "Insufficient saturation footprint.");
                Check(maxX - minX < 450f && maxZ - minZ < 450f, "Payload diluted over an excessively large area.");
                Check(Vector3.Distance(average, aim) < 50f, "Cone introduced a large aiming bias.");
                carrierType.GetField("Kind", Fields).SetValue(carrier, Enum.Parse(kind, "Smoke"));
                if (opening.y - aim.y > 120f)
                    Check(!(bool)fuse.Invoke(null, new object[] { carrier, opening, velocity, opened }), "Smoke inherited early cluster fuse.");
                carrierType.GetField("Kind", Fields).SetValue(carrier, Enum.Parse(kind, "Cluster"));
                Check(!(bool)fuse.Invoke(null, new object[] { carrier, opening, new Vector3(0, 50, initial.z), opened }), "Ascending carrier opened.");
                carrierType.GetField("Released", Fields).SetValue(carrier, true);
                Check(!(bool)fuse.Invoke(null, new object[] { carrier, opening, velocity, opened }), "Released carrier opened twice.");
                report.AppendLine(string.Format("{0} m/s: burst {1:F2}s before impact at {2:F0}m above target; {3:F0} x {4:F0}m footprint; centroid error {5:F1}m.", speed, lead, opening.y - aim.y, maxX - minX, maxZ - minZ, Vector3.Distance(average, aim)));
            }
            return report.ToString();
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            origin.SetValue(null, savedOrigin); originPosition.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
