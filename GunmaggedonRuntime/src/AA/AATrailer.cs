using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GunmageddonAudio
{
    internal static class AATrailer
    {
        internal const string Key = "Gunmaggedon_L40_AA";
        internal static readonly List<AATowCoupling> Trailers = new List<AATowCoupling>();
        internal static bool Matches(Unit unit) { return unit && unit.definition && unit.definition.jsonKey == Key; }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(GroundVehicle), "InitializeUnit"), postfix: new HarmonyMethod(typeof(AATrailer), nameof(Initialize)));
            foreach (string method in new[] { "UpdateJobFields", "ApplyJobFields" })
                harmony.Patch(AccessTools.Method(typeof(GroundVehicle), method), prefix: new HarmonyMethod(typeof(AATrailer), nameof(NativeMotion)));
            harmony.Patch(AccessTools.Method(typeof(Gun), "Fire"), prefix: new HarmonyMethod(typeof(AATrailer), nameof(CanFire)));
            // Also cover a shot already queued before the hitch button was pressed.
            harmony.Patch(AccessTools.Method(typeof(Gun), "SpawnBullet"), prefix: new HarmonyMethod(typeof(AATrailer), nameof(CanFire)));
            AAOpticalRanging.Install(harmony);
        }
        static void Initialize(GroundVehicle __instance)
        {
            if (!Matches(__instance)) return;
            if (!__instance.GetComponent<AATowCoupling>()) __instance.gameObject.AddComponent<AATowCoupling>();
            if (!__instance.GetComponent<AACrankMotion>()) __instance.gameObject.AddComponent<AACrankMotion>();
        }
        static bool NativeMotion(GroundVehicle __instance)
        {
            if (!Matches(__instance)) return true;
            var tow = __instance.GetComponent<AATowCoupling>(); return !tow || !tow.Attached;
        }
        static bool CanFire(Gun __instance)
        {
            return !Matches(__instance.attachedUnit) || NativeMotion((GroundVehicle)__instance.attachedUnit);
        }
        internal static void Shutdown()
        {
            foreach (var t in Trailers.ToArray()) if (t) {
                var crank = t.GetComponent<AACrankMotion>(); if (crank) UnityEngine.Object.Destroy(crank);
                UnityEngine.Object.Destroy(t);
            }
            Trailers.Clear(); TowPanel.Shutdown(); AAOpticalRanging.Shutdown();
        }
    }

    public sealed class AACrankMotion : MonoBehaviour
    {
        Transform yaw, pitch, left, right; float oldYaw, oldPitch;
        void Awake()
        {
            var turret = GetComponentInChildren<Turret>(true);
            if (!turret) { enabled = false; return; }
            yaw = turret.transform; pitch = yaw.Find("PitchPivot");
            left = yaw.Find("PitchCrankPivot"); right = yaw.Find("YawCrankPivot");
            if (!pitch || !left || !right) { enabled = false; return; }
            oldYaw = yaw.localEulerAngles.y; oldPitch = pitch.localEulerAngles.x;
        }
        void LateUpdate()
        {
            float y = yaw.localEulerAngles.y, p = pitch.localEulerAngles.x;
            left.Rotate(Vector3.right, -Mathf.DeltaAngle(oldPitch, p) * 12f, Space.Self);
            right.Rotate(Vector3.right, Mathf.DeltaAngle(oldYaw, y) * 6f, Space.Self);
            oldYaw = y; oldPitch = p;
        }
        void OnDestroy() { L40Runtime.Unregister(this); }
    }

    public sealed class AATowCoupling : MonoBehaviour
    {
        internal GroundVehicle Vehicle, Tractor;
        internal bool Attached { get { return Tractor && joint; } }
        ConfigurableJoint joint; Rigidbody body; Transform hook;
        bool oldKinematic, oldGravity, oldHold, oldAnchored, oldWheels; RigidbodyConstraints oldConstraints;
        static readonly FieldInfo Anchored = AccessTools.Field(typeof(GroundVehicle), "anchored");
        static readonly FieldInfo WheelsLocked = AccessTools.Field(typeof(GroundVehicle), "wheelsLocked");
        static readonly FieldInfo ResetStationary = AccessTools.Field(typeof(GroundVehicle), "resetStationary");
        void Awake()
        {
            Vehicle = GetComponent<GroundVehicle>(); body = GetComponent<Rigidbody>(); hook = transform.Find("TowHook");
            AATrailer.Trailers.Add(this);
        }
        internal static bool IsTractor(GroundVehicle v)
        {
            return v && v.definition && (v.definition.jsonKey == "HLT-T" || v.definition.jsonKey == "Truck2-T");
        }
        internal static Vector3 RearHook(GroundVehicle truck)
        {
            var def = truck.definition as VehicleDefinition;
            return truck.transform.TransformPoint(new Vector3(0, -.2f, -(def ? def.length : 10f) * .45f));
        }
        internal bool CanHitch(GroundVehicle truck)
        {
            if (Attached || !Vehicle || !hook || !body || !Vehicle.IsServer || Vehicle.Networkdisabled ||
                !IsTractor(truck) || truck.Networkdisabled || !truck.IsServer || !truck.rb ||
                !Vehicle.NetworkHQ || Vehicle.NetworkHQ != truck.NetworkHQ ||
                body.velocity.sqrMagnitude > 1f || truck.rb.velocity.sqrMagnitude > 1f) return false;
            foreach (var other in AATrailer.Trailers) if (other && other.Attached && other.Tractor == truck) return false;
            return Vector3.Distance(hook.position, RearHook(truck)) <= 3f;
        }
        internal bool Hitch(GroundVehicle truck)
        {
            if (!CanHitch(truck)) return false;
            oldKinematic = body.isKinematic; oldGravity = body.useGravity; oldConstraints = body.constraints;
            oldHold = Vehicle.GetHoldPosition();
            oldAnchored = (bool)Anchored.GetValue(Vehicle); oldWheels = (bool)WheelsLocked.GetValue(Vehicle);
            Vehicle.SetHoldPosition(false); Vehicle.SetWheelsLocked(false); Vehicle.StowTurrets(true);
            Anchored.SetValue(Vehicle, false); Vehicle.NetworknetworkStationary = false;
            body.position += RearHook(truck) - hook.position;
            body.isKinematic = false; body.useGravity = true; body.constraints = RigidbodyConstraints.None;
            Tractor = truck;
            joint = gameObject.AddComponent<ConfigurableJoint>(); joint.connectedBody = truck.rb;
            joint.autoConfigureConnectedAnchor = false; joint.anchor = hook.localPosition;
            joint.connectedAnchor = truck.transform.InverseTransformPoint(RearHook(truck));
            joint.axis = Vector3.right; joint.secondaryAxis = Vector3.up;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -20f }; joint.highAngularXLimit = new SoftJointLimit { limit = 20f };
            joint.angularYLimit = new SoftJointLimit { limit = 70f }; joint.angularZLimit = new SoftJointLimit { limit = 15f };
            joint.projectionMode = JointProjectionMode.PositionAndRotation; joint.projectionDistance = .15f;
            joint.enableCollision = false; return true;
        }
        internal bool Unhitch(bool force = false)
        {
            if (!Tractor && !joint) return false;
            if (!force && Tractor && Tractor.rb && Tractor.rb.velocity.sqrMagnitude > 1f) return false;
            // Disconnect immediately; Destroy alone defers removal until the frame ends.
            if (joint) { joint.connectedBody = null; joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free; Destroy(joint); }
            joint = null; Tractor = null;
            if (body) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.isKinematic = oldKinematic; body.useGravity = oldGravity; body.constraints = oldConstraints; }
            if (Vehicle && !Vehicle.Networkdisabled) {
                Vehicle.SetHoldPosition(oldHold); Vehicle.SetWheelsLocked(oldWheels); Vehicle.StowTurrets(false);
                if (Vehicle.IsServer) AccessTools.Method(typeof(GroundVehicle), "TryAttachToSurface").Invoke(Vehicle, null);
                Anchored.SetValue(Vehicle, oldAnchored); ResetStationary.SetValue(Vehicle, true);
            }
            return true;
        }
        void FixedUpdate()
        {
            if ((joint || Tractor) && (!joint || !Tractor || Tractor.Networkdisabled || Vehicle.Networkdisabled || Tractor.NetworkHQ != Vehicle.NetworkHQ)) Unhitch(true);
        }
        void OnDisable() { Unhitch(true); }
        void OnDestroy() { Unhitch(true); AATrailer.Trailers.Remove(this); }
    }

    internal static class TowPanel
    {
        static readonly FieldInfo Selected = AccessTools.Field(typeof(DynamicMap), "selectedIcons");
        static readonly FieldInfo HQ = AccessTools.Field(typeof(DynamicMap), "HQ");
        static readonly PropertyInfo Maximized = AccessTools.Property(typeof(DynamicMap), "mapMaximized");
        static DynamicMap map; static GameObject panel; static TextMeshProUGUI text; static Button button;
        static AATowCoupling chosen; static GroundVehicle tractor; static float nextUpdate;
        static GroundVehicle[] candidates; static float nextScan;
        internal static void Tick()
        {
            if (Time.unscaledTime < nextUpdate) return; nextUpdate = Time.unscaledTime + .25f;
            chosen = null; tractor = null;
            if (AATrailer.Trailers.Count == 0) { if (panel) panel.SetActive(false); return; }
            if (!map) map = UnityEngine.Object.FindObjectOfType<DynamicMap>();
            if (map && (bool)Maximized.GetValue(Maximized.GetGetMethod(true).IsStatic ? null : map, null))
            {
                var icons = Selected.GetValue(map) as List<MapIcon>;
                var icon = icons != null && icons.Count == 1 ? icons[0] as UnitMapIcon : null;
                var unit = icon ? icon.unit as GroundVehicle : null;
                if (unit && unit.IsServer && unit.NetworkHQ == (HQ.GetValue(map) as FactionHQ))
                {
                    if (AATrailer.Matches(unit)) {
                        chosen = unit.GetComponent<AATowCoupling>();
                        if (chosen && !chosen.Attached) {
                            if (candidates == null || Time.unscaledTime >= nextScan) {
                                candidates = UnityEngine.Object.FindObjectsOfType<GroundVehicle>(); nextScan = Time.unscaledTime + 2f;
                            }
                            foreach (var truck in candidates)
                                if (chosen.CanHitch(truck) && (!tractor || Vector3.SqrMagnitude(AATowCoupling.RearHook(truck)-chosen.transform.position) < Vector3.SqrMagnitude(AATowCoupling.RearHook(tractor)-chosen.transform.position))) tractor = truck;
                        }
                    } else if (AATowCoupling.IsTractor(unit)) {
                        foreach (var t in AATrailer.Trailers) if (t && t.Attached && t.Tractor == unit) { chosen = t; break; }
                        if (!chosen) foreach (var t in AATrailer.Trailers) if (t && t.CanHitch(unit)) { chosen = t; tractor = unit; break; }
                    }
                }
            }
            if (!chosen) { if (panel) panel.SetActive(false); return; }
            if (!panel) Create(); panel.SetActive(true);
            text.text = chosen.Attached ? "Unhitch L40 AA trailer" : tractor ? "Hitch L40 AA trailer" : "Move a stopped tractor within 3m of hitch";
            button.interactable = chosen.Attached ? chosen.Tractor.rb && chosen.Tractor.rb.velocity.sqrMagnitude <= 1f : tractor;
        }
        static void Create()
        {
            panel = new GameObject("Gunmaggedon towing", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            panel.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; panel.GetComponent<Canvas>().sortingOrder = 5000;
            var scaler = panel.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            var g = new GameObject("Tow control", typeof(RectTransform), typeof(Image), typeof(Button));g.transform.SetParent(panel.transform,false);
            var r = g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,30);r.sizeDelta=new Vector2(450,60);
            g.GetComponent<Image>().color = new Color(.08f,.1f,.12f,.95f);button=g.GetComponent<Button>();
            var label = new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(g.transform,false);
            var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(8,4);lr.offsetMax=new Vector2(-8,-4);
            text=label.GetComponent<TextMeshProUGUI>();text.fontSize=20;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            button.onClick.AddListener(()=>{if(!chosen)return;if(chosen.Attached)chosen.Unhitch();else if(tractor)chosen.Hitch(tractor);nextUpdate=0;});
        }
        internal static void Shutdown() {if(panel)UnityEngine.Object.Destroy(panel);panel=null;map=null;chosen=null;tractor=null;candidates=null;nextUpdate=nextScan=0;}
    }
}
