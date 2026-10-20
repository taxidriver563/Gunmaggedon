using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArtilleryIntegration
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        var a = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/1.5.4/GunmageddonAudio.dll")));
        var artillery = a.GetType("GunmageddonAudio.CoastalArtillery");
        var ranging = a.GetType("GunmageddonAudio.CoastalRanging");
        var special = a.GetType("GunmageddonAudio.SpecialShells");
        var game = typeof(Unit).Assembly;
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("HarmonyLib.Harmony")).First(t => t != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.artillery.integration" });
        var callback = Activator.CreateInstance(harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod"), new object[] { artillery.GetMethod("Aim", Static) });
        var patch = harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
        patch.Invoke(harmony, new object[] { typeof(AimSolver).GetMethod("GetAimVector", Fields), callback, null, null, null });
        var preview = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        try
        {
            var shipObject = new GameObject("Artillery lead target"); shipObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(shipObject, preview);
            shipObject.transform.position = new Vector3(50000, 1000, 55000);
            var ship = shipObject.AddComponent<Ship>();
            var rb = shipObject.AddComponent<Rigidbody>(); rb.useGravity = false;
            typeof(Unit).GetField("<rb>k__BackingField", Fields).SetValue(ship, rb);
            foreach (string weaponName in new[] { "203mmEmplacement", "203mmEmplacement_SPECIAL" })
            {
                var info = AssetDatabase.LoadAssetAtPath<WeaponInfo>("Assets/Blueprinter/Mods/Gunmageddon/" + weaponName + ".asset");
                Check(info.gravMult == 2f, "The user's double gravity was not preserved.");
                var root = new GameObject(weaponName + " arc test"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                root.transform.position = new Vector3(50000, 1000, 50000);
                var owner = root.AddComponent<Building>();
                var gunObject = new GameObject("gun"); gunObject.transform.SetParent(root.transform, false);
                var gun = gunObject.AddComponent<Gun>(); gun.info = info; gun.attachedUnit = owner;
                var muzzle = new GameObject("muzzle").transform; muzzle.SetParent(gunObject.transform, false);
                typeof(Gun).GetField("muzzles", Fields).SetValue(gun, new[] { muzzle });
                typeof(Gun).GetMethod("SetTarget", Fields).Invoke(gun, new object[] { ship });
                var simulation = gunObject.AddComponent<BulletSim>();
                typeof(BulletSim).GetField("gun", Fields).SetValue(simulation, gun);
                typeof(BulletSim).GetField("visualOnly", Fields).SetValue(simulation, false);
                var solver = new AimSolver();
                typeof(AimSolver).GetMethod("SetTarget", Fields).Invoke(solver, new object[] { owner, ship, muzzle, info });
                foreach (var targetVelocity in new[] { Vector3.zero, new Vector3(12f, 0f, 6f) })
                {
                    rb.velocity = targetVelocity;
                    artillery.GetMethod("Shutdown", Static).Invoke(null, null);
                    var aimArgs = new object[] { 0f };
                    Vector3 direction = ((Vector3)typeof(AimSolver).GetMethod("GetAimVector", Fields).Invoke(solver, aimArgs)).normalized;
                    Check(Mathf.Abs(Mathf.Asin(direction.y) * Mathf.Rad2Deg - 23f) < .05f, "Native aiming did not use the lofted solver.");
                    muzzle.rotation = Quaternion.LookRotation(direction);
                    var plan = artillery.GetMethod("GetPlan", Static).Invoke(null, new object[] { gun, ship, muzzle.position });
                    var planType = plan.GetType();
                    float flight = (float)planType.GetField("FlightSeconds", Fields).GetValue(plan);
                    object expectedGlobal = planType.GetField("Aim", Fields).GetValue(plan);
                    var convert = game.GetType("GlobalPositionExtensions").GetMethod("ToLocalPosition");
                    Vector3 expected = (Vector3)convert.Invoke(null, new[] { expectedGlobal });
                    Check(Vector3.Distance(expected, ship.transform.position + targetVelocity * flight) < 1.2f, "Moving ship lead uses the wrong flight time.");
                    var launchArgs = new object[] { simulation, muzzle, direction * 850f, .1f, false, 16f, Color.yellow, null };
                    Check((bool)ranging.GetMethod("BeforeAdd", Static).Invoke(null, launchArgs), "Launch was rejected.");
                    Vector3 velocity = (Vector3)launchArgs[2];
                    Vector3 flat = expected - muzzle.position; flat.y = 0f;
                    Vector3 right = new Vector3(flat.z, 0f, -flat.x).normalized;
                    var correctionArgs = new object[] { gun, ship, muzzle, right, 200f, 50f, velocity };
                    Check((bool)artillery.GetMethod("AdjustLaunch", Static).Invoke(null, correctionArgs), "Ranging correction was rejected.");
                    Vector3 corrected = (Vector3)correctionArgs[6];
                    Vector3 biasedAim = expected + flat.normalized * 200f + right * 50f;
                    Vector3 biasedFlat = biasedAim - muzzle.position; biasedFlat.y = 0f;
                    float correctedAngle = Mathf.Asin(corrected.y / corrected.magnitude) * Mathf.Rad2Deg;
                    var heightArgs = new object[] { correctedAngle, biasedFlat.magnitude, corrected.magnitude, 850f, .01f, 19.62f, .02f, 0f, 0f };
                    Check((bool)a.GetType("GunmageddonAudio.ArtilleryBallistics").GetMethod("HeightAtRange").Invoke(null, heightArgs) &&
                        Mathf.Abs((float)heightArgs[7] - (biasedAim.y - muzzle.position.y)) < 1f, "Ranging offset still assumes stock gravity.");
                    try
                    {
                        Check(Vector3.Angle(velocity, direction) < .02f && velocity.magnitude > 350f && velocity.magnitude < 450f, "Barrel angle and charge disagree.");
                        Check((bool)launchArgs[4] && (float)launchArgs[5] <= 4f, "Artillery projectile visual was not enabled.");
                        if (weaponName.EndsWith("SPECIAL"))
                        {
                            var pending = launchArgs[7];
                            var shot = pending.GetType().GetField("Shot", Fields).GetValue(pending);
                            var carrier = shot.GetType().GetField("Payload", Fields).GetValue(shot);
                            Check((float)carrier.GetType().GetField("Gravity", Fields).GetValue(carrier) == 19.62f, "Clusterlets did not inherit shell gravity.");
                            Vector3 fuseAim = (Vector3)convert.Invoke(null, new[] { carrier.GetType().GetField("Aim", Fields).GetValue(carrier) });
                            Check(Vector3.Distance(fuseAim, expected) < .1f, "Special fuse did not use the led target area.");
                        }
                        var bullet = Activator.CreateInstance(typeof(BulletSim.Bullet), new object[] { velocity, null, 180f, false, false, null });
                        var global = game.GetType("GlobalPositionExtensions").GetMethod("ToGlobalPosition").Invoke(null, new object[] { muzzle.position });
                        typeof(BulletSim.Bullet).GetField("position", Fields).SetValue(bullet, global);
                        var effectsType = game.GetType("ParticleEffectManager+PrefabEffect");
                        var emptyEffects = Array.CreateInstance(effectsType, 4);
                        var trace = typeof(BulletSim.Bullet).GetMethod("TrajectoryTrace", Fields);
                        Vector3 previous = muzzle.position, landed = Vector3.zero;
                        for (int step = 0; step < 6000; step++)
                        {
                            trace.Invoke(bullet, new object[] { null, info, null, emptyEffects, 2f, true, .02f, false,
                                Activator.CreateInstance(game.GetType("GlobalPosition")), Vector3.zero, 60f, false, Activator.CreateInstance(game.GetType("GlobalPosition")) });
                            Vector3 point = (Vector3)convert.Invoke(null, new[] { typeof(BulletSim.Bullet).GetField("position", Fields).GetValue(bullet) });
                            if (point.y <= expected.y && step > 1)
                            {
                                landed = Vector3.Lerp(previous, point, (previous.y - expected.y) / (previous.y - point.y)); break;
                            }
                            Check((bool)typeof(BulletSim.Bullet).GetField("active", Fields).GetValue(bullet), "Native game culled the reduced-charge projectile.");
                            previous = point;
                        }
                        Check(landed != Vector3.zero && Vector3.Distance(landed, expected) < 3f, "Native projectile misses solved target: " + Vector3.Distance(landed, expected));
                        report.AppendLine(weaponName + ": native aim + launch + gravity/drag trace passed; speed " + velocity.magnitude.ToString("F1") +
                            " m/s, flight " + flight.ToString("F1") + " s, target velocity " + targetVelocity + ", miss " + Vector3.Distance(landed, expected).ToString("F2") + " m.");
                    }
                    finally { ranging.GetMethod("AfterAdd", Static).Invoke(null, new object[] { null, launchArgs[7] }); }
                }
            }
            var prefabNames = new[] { "Emplacement_203mmCG", "Emplacement_203mmCG_SPECIAL" };
            foreach (var name in prefabNames)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/" + name + ".prefab");
                var so = new SerializedObject(root.GetComponentInChildren<Turret>(true));
                Check(so.FindProperty("aimSolver.artillery").boolValue && !so.FindProperty("aimSolver.allowHighTrajectory").boolValue &&
                    !so.FindProperty("aimSolver.correctShots").boolValue, "Prefab solver ownership mismatch.");
            }
            report.AppendLine("Passed: both prefab solver settings, both gravity multipliers, moving-target lead, special fuse lead/gravity and compact projectile visuals.");
            return report.ToString();
        }
        finally
        {
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            foreach (var state in ((IDictionary)ranging.GetField("Guns", Static).GetValue(null)).Values)
            {
                var info = state.GetType().GetField("SpottingInfo", Fields).GetValue(state) as UnityEngine.Object;
                if (info) UnityEngine.Object.DestroyImmediate(info);
            }
            artillery.GetMethod("Shutdown", Static).Invoke(null, null);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
