using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class SearchlightRuntime
    {
        internal const string Key="Gunmaggedon_Searchlight";
        internal const float CoastalSupportRadius=2000f;
        static Harmony harmony;
        static ManualLogSource log;
        static readonly Dictionary<Gun,float> BatteryPolls=new Dictionary<Gun,float>();
        static readonly Dictionary<Gun,string> BatteryStatus=new Dictionary<Gun,string>();
        static readonly List<TrackingInfo> KnownShips=new List<TrackingInfo>();
        static readonly System.Reflection.FieldInfo KnownUnit=AccessTools.Field(typeof(TrackingInfo),"unit");
        static readonly List<Gun> ExpiredBatteries=new List<Gun>();
        static float nextBatteryCleanup;
        static readonly List<SearchlightController> Controllers=new List<SearchlightController>();
        internal static readonly SearchlightClaims<FactionHQ,Unit,SearchlightController> Claims=new SearchlightClaims<FactionHQ,Unit,SearchlightController>();
        internal sealed class CoastalRequest
        {
            internal Gun Gun;internal Unit Target;internal FactionHQ Team;
            internal GlobalPosition Position;internal Vector3 Velocity;
            internal float Updated,Created;
            internal bool Valid(float now) {return Gun&&Gun.attachedUnit&&Gun.attachedUnit.LocalSim&&!Gun.attachedUnit.Networkdisabled&&
                Gun.attachedUnit.gameObject.activeInHierarchy&&Gun.attachedUnit.NetworkHQ==Team&&Team&&Target is Ship&&
                Target&&!Target.Networkdisabled&&Target.gameObject.activeInHierarchy&&Target.NetworkHQ&&Target.NetworkHQ!=Team&&now-Updated<3f;}
            internal Vector3 Estimate(float now){return Position.ToLocalPosition()+Velocity*Mathf.Clamp(now-Updated,0,2f)+Vector3.up*8f;}
            internal bool InRangeOf(Vector3 lampPosition){return Gun&&Gun.attachedUnit&&
                (Gun.attachedUnit.transform.position-lampPosition).sqrMagnitude<=CoastalSupportRadius*CoastalSupportRadius;}
        }
        static readonly Dictionary<Gun,CoastalRequest> CoastalRequests=new Dictionary<Gun,CoastalRequest>();
        static readonly List<Gun> ExpiredRequests=new List<Gun>();
        // Native weapon AI may reject a dark contact before aiming or Fire runs.
        // Request lighting from faction-known contacts independently of that gate.
        static void RefreshBattery(Gun __instance)
        {
            var gun=__instance;
            if(!CoastalArtillery.IsSupported(gun)||!gun.attachedUnit.LocalSim)return;
            if(Time.time>=nextBatteryCleanup){nextBatteryCleanup=Time.time+5f;ExpiredBatteries.Clear();
                foreach(var battery in BatteryPolls.Keys)if(!battery)ExpiredBatteries.Add(battery);
                foreach(var battery in ExpiredBatteries){BatteryPolls.Remove(battery);BatteryStatus.Remove(battery);ReleaseCoastal(battery);}ExpiredBatteries.Clear();}
            float next;if(BatteryPolls.TryGetValue(gun,out next)&&Time.time<next)return;
            BatteryPolls[gun]=Time.time+.5f;
            var owner=gun.attachedUnit;var team=owner.NetworkHQ;
            if(owner.Networkdisabled||!owner.gameObject.activeInHierarchy||!team||!CoastalIllumination.NeedsLight(owner))
            {ReleaseCoastal(gun);return;}
            bool nearby=false;foreach(var lamp in Controllers)if(lamp&&lamp.NearbyBattery(owner)){nearby=true;break;}
            if(!nearby){ReleaseCoastal(gun);ReportBattery(gun,"no operational friendly searchlight within 2km");return;}
            var target=gun.GetTarget();GlobalPosition known=default(GlobalPosition);
            if(!(target is Ship)||!target.NetworkHQ||target.NetworkHQ==team||target.Networkdisabled||!team.TryGetKnownPosition(target,out known)) {
                target=null;float best=float.MaxValue;
                KnownShips.Clear();team.GetTargetsWithinRange(KnownShips,owner.transform,15000f,false);
                foreach(var contact in KnownShips) {
                    if(ReferenceEquals(contact,null))continue;
                    var candidate=KnownUnit.GetValue(contact) as Unit;
                    if(!(candidate is Ship)||candidate.Networkdisabled||!candidate.gameObject.activeInHierarchy||!candidate.NetworkHQ||candidate.NetworkHQ==team||!team.TryGetKnownPosition(candidate,out known))continue;
                    float distance=(known.ToLocalPosition()-owner.transform.position).sqrMagnitude;
                    if(distance<best){target=candidate;best=distance;}
                }
                KnownShips.Clear();
            }
            if(!target){ReleaseCoastal(gun);ReportBattery(gun,"no faction-known enemy ship contact to illuminate");return;}
            CoastalIllumination.Observe(gun,target);
            ReportRequest(gun,target);
        }
        static void ReportBattery(Gun gun,string status)
        {
            string previous;if(BatteryStatus.TryGetValue(gun,out previous)&&previous==status)return;
            BatteryStatus[gun]=status;log?.LogInfo("Coastal support ["+gun.attachedUnit.name+"]: "+status+".");
        }
        static void ReportRequest(Gun gun,Unit target)
        {
            CoastalRequest request;
            if(!CoastalRequests.TryGetValue(gun,out request)||!request.Valid(Time.time))
            {ReportBattery(gun,"no active low-light illumination request for "+target.name);return;}
            string status=null;
            foreach(var lamp in Controllers)if(lamp&&lamp.NearbyBattery(gun.attachedUnit)) {
                string candidate=lamp.SupportStatus(request);
                if(candidate=="tracking requested ship"){status=candidate;break;}
                if(status==null||candidate=="ready to traverse")status=candidate;
            }
            ReportBattery(gun,"illumination of "+target.name+": "+(status??"no operational nearby lamp"));
        }
        internal static void Accepted(SearchlightController lamp,CoastalRequest request)
        {log?.LogInfo("Searchlight ["+lamp.name+"] accepting coastal illumination request for "+request.Target.name+".");}
        internal static void RequestCoastal(Gun gun,Unit target,GlobalPosition position,Vector3 velocity)
        {
            if(!gun||!gun.attachedUnit||!gun.attachedUnit.LocalSim||!(target is Ship)||!CoastalIllumination.NeedsLight(target))return;
            PruneRequests();CoastalRequest request;bool created=false;
            if(!CoastalRequests.TryGetValue(gun,out request)||request.Target!=target) {
                request=new CoastalRequest{Gun=gun,Target=target,Team=gun.attachedUnit.NetworkHQ,Created=Time.time};
                CoastalRequests[gun]=request;created=true;
            }
            request.Position=position;request.Velocity=velocity;request.Updated=Time.time;
            // Reserve an idle lamp before this firing decision can allocate a
            // flare. The physical lamp must still traverse and acquire.
            if(created&&request.Valid(Time.time))foreach(var lamp in Controllers)if(lamp)lamp.OfferCoastal();
        }
        internal static void ReleaseCoastal(Gun gun){if(!ReferenceEquals(gun,null))CoastalRequests.Remove(gun);}
        internal static void ReleaseCoastalIfChanged(Gun gun)
        {
            // A dark contact may be rejected by native weapon AI. Keep the
            // independent lighting task alive until refresh/expiry withdraws it.
            CoastalRequest r;var target=gun?gun.GetTarget():null;
            if(target&&CoastalRequests.TryGetValue(gun,out r)&&target!=r.Target)CoastalRequests.Remove(gun);
        }
        static void PruneRequests()
        {
            ExpiredRequests.Clear();foreach(var pair in CoastalRequests)if(!pair.Value.Valid(Time.time))ExpiredRequests.Add(pair.Key);
            foreach(var gun in ExpiredRequests)CoastalRequests.Remove(gun);ExpiredRequests.Clear();
        }
        internal static CoastalRequest CoastalFor(FactionHQ team,Unit target)
        {return FindCoastal(team,target,null);}
        internal static CoastalRequest CoastalForLamp(FactionHQ team,Unit target,Vector3 lampPosition)
        {return FindCoastal(team,target,lampPosition);}
        static CoastalRequest FindCoastal(FactionHQ team,Unit target,Vector3? lampPosition)
        {
            CoastalRequest newest=null;
            foreach(var pair in CoastalRequests) {
                var r=pair.Value;
                if(r.Team==team&&r.Target==target&&r.Valid(Time.time)&&(!lampPosition.HasValue||r.InRangeOf(lampPosition.Value))&&
                    (newest==null||r.Updated>newest.Updated))newest=r;
            }
            return newest;
        }
        internal static bool TryCoastalCue(SearchlightController lamp,out CoastalRequest result)
        {
            PruneRequests();result=null;float best=float.MaxValue;
            foreach(var pair in CoastalRequests) {
                float score;if(!lamp.CanSupport(pair.Value,out score)||score>=best)continue;
                best=score;result=pair.Value;
            }
            return result!=null;
        }
        internal static bool AwaitingCoastal(Gun gun,Unit target)
        {
            CoastalRequest r;
            if(!CoastalRequests.TryGetValue(gun,out r)||r.Target!=target||!r.Valid(Time.time)||Time.time-r.Created>=12f)return false;
            foreach(var c in Controllers)if(c&&r.InRangeOf(c.transform.position)&&c.AcquiringCoastal(r.Team,target))return true;
            return false;
        }
        internal static bool IlluminatesAircraft(FactionHQ team,Unit target)
        {foreach(var c in Controllers)if(c&&c.IlluminatesAircraft(team,target))return true;return false;}
        internal static bool IlluminatesCoastal(Unit target)
        {foreach(var c in Controllers)if(c&&c.IlluminatesCoastal(target))return true;return false;}
        internal static bool CoversCoastal(FactionHQ team,Unit target,Vector3 predicted,float seconds)
        {foreach(var c in Controllers)if(c&&c.CoversCoastal(team,target,predicted,seconds))return true;return false;}
        internal static bool Matches(Unit unit) {return unit&&unit.definition&&unit.definition.jsonKey==Key;}
        internal static void Install(ManualLogSource logger)
        {
            log=logger;
            harmony=new Harmony("gunmageddon.searchlight");
            try {
                harmony.Patch(AccessTools.Method(typeof(TargetDetector),"TargetDetector_OnInitialize"),postfix:new HarmonyMethod(typeof(SearchlightRuntime),nameof(Initialize)));
                harmony.Patch(AccessTools.Method(typeof(TargetDetector),"VisualCheck"),prefix:new HarmonyMethod(typeof(SearchlightRuntime),nameof(VisualCheck)));
                harmony.Patch(AccessTools.Method(typeof(Gun),"Update"),postfix:new HarmonyMethod(typeof(SearchlightRuntime),nameof(RefreshBattery)));
                logger.LogInfo("Searchlight night sweep, 300m radar cues and 2km coastal support with aircraft priority enabled.");
            }catch(Exception e){harmony.UnpatchSelf();logger.LogError("Searchlight initialization failed: "+e);}
        }
        static bool VisualCheck(TargetDetector __instance){return !Matches(__instance.GetAttachedUnit());}
        static void Initialize(TargetDetector __instance)
        {
            var owner=__instance.GetAttachedUnit();if(!Matches(owner)||owner.GetComponent<SearchlightController>())return;
            var controller=owner.gameObject.AddComponent<SearchlightController>();Controllers.Add(controller);
        }
        internal static void Unregister(SearchlightController c){Claims.Release(c);Controllers.Remove(c);}
        internal static void Shutdown(){harmony?.UnpatchSelf();harmony=null;foreach(var c in Controllers.ToArray())if(c)UnityEngine.Object.Destroy(c);Controllers.Clear();Claims.Clear();CoastalRequests.Clear();ExpiredRequests.Clear();BatteryPolls.Clear();BatteryStatus.Clear();KnownShips.Clear();ExpiredBatteries.Clear();nextBatteryCleanup=0f;log=null;}
    }

    public sealed class SearchlightController : MonoBehaviour
    {
        GroundVehicle owner; TargetDetector detector; Transform yaw,pitch,origin;MeshRenderer beam,lens;Light spot;
        List<Unit> nearby;
        List<Radar> support;
        RaycastHit[] hits;
        MaterialPropertyBlock properties;
        SearchlightSolution solution;
        Unit attention;float nextCandidates,nextSupport,nextSample,lastSample,nextReport,phase,lastCueAt;
        Vector3 cue;bool active,radarCued,acousticCued;float reach;
        float yawVelocity,pitchVelocity,lastVisualAt=float.NegativeInfinity,nextAcousticAt,acousticWidth;
        static readonly System.Reflection.FieldInfo Engines=AccessTools.Field(typeof(Aircraft),"engines");
        bool HasVisualTrack {get{return attention is Aircraft&&Time.time-lastVisualAt<=.3f;}}
        float claimStarted,lastSeen,retryAfter;bool claimConfirmed;Unit retryTarget;
        bool coastalVisible;float coastalSampleAt;
        static readonly Func<SearchlightController,FactionHQ,Unit,bool> ValidClaim=ClaimValid;
        static bool ClaimValid(SearchlightController c,FactionHQ team,Unit target)
        {
            return c&&c.enabled&&c.active&&c.owner&&!c.owner.Networkdisabled&&c.owner.gameObject.activeSelf&&
                c.owner.NetworkHQ==team&&c.attention==target&&c.Enemy(target);
        }
        bool Available(Unit target){return target&&!(target==retryTarget&&Time.time<retryAfter)&&SearchlightRuntime.Claims.Available(owner.NetworkHQ,target,this,Time.time,ValidClaim);}
        bool Focus(Unit target)
        {
            if(!Available(target))return false;
            if(attention!=target)Lose();
            if(!SearchlightRuntime.Claims.Take(owner.NetworkHQ,target,this,Time.time,ValidClaim))return false;
            if(attention!=target){attention=target;solution.Reset();claimStarted=lastSeen=Time.time;claimConfirmed=false;lastVisualAt=float.NegativeInfinity;acousticCued=false;}
            return true;
        }
        void Awake()
        {
            solution=new SearchlightSolution();nearby=new List<Unit>();support=new List<Radar>();hits=new RaycastHit[64];properties=new MaterialPropertyBlock();
            owner=GetComponent<GroundVehicle>();detector=GetComponentInChildren<TargetDetector>(true);
            foreach(var t in GetComponentsInChildren<Transform>(true)) {
                if(t.name=="SearchlightYaw")yaw=t;else if(t.name=="LampPitch")pitch=t;else if(t.name=="BeamOrigin")origin=t;
                else if(t.name=="SearchlightBeam")beam=t.GetComponent<MeshRenderer>();else if(t.name=="LensGlow")lens=t.GetComponent<MeshRenderer>();
            }
            if(origin)spot=origin.GetComponentInChildren<Light>(true);
            if(!owner||!detector||!yaw||!pitch||!origin||!beam||!lens||!spot){enabled=false;return;}
            // Pack 0.0.19 already has a 13km cone mesh; use its full reach
            // for both targeting and per-instance visual/light clipping.
            reach=13000f;
            phase=(GetInstanceID()&1023)*.17f;lastSample=Time.time;
            SetActive(false);
        }
        void SetActive(bool value){if(!value)yawVelocity=pitchVelocity=0;active=value;if(beam)beam.enabled=value;if(lens)lens.enabled=value;if(spot)spot.enabled=value;}
        bool Enemy(Unit u){return u&&(u is Aircraft||(u is Ship&&SearchlightRuntime.CoastalForLamp(owner.NetworkHQ,u,transform.position)!=null))&&!u.Networkdisabled&&u.gameObject.activeSelf&&owner.NetworkHQ&&u.NetworkHQ&&owner.NetworkHQ!=u.NetworkHQ;}
        static Vector3 AimPoint(Unit u){return u.transform.position+(u is Ship?Vector3.up*8f:Vector3.zero);}
        void CoastalCoverage(bool value){if(coastalVisible!=value)CoastalIllumination.CoverageChanged();coastalVisible=value;coastalSampleAt=Time.time;}
        void Lose(){SearchlightRuntime.Claims.Release(this);CoastalCoverage(false);attention=null;radarCued=acousticCued=false;lastVisualAt=float.NegativeInfinity;claimConfirmed=false;solution?.Reset();if(detector&&detector.detectedTargets!=null)detector.detectedTargets.Clear();}
        bool Operational {get{return enabled&&active&&owner&&owner.LocalSim&&!owner.Networkdisabled&&owner.gameObject.activeInHierarchy&&detector&&detector.IsOperational()&&spot&&spot.enabled&&(!owner.rb||owner.rb.velocity.sqrMagnitude<4f);}}
        internal bool NearbyBattery(Unit battery){return Operational&&battery&&battery.NetworkHQ==owner.NetworkHQ&&
            (battery.transform.position-transform.position).sqrMagnitude<=SearchlightRuntime.CoastalSupportRadius*SearchlightRuntime.CoastalSupportRadius;}
        internal string SupportStatus(SearchlightRuntime.CoastalRequest request)
        {
            if(!Operational)return "lamp not operational";
            if(attention is Aircraft)return "aircraft takes priority";
            if(attention==request.Target)return "tracking requested ship";
            if(attention is Ship)return "lamp busy supporting another ship";
            if(!Available(request.Target))return "ship claimed by another lamp or acquisition retry pending";
            Vector3 delta=transform.InverseTransformDirection(request.Estimate(Time.time)-origin.position);
            if(delta.sqrMagnitude>reach*reach)return "ship outside beam range ("+Mathf.RoundToInt(reach)+"m)";
            if(!CanReach(request.Estimate(Time.time)))return "ship outside mechanical elevation limits";
            return "ready to traverse";
        }
        internal bool CanSupport(SearchlightRuntime.CoastalRequest request,out float distance)
        {
            distance=float.MaxValue;
            if(!Operational||attention is Aircraft||request.Team!=owner.NetworkHQ||!request.InRangeOf(transform.position)||!Available(request.Target))return false;
            Vector3 estimated=request.Estimate(Time.time);distance=(estimated-origin.position).sqrMagnitude;
            return CanReach(estimated);
        }
        internal bool AcquiringCoastal(FactionHQ team,Unit target)
        {return Operational&&owner.NetworkHQ==team&&attention==target&&target is Ship&&!claimConfirmed&&Time.time-claimStarted<12f&&Enemy(target);}
        internal bool IlluminatesAircraft(FactionHQ team,Unit target)
        {return Operational&&target is Aircraft&&attention==target&&owner.NetworkHQ==team&&claimConfirmed&&solution.Acquired&&Time.time-lastSeen<.25f&&Inside(target,reach)&&Clear(target);}
        internal bool IlluminatesCoastal(Unit target)
        {return Operational&&target is Ship&&attention==target&&coastalVisible&&claimConfirmed&&Time.time-coastalSampleAt<=.25f&&Enemy(target);}
        internal bool CoversCoastal(FactionHQ team,Unit target,Vector3 predicted,float seconds)
        {
            if(!IlluminatesCoastal(target)||owner.NetworkHQ!=team)return false;
            Vector3 destination=predicted+Vector3.up*8f;
            if(!CanReach(destination,.95f)||!ClearPoint(destination,target))return false;
            // Leave tracking-rate headroom for a moving ship rather than promising
            // coverage simply because its future position remains in weapon range.
            float change=Vector3.Angle(origin.forward,destination-origin.position);
            return change<=1f+12f*Mathf.Max(0f,seconds);
        }
        bool CanReach(Vector3 point,float rangeFactor=1f)
        {
            Vector3 v=transform.InverseTransformDirection(point-origin.position);
            float elevation=Mathf.Atan2(v.y,new Vector2(v.x,v.z).magnitude)*Mathf.Rad2Deg;
            return v.sqrMagnitude<=reach*reach*rangeFactor*rangeFactor&&elevation>=-85f&&elevation<=85f;
        }
        void CoastalCue()
        {
            if(attention is Ship) {
                var r=SearchlightRuntime.CoastalForLamp(owner.NetworkHQ,attention,transform.position);
                if(r!=null&&!coastalVisible)cue=r.Estimate(Time.time);
                return;
            }
            if(attention)return;
            SearchlightRuntime.CoastalRequest request;
            if(SearchlightRuntime.TryCoastalCue(this,out request)&&Focus(request.Target)){
                cue=request.Estimate(Time.time);radarCued=false;
                // This is a search sector cue, not an automatic target detection.
                SearchlightRuntime.Accepted(this,request);
            }
        }
        internal void OfferCoastal()
        {
            if(!Operational||attention)return;
            RadarCue();OpticalCue(reach);AcousticCue();CoastalCue();
        }
        void Update()
        {
            if(!owner||!detector||!origin)return;
            bool dark=CoastalIllumination.IsLowLight(LevelInfo.i,origin.position,.15f);
            bool on=dark&&!owner.Networkdisabled&&detector.IsOperational()&&(!owner.rb||owner.rb.velocity.sqrMagnitude<4f);
            if(!on){if(active){SetActive(false);Lose();}return;}
            if(!active){SetActive(true);lastSample=Time.time;}
            // Target evidence and faction reports are authoritative. Remote presentation
            // currently uses a local sweep, without publishing extra contacts.
            if(owner.LocalSim&&Time.time>=nextCandidates){nextCandidates=Time.time+.5f;nearby.Clear();SharedWorldQueries.Units(owner.GlobalPosition(),reach,nearby);}
            if(owner.LocalSim&&Time.time>=nextSupport){nextSupport=Time.time+1;FindSupport();}
            if(owner.LocalSim&&attention&&(!Enemy(attention)||Vector3.Distance(origin.position,attention.transform.position)>reach))Lose();
            if(owner.LocalSim&&attention) {
                bool timeout=!claimConfirmed&&Time.time-claimStarted>=12f;
                bool lost=claimConfirmed&&Time.time-lastSeen>.6f;
                if(timeout||lost){retryTarget=attention;retryAfter=Time.time+(timeout?5f:2f);}
                if(timeout||lost||!SearchlightRuntime.Claims.Take(owner.NetworkHQ,attention,this,Time.time,ValidClaim))Lose();
            }
            if(owner.LocalSim&&Time.time>=lastCueAt+.5f){lastCueAt=Time.time;RadarCue();OpticalCue(reach);AcousticCue();CoastalCue();}
            Aim(Time.deltaTime);
            if(Time.time<nextSample)return;nextSample=Time.time+.1f;
            float dt=Mathf.Min(.25f,Time.time-lastSample);lastSample=Time.time;
            float length=Clip();ApplyLength(length);
            if(!owner.LocalSim)return;
            OpticalCue(length);
            bool visible=attention&&Inside(attention,length)&&Clear(attention);
            bool acquired=solution.Observe(visible,radarCued,dt);
            CoastalCoverage(attention is Ship&&acquired);
            detector.detectedTargets.Clear();
            if(acquired) {
                if(Time.time>=nextReport){nextReport=Time.time+.5f;detector.DetectTarget(attention);}
                else detector.detectedTargets.Add(attention);
            }
            if(visible){cue=AimPoint(attention);lastSeen=lastVisualAt=Time.time;acousticCued=false;if(acquired)claimConfirmed=true;}
            if(attention&&claimConfirmed&&solution.BlindSeconds==0&&!visible)Lose();
        }
        void OpticalCue(float length)
        {
            if(!(attention is Aircraft)) {
                float angle=2.8f;Unit candidate=null;
                foreach(var u in nearby)if(u is Aircraft&&Enemy(u)&&Available(u)&&Vector3.Distance(origin.position,u.transform.position)<=length) {
                    float a=Vector3.Angle(origin.forward,u.transform.position-origin.position);
                    if(a<angle&&Clear(u)){candidate=u;angle=a;}
                }
                if(candidate&&Focus(candidate))cue=candidate.transform.position;
            }
        }
        void FindSupport()
        {
            SharedWorldQueries.Radars(owner.GlobalPosition(),300,support);
            for(int i=support.Count-1;i>=0;i--){var radar=support[i];var u=radar?radar.GetAttachedUnit():null;
                string key=u&&u.definition?u.definition.jsonKey:null;bool truck=key=="HLT-R"||key=="Truck2-R";
                if(!(u is GroundVehicle)||!SearchlightSolution.RadarInRange((u.transform.position-owner.transform.position).sqrMagnitude,u.NetworkHQ==owner.NetworkHQ,!u.Networkdisabled,truck)||!radar.IsOperational()||!radar.activated||radar.IsJammed())support.RemoveAt(i);
            }
        }
        void RadarCue()
        {
            radarCued=false;Unit contact=null;float best=float.MaxValue;
            foreach(var r in support) {
                var truck=r?r.GetAttachedUnit():null;
                if(!truck||truck.NetworkHQ!=owner.NetworkHQ||truck.Networkdisabled||(truck.transform.position-owner.transform.position).sqrMagnitude>90000||!r.IsOperational()||!r.activated||r.IsJammed())continue;
                foreach(var u in r.detectedTargets)if(u is Aircraft&&Enemy(u)&&Available(u)) {
                    float d=(u.transform.position-origin.position).sqrMagnitude;
                    if(d<=reach*reach&&(u==attention||d<best)){contact=u;best=d;if(u==attention)break;}
                }
                if(contact==attention&&attention)break;
            }
            if(!contact)return;
            if(HasVisualTrack&&contact!=attention)return;
            if(!Focus(contact))return;
            radarCued=true;acousticCued=false;
            // Coarse sector cue, never teleport the lamp or directly report the contact.
            if(!HasVisualTrack)cue=contact.transform.position;
        }
        internal static float EngineNoise(Aircraft aircraft)
        {
            var engines=aircraft?Engines.GetValue(aircraft) as List<IEngine>:null;float rpm=0;
            if(engines!=null)foreach(var engine in engines){
                if(engine==null)continue;var obj=engine as UnityEngine.Object;if(!ReferenceEquals(obj,null)&&!obj)continue;
                if(!engine.transform)continue;float value=engine.GetRPMRatio();if(value>rpm&&!float.IsInfinity(value))rpm=value;
            }
            return rpm;
        }
        void AcousticCue()
        {
            if(radarCued||HasVisualTrack||Time.time<nextAcousticAt)return;nextAcousticAt=Time.time+1;
            Unit heard=null;float best=float.MaxValue,radius=0;
            foreach(var u in nearby)if(u is Aircraft&&Enemy(u)&&Available(u)){
                float audible=SearchlightSolution.AcousticRange(EngineNoise((Aircraft)u));float distance=Vector3.Distance(origin.position,u.transform.position);
                if(audible<=0||distance>audible)continue;
                if(u==attention){heard=u;best=distance;radius=audible;break;}
                if(distance<best){heard=u;best=distance;radius=audible;}
            }
            if(!heard||(attention is Aircraft&&heard!=attention)||!Focus(heard))return;
            acousticCued=true;acousticWidth=SearchlightSolution.AcousticWidth(best,radius);
            int seed=GetInstanceID()^heard.GetInstanceID();float yawError=SearchlightSolution.AcousticBearingError(best,radius,seed,Time.time);
            var bearing=heard.transform.position-origin.position;
            float pitchError=Mathf.Sin(Time.time*.13f+phase)*4;
            float rangeError=1+.25f*Mathf.Sin(Time.time*.11f+phase);
            cue=origin.position+Quaternion.Euler(pitchError,yawError,0)*bearing*rangeError;
            // This only reserves a search sector. No evidence, detectedTargets,
            // illumination coverage or faction report is produced by hearing.
        }
        void Aim(float dt)
        {
            float desiredYaw,desiredPitch;
            if(attention) {
                if(HasVisualTrack)cue=AimPoint(attention);
                var v=transform.InverseTransformDirection(cue-origin.position);
                // A coarse ship cue must remain inside the narrow optical cone
                // once the lamp reaches it, rather than jittering out indefinitely.
                float uncertainty=attention is Ship?(claimConfirmed?.15f:.6f):HasVisualTrack?0:radarCued?.55f:acousticCued?acousticWidth:1.2f;
                desiredYaw=Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg+Mathf.Sin(Time.time*1.3f+phase)*uncertainty;
                desiredPitch=Mathf.Atan2(v.y,new Vector2(v.x,v.z).magnitude)*Mathf.Rad2Deg+Mathf.Cos(Time.time+phase)*uncertainty*.5f;
            } else {
                desiredYaw=Mathf.Repeat(Time.time*12+phase*57.3f,360);
                desiredPitch=25+Mathf.PingPong(Time.time*2+phase,45);
            }
            if(dt<=0)return;
            float smoothYaw=Mathf.SmoothDampAngle(yaw.localEulerAngles.y,desiredYaw,ref yawVelocity,.25f,20f,dt);
            float smoothPitch=Mathf.SmoothDampAngle(Mathf.DeltaAngle(0,pitch.localEulerAngles.x),-Mathf.Clamp(desiredPitch,attention is Ship?-85:5,85),ref pitchVelocity,.25f,15f,dt);
            yaw.localRotation=Quaternion.Euler(0,smoothYaw,0);pitch.localRotation=Quaternion.Euler(smoothPitch,0,0);
        }
        bool Inside(Unit u,float length){var v=AimPoint(u)-origin.position;return v.magnitude<=length&&Vector3.Angle(origin.forward,v)<=1.1f+Mathf.Atan2(8,v.magnitude)*Mathf.Rad2Deg;}
        bool Clear(Unit u)
        {
            return ClearPoint(AimPoint(u),u);
        }
        bool ClearPoint(Vector3 destination,Unit u)
        {
            if(ShellObscurants.SmokeBlocks(origin.position,destination))return false;
            var v=destination-origin.position;int count=Physics.RaycastNonAlloc(origin.position,v.normalized,hits,v.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(hits[i].collider&&!hits[i].collider.transform.IsChildOf(transform)&&!hits[i].collider.transform.IsChildOf(u.transform))return false;
            return true;
        }
        float Clip()
        {
            float length=reach;int count=Physics.RaycastNonAlloc(origin.position,origin.forward,hits,reach,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return .05f;
            for(int i=0;i<count;i++)if(hits[i].collider&&!hits[i].collider.attachedRigidbody&&!hits[i].collider.transform.IsChildOf(transform))length=Mathf.Min(length,Mathf.Max(.05f,hits[i].distance-.03f));
            return length;
        }
        void ApplyLength(float length){beam.GetPropertyBlock(properties);properties.SetFloat("_BeamRange",reach);properties.SetFloat("_VisibleLength",length);SearchlightClouds.Apply(properties,LevelInfo.i,origin);beam.SetPropertyBlock(properties);float r=.58f+beam.sharedMaterial.GetFloat("_Slope")*length;beam.localBounds=new Bounds(new Vector3(0,0,length*.5f),new Vector3(r*2,r*2,length));spot.range=length;}
        void OnDisable(){SetActive(false);Lose();}
        void OnDestroy(){SearchlightRuntime.Unregister(this);}
    }
}
