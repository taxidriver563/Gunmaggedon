using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IlluminationIntegration
{
    public static object Run()
    {
        const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var assembly = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/1.5.4/GunmageddonAudio.dll")));
        var game = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("Gun") != null);
        var feature = assembly.GetType("GunmageddonAudio.CoastalIllumination");
        var ranging = assembly.GetType("GunmageddonAudio.CoastalRanging");
        UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/pilotDismounted_PLACEHOLDER.prefab");
        if (!(bool)feature.GetMethod("EnsureVisuals", Hidden).Invoke(null, null)) throw new Exception("Native parachute lookup failed.");
        var native = feature.GetField("nativeParachute", Hidden).GetValue(null);
        var glow = (Material)feature.GetField("glow", Hidden).GetValue(null);

        var toGlobal = game.GetType("GlobalPositionExtensions").GetMethod("ToGlobalPosition");
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject obstacle = null;
        var report = new StringBuilder();
        try
        {
            var environmentObject = new GameObject("Clock and cloud policy test");
            environmentObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(environmentObject, preview);
            var environment = environmentObject.AddComponent(game.GetType("LevelInfo"));
            var cloud = environmentObject.AddComponent(game.GetType("CloudLayer"));
            cloud.transform.position = Vector3.up * 1000;
            var levelType = environment.GetType();
            levelType.GetField("cloudLayer", Fields).SetValue(environment, cloud);
            levelType.GetField("conditions", Fields).SetValue(environment, 0f);
            var hour = levelType.GetField("timeOfDay", Fields);
            var lowLight = feature.GetMethod("IsLowLight", Hidden);
            hour.SetValue(environment, 0f);
            if (!(bool)lowLight.Invoke(null, new object[] { environment, Vector3.zero, .35f }))
                throw new Exception("The game clock's midnight did not require illumination.");
            hour.SetValue(environment, 12f);
            if ((bool)lowLight.Invoke(null, new object[] { environment, Vector3.zero, .35f }))
                throw new Exception("Clear noon incorrectly required illumination.");
            levelType.GetField("conditions", Fields).SetValue(environment, .7f);
            if ((bool)lowLight.Invoke(null, new object[] { environment, Vector3.zero, .35f }))
                throw new Exception("Dense daytime overcast authorized flares.");
            foreach (float visibleHour in new[] { 6f, 9f, 15f, 18f, 18.3f })
            {
                hour.SetValue(environment, visibleHour);
                if ((bool)lowLight.Invoke(null, new object[] { environment, Vector3.zero, .9f }))
                    throw new Exception("Visible clock conditions authorized flares: " + visibleHour);
            }
            foreach (float darkHour in new[] { 0f, 5f, 18.48f, 21f })
            {
                hour.SetValue(environment, darkHour);
                if (!(bool)lowLight.Invoke(null, new object[] { environment, Vector3.zero, .15f }))
                    throw new Exception("Dark clock conditions did not authorize flares: " + darkHour);
            }
            report.AppendLine("Passed: native clock midnight/noon, daytime overcast rejection, visible dawn/dusk rejection even with old/high thresholds, and dark-hour authorization.");

            var shipObject = new GameObject("Illumination gate test ship");
            shipObject.SetActive(false); // Do not initialize the game's networking/mission machinery.
            SceneManager.MoveGameObjectToScene(shipObject, preview);
            shipObject.transform.position = new Vector3(50000, 0, 50000);
            var ship = shipObject.AddComponent(game.GetType("Ship"));
            var go = new GameObject("Illumination visual and fire-gate test");
            SceneManager.MoveGameObjectToScene(go, preview);
            var flare = go.AddComponent(assembly.GetType("GunmageddonAudio.ParachuteIllumination"));
            var location = toGlobal.Invoke(null, new object[] { new Vector3(50000, 600, 50000) });
            flare.GetType().GetMethod("Initialize").Invoke(flare,
                new object[] { location, new Vector3(300, 80, 0), 120f, 1000f, 2.5f, glow, native });
            var light = go.GetComponent<Light>();
            var born = flare.GetType().GetField("born", Fields);
            var illuminates = flare.GetType().GetMethod("Illuminates");
            light.intensity = 200000f;
            born.SetValue(flare, Time.time - .2f);
            if ((bool)illuminates.Invoke(flare, new object[] { ship })) throw new Exception("Unignited flare unlocked fire.");
            born.SetValue(flare, Time.time - 2f);
            if (!(bool)illuminates.Invoke(flare, new object[] { ship })) throw new Exception("Working overhead flare did not illuminate target.");
            light.enabled = false;
            if ((bool)illuminates.Invoke(flare, new object[] { ship })) throw new Exception("Disabled light unlocked fire.");
            light.enabled = true;
            shipObject.transform.position += Vector3.right * 2000;
            if ((bool)illuminates.Invoke(flare, new object[] { ship })) throw new Exception("Ship outside light range unlocked fire.");
            shipObject.transform.position -= Vector3.right * 2000;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Temporary illumination occlusion test";
            obstacle.transform.position = new Vector3(50000, 300, 50000);
            obstacle.transform.localScale = Vector3.one * 20;
            Physics.SyncTransforms();
            if ((bool)illuminates.Invoke(flare, new object[] { ship })) throw new Exception("An occluded target unlocked fire.");
            UnityEngine.Object.DestroyImmediate(obstacle);
            obstacle = null;
            Physics.SyncTransforms();
            born.SetValue(flare, Time.time - 119f);
            if ((bool)illuminates.Invoke(flare, new object[] { ship })) throw new Exception("Burned-out flare unlocked fire.");
            var meshes = go.GetComponentsInChildren<MeshFilter>();
            if (!meshes.Any(m => m.sharedMesh.name == "parachute1_canopy") ||
                !meshes.Any(m => m.sharedMesh.name == "parachute1_lines"))
                throw new Exception("Actual game parachute canopy or lines missing.");
            var velocity = (Vector3)flare.GetType().GetField("velocity", Fields).GetValue(flare);
            if (velocity != new Vector3(300, 80, 0)) throw new Exception("Carrier momentum lost at deployment.");
            flare.GetType().GetMethod("UpdateCanopy", Fields).Invoke(flare, new object[] { 2f });
            var canopyTransform = go.transform.Find("Native parachute canopy");
            if (canopyTransform.localScale.x < .99f) throw new Exception("Native opening curve did not inflate canopy.");
            var linesTransform = go.transform.Find("Native parachute suspension lines");
            if (Vector3.Distance(linesTransform.TransformPoint(Vector3.forward), canopyTransform.position) > .01f ||
                Vector3.Distance(linesTransform.position, go.transform.position) > .01f)
                throw new Exception("Native suspension lines do not connect the payload to the canopy rim.");
            report.AppendLine("Passed: ignition, overhead illumination, disabled light, range, line-of-sight obstruction, burnout, canopy and cords.");

            var planType = feature.GetNestedType("Plan", BindingFlags.NonPublic);
            var plan = Activator.CreateInstance(planType, true);
            planType.GetField("EstimatedPosition", Fields).SetValue(plan,
                toGlobal.Invoke(null, new object[] { new Vector3(50000, 0, 50000) }));
            planType.GetField("DeploymentAltitude", Fields).SetValue(plan, 600f);
            planType.GetField("FlightSeconds", Fields).SetValue(plan, 10f);
            var fuse = feature.GetMethod("AtDeployment", Hidden);
            var tooLow = toGlobal.Invoke(null, new object[] { new Vector3(50000, 100, 50000) });
            if ((bool)fuse.Invoke(null, new object[] { tooLow, Vector3.zero, plan, 10f })) throw new Exception("Flare deployed too low.");
            if (!(bool)fuse.Invoke(null, new object[] { location, Vector3.zero, plan, 10f })) throw new Exception("Flare did not deploy overhead.");
            var upstream = toGlobal.Invoke(null, new object[] { new Vector3(49700, 500, 50000) });
            if (!(bool)fuse.Invoke(null, new object[] { upstream, new Vector3(300, 0, 0), plan, 8f }))
                throw new Exception("Fast carrier did not begin deploying upstream.");
            if ((bool)fuse.Invoke(null, new object[] { upstream, new Vector3(-300, 0, 0), plan, 8f }))
                throw new Exception("Carrier moving away from target deployed prematurely.");
            report.AppendLine("Passed: early upstream deployment accounts for opening coast, altitude and approach direction.");
            var motion = assembly.GetType("GunmageddonAudio.ParachuteMotion");
            var advance = motion.GetMethod("Advance", Hidden);
            Vector3 lastPosition = Vector3.zero, lastVelocity = Vector3.zero;
            foreach (int hz in new[] { 30, 60, 144 })
            {
                Vector3 v = new Vector3(300, 80, 0), p = Vector3.zero;
                for (int i = 0; i < hz * 8; i++)
                {
                    var args = new object[] { v, Vector3.down * 2.5f, (float)i / hz, 1f / hz };
                    p += (Vector3)advance.Invoke(null, args);
                    var next = (Vector3)args[0];
                    if (i == 0 && next.x < 290) throw new Exception("Abrupt first-frame momentum stop.");
                    if (next.x > v.x + .001f || next.x < 0) throw new Exception("Unstable drag.");
                    v = next;
                }
                if (Mathf.Abs(v.y + 2.5f) > .01f || Mathf.Abs(v.x) > .01f) throw new Exception("Terminal descent failed.");
                if (lastPosition != Vector3.zero && Vector3.Distance(lastPosition, p) > .1f) throw new Exception("Frame-rate dependent coast.");
                lastPosition = p; lastVelocity = v;
            }
            report.AppendLine("Passed: preserved initial velocity, smooth deceleration, terminal descent and 30/60/144 FPS agreement.");

            var stateType = ranging.GetNestedType("GunState", BindingFlags.NonPublic);
            var shotType = ranging.GetNestedType("Shot", BindingFlags.NonPublic);
            var state = Activator.CreateInstance(stateType, true);
            var shot = Activator.CreateInstance(shotType, true);
            var realInfo = UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Blueprinter/Mods/Gunmageddon/203mmEmplacement.asset");
            var harmless = UnityEngine.Object.Instantiate(realInfo);
            var serialized = new UnityEditor.SerializedObject(harmless);
            serialized.FindProperty("pierceDamage").floatValue = 0;
            serialized.FindProperty("blastDamage").floatValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            stateType.GetField("SpottingInfo", Fields).SetValue(state, harmless);
            shotType.GetField("State", Fields).SetValue(shot, state);
            shotType.GetField("Illumination", Fields).SetValue(shot, true);
            var bullet = Activator.CreateInstance(game.GetType("BulletSim+Bullet"),
                new object[] { new Vector3(0, 0, 850), null, 180f, false, true, null });
            var shots = (IDictionary)ranging.GetField("Shots", Hidden).GetValue(null);
            shots.Add(bullet, shot);
            try
            {
                var args = new object[] { bullet, realInfo, null, false, null };
                ranging.GetMethod("BeforeTrace", Hidden).Invoke(null, args);
                if (args[1] != harmless || !(bool)args[3]) throw new Exception("Illumination shell retained live damage.");
                report.AppendLine("Passed: illumination carrier is non-damaging and separate from the ranging-shot counter.");
            }
            finally { shots.Remove(bullet); UnityEngine.Object.DestroyImmediate(harmless); }
            return report.ToString();
        }
        finally
        {
            if (obstacle) UnityEngine.Object.DestroyImmediate(obstacle);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
