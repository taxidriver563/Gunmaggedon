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
        static Harmony harmony;
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
        }
        static readonly Dictionary<Gun,CoastalRequest> CoastalRequests=new Dictionary<Gun,CoastalRequest>();
        static readonly List<Gun> ExpiredRequests=new List<Gun>();
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
        {CoastalRequest r;if(gun&&CoastalRequests.TryGetValue(gun,out r)&&gun.GetTarget()!=r.Target)CoastalRequests.Remove(gun);}
        static void PruneRequests()
        {
            ExpiredRequests.Clear();foreach(var pair in CoastalRequests)if(!pair.Value.Valid(Time.time))ExpiredRequests.Add(pair.Key);
            foreach(var gun in ExpiredRequests)CoastalRequests.Remove(gun);ExpiredRequests.Clear();
        }
        internal static CoastalRequest CoastalFor(FactionHQ team,Unit target)
        {
            CoastalRequest newest=null;
            foreach(var pair in CoastalRequests) {
                var r=pair.Value;
                if(r.Team==team&&r.Target==target&&r.Valid(Time.time)&&(newest==null||r.Updated>newest.Updated))newest=r;
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
            foreach(var c in Controllers)if(c&&c.AcquiringCoastal(r.Team,target))return true;
            return false;
        }
        internal static bool IlluminatesCoastal(Unit target)
        {foreach(var c in Controllers)if(c&&c.IlluminatesCoastal(target))return true;return false;}
        internal static bool CoversCoastal(FactionHQ team,Unit target,Vector3 predicted,float seconds)
        {foreach(var c in Controllers)if(c&&c.CoversCoastal(team,target,predicted,seconds))return true;return false;}
        internal static bool Matches(Unit unit) {return unit&&unit.definition&&unit.definition.jsonKey==Key;}
        internal static void Install(ManualLogSource logger)
        {
            harmony=new Harmony("gunmageddon.searchlight");
            try {
                harmony.Patch(AccessTools.Method(typeof(TargetDetector),"TargetDetector_OnInitialize"),postfix:new HarmonyMethod(typeof(SearchlightRuntime),nameof(Initialize)));
                harmony.Patch(AccessTools.Method(typeof(TargetDetector),"VisualCheck"),prefix:new HarmonyMethod(typeof(SearchlightRuntime),nameof(VisualCheck)));
                logger.LogInfo("Searchlight night sweep, 300m radar cues and shared coastal illumination requests enabled.");
            }catch(Exception e){harmony.UnpatchSelf();logger.LogError("Searchlight initialization failed: "+e);}
        }
        static bool VisualCheck(TargetDetector __instance){return !Matches(__instance.GetAttachedUnit());}
        static void Initialize(TargetDetector __instance)
        {
            var owner=__instance.GetAttachedUnit();if(!Matches(owner)||owner.GetComponent<SearchlightController>())return;
            var controller=owner.gameObject.AddComponent<SearchlightController>();Controllers.Add(controller);
        }
        internal static void Unregister(SearchlightController c){Claims.Release(c);Controllers.Remove(c);}
        internal static void Shutdown(){harmony?.UnpatchSelf();harmony=null;foreach(var c in Controllers.ToArray())if(c)UnityEngine.Object.Destroy(c);Controllers.Clear();Claims.Clear();CoastalRequests.Clear();ExpiredRequests.Clear();}
    }

    public sealed class SearchlightController : MonoBehaviour
    {
        GroundVehicle owner; TargetDetector detector; Transform yaw,pitch,origin;MeshRenderer beam,lens;Light spot;
        List<Unit> nearby,locals;
        List<Radar> support;
        RaycastHit[] hits;
        MaterialPropertyBlock properties;
        SearchlightSolution solution;
        Unit attention;float nextCandidates,nextSupport,nextSample,lastSample,nextReport,phase,lastCueAt;
        Vector3 cue;bool active,radarCued;float reach;
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
            if(attention!=target){attention=target;solution.Reset();claimStarted=lastSeen=Time.time;claimConfirmed=false;}
            return true;
        }
        void Awake()
        {
            solution=new SearchlightSolution();nearby=new List<Unit>();locals=new List<Unit>();support=new List<Radar>();hits=new RaycastHit[64];properties=new MaterialPropertyBlock();
            owner=GetComponent<GroundVehicle>();detector=GetComponentInChildren<TargetDetector>(true);
            foreach(var t in GetComponentsInChildren<Transform>(true)) {
                if(t.name=="SearchlightYaw")yaw=t;else if(t.name=="LampPitch")pitch=t;else if(t.name=="BeamOrigin")origin=t;
                else if(t.name=="SearchlightBeam")beam=t.GetComponent<MeshRenderer>();else if(t.name=="LensGlow")lens=t.GetComponent<MeshRenderer>();
            }
            if(origin)spot=origin.GetComponentInChildren<Light>(true);
            if(!owner||!detector||!yaw||!pitch||!origin||!beam||!lens||!spot){enabled=false;return;}
            reach=Mathf.Clamp(beam.sharedMaterial.GetFloat("_BeamRange"),0,13000);
            phase=(GetInstanceID()&1023)*.17f;lastSample=Time.time;
            SetActive(false);
        }
        void SetActive(bool value){active=value;if(beam)beam.enabled=value;if(lens)lens.enabled=value;if(spot)spot.enabled=value;}
        bool Enemy(Unit u){return u&&(u is Aircraft||(u is Ship&&SearchlightRuntime.CoastalFor(owner.NetworkHQ,u)!=null))&&!u.Networkdisabled&&u.gameObject.activeSelf&&owner.NetworkHQ&&u.NetworkHQ&&owner.NetworkHQ!=u.NetworkHQ;}
        static Vector3 AimPoint(Unit u){return u.transform.position+(u is Ship?Vector3.up*8f:Vector3.zero);}
        void CoastalCoverage(bool value){if(coastalVisible!=value)CoastalIllumination.CoverageChanged();coastalVisible=value;coastalSampleAt=Time.time;}
        void Lose(){SearchlightRuntime.Claims.Release(this);CoastalCoverage(false);attention=null;radarCued=false;claimConfirmed=false;solution?.Reset();if(detector&&detector.detectedTargets!=null)detector.detectedTargets.Clear();}
        bool Operational {get{return enabled&&active&&owner&&owner.LocalSim&&!owner.Networkdisabled&&owner.gameObject.activeInHierarchy&&detector&&detector.IsOperational()&&spot&&spot.enabled&&(!owner.rb||owner.rb.velocity.sqrMagnitude<4f);}}
        internal bool CanSupport(SearchlightRuntime.CoastalRequest request,out float distance)
        {
            distance=float.MaxValue;
            if(!Operational||request.Team!=owner.NetworkHQ||!Available(request.Target))return false;
            Vector3 estimated=request.Estimate(Time.time);distance=(estimated-origin.position).sqrMagnitude;
            return CanReach(estimated)&&ClearPoint(estimated,request.Target);
        }
        internal bool AcquiringCoastal(FactionHQ team,Unit target)
        {return Operational&&owner.NetworkHQ==team&&attention==target&&target is Ship&&!claimConfirmed&&Time.time-claimStarted<12f&&Enemy(target);}
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
            return v.sqrMagnitude<=reach*reach*rangeFactor*rangeFactor&&elevation>=-5f&&elevation<=85f;
        }
        void CoastalCue()
        {
            if(attention is Ship) {
                var r=SearchlightRuntime.CoastalFor(owner.NetworkHQ,attention);
                if(r!=null&&!coastalVisible)cue=r.Estimate(Time.time);
                return;
            }
            if(attention)return;
            SearchlightRuntime.CoastalRequest request;
            if(SearchlightRuntime.TryCoastalCue(this,out request)&&Focus(request.Target)){cue=request.Estimate(Time.time);radarCued=false;}
        }
        internal void OfferCoastal(){if(Operational&&!attention)CoastalCue();}
        void Update()
        {
            if(!owner||!detector||!origin)return;
            bool dark=CoastalIllumination.IsLowLight(LevelInfo.i,origin.position,.15f);
            bool on=dark&&!owner.Networkdisabled&&detector.IsOperational()&&(!owner.rb||owner.rb.velocity.sqrMagnitude<4f);
            if(!on){if(active){SetActive(false);Lose();}return;}
            if(!active){SetActive(true);lastSample=Time.time;}
            // Target evidence and faction reports are authoritative. Remote presentation
            // currently uses a local sweep, without publishing extra contacts.
            if(owner.LocalSim&&Time.time>=nextCandidates){nextCandidates=Time.time+.5f;nearby.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(owner.GlobalPosition(),reach,nearby);}
            if(owner.LocalSim&&Time.time>=nextSupport){nextSupport=Time.time+1;FindSupport();}
            if(owner.LocalSim&&attention&&(!Enemy(attention)||Vector3.Distance(origin.position,attention.transform.position)>reach))Lose();
            if(owner.LocalSim&&attention) {
                bool timeout=!claimConfirmed&&Time.time-claimStarted>=12f;
                bool lost=claimConfirmed&&Time.time-lastSeen>.6f;
                if(timeout||lost){retryTarget=attention;retryAfter=Time.time+(timeout?5f:2f);}
                if(timeout||lost||!SearchlightRuntime.Claims.Take(owner.NetworkHQ,attention,this,Time.time,ValidClaim))Lose();
            }
            if(owner.LocalSim&&Time.time>=lastCueAt+.5f){lastCueAt=Time.time;RadarCue();CoastalCue();}
            Aim(Time.deltaTime);
            if(Time.time<nextSample)return;nextSample=Time.time+.1f;
            float dt=Mathf.Min(.25f,Time.time-lastSample);lastSample=Time.time;
            float length=Clip();ApplyLength(length);
            if(!owner.LocalSim)return;
            if(!attention) {
                float angle=2.8f;Unit candidate=null;
                foreach(var u in nearby)if(u is Aircraft&&Enemy(u)&&Available(u)&&Vector3.Distance(origin.position,u.transform.position)<=length) {
                    float a=Vector3.Angle(origin.forward,u.transform.position-origin.position);
                    if(a<angle&&Clear(u)){candidate=u;angle=a;}
                }
                if(candidate&&Focus(candidate))cue=candidate.transform.position;
            }
            bool visible=attention&&Inside(attention,length)&&Clear(attention);
            bool acquired=solution.Observe(visible,radarCued,dt);
            CoastalCoverage(attention is Ship&&acquired);
            detector.detectedTargets.Clear();
            if(acquired) {
                if(Time.time>=nextReport){nextReport=Time.time+.5f;detector.DetectTarget(attention);}
                else detector.detectedTargets.Add(attention);
            }
            if(visible){cue=AimPoint(attention);lastSeen=Time.time;if(acquired)claimConfirmed=true;}
            if(attention&&solution.BlindSeconds==0&&!visible)Lose();
        }
        void FindSupport()
        {
            support.Clear();locals.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(owner.GlobalPosition(),300,locals);
            foreach(var u in locals) {
                if(!(u is GroundVehicle)||!u.definition)continue;
                string key=u.definition.jsonKey;bool truck=key=="HLT-R"||key=="Truck2-R";
                if(!SearchlightSolution.RadarInRange((u.transform.position-owner.transform.position).sqrMagnitude,u.NetworkHQ==owner.NetworkHQ,!u.Networkdisabled,truck))continue;
                foreach(var radar in u.GetComponentsInChildren<Radar>())if(radar.IsOperational()&&radar.activated&&!radar.IsJammed())support.Add(radar);
            }
        }
        void RadarCue()
        {
            if(attention is Ship)return;
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
            if(!Focus(contact))return;
            radarCued=true;
            // Coarse sector cue, never teleport the lamp or directly report the contact.
            cue=contact.transform.position;
        }
        void Aim(float dt)
        {
            float desiredYaw,desiredPitch;
            if(attention) {
                var v=transform.InverseTransformDirection(cue-origin.position);
                float uncertainty=attention is Ship&&claimConfirmed?.15f:radarCued?.55f:1.2f;
                desiredYaw=Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg+Mathf.Sin(Time.time*1.3f+phase)*uncertainty;
                desiredPitch=Mathf.Atan2(v.y,new Vector2(v.x,v.z).magnitude)*Mathf.Rad2Deg+Mathf.Cos(Time.time+phase)*uncertainty*.5f;
            } else {
                desiredYaw=Mathf.Repeat(Time.time*12+phase*57.3f,360);
                desiredPitch=25+Mathf.PingPong(Time.time*2+phase,45);
            }
            yaw.localRotation=Quaternion.RotateTowards(yaw.localRotation,Quaternion.Euler(0,desiredYaw,0),20*dt);
            pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(-Mathf.Clamp(desiredPitch,attention is Ship?-5:5,85),0,0),15*dt);
        }
        bool Inside(Unit u,float length){var v=AimPoint(u)-origin.position;return v.magnitude<=length&&Vector3.Angle(origin.forward,v)<=1.1f+Mathf.Atan2(8,v.magnitude)*Mathf.Rad2Deg;}
        bool Clear(Unit u)
        {
            return ClearPoint(AimPoint(u),u);
        }
        bool ClearPoint(Vector3 destination,Unit u)
        {
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
        void ApplyLength(float length){beam.GetPropertyBlock(properties);properties.SetFloat("_VisibleLength",length);SearchlightClouds.Apply(properties,LevelInfo.i,origin);beam.SetPropertyBlock(properties);float r=.58f+beam.sharedMaterial.GetFloat("_Slope")*length;beam.localBounds=new Bounds(new Vector3(0,0,length*.5f),new Vector3(r*2,r*2,length));spot.range=length;}
        void OnDisable(){SetActive(false);Lose();}
        void OnDestroy(){SearchlightRuntime.Unregister(this);}
    }
}
