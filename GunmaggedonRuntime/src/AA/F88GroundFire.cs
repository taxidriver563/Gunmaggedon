using UnityEngine;
namespace GunmageddonAudio
{
    public sealed partial class F88FlakController
    {
        internal bool APQueued{get;private set;}
        internal void CueRound(){APQueued=GroundMode;}
        readonly FlakGroundTrack groundTrack=new FlakGroundTrack();
        internal static bool GroundKind(Unit unit){return unit&&(unit is GroundVehicle||unit is Building);}
        internal bool GroundEnabled{get{return F88FlakRuntime.Matches(gun)&&!IncendiaryCanisterRuntime.Matches(gun);}}
        internal bool GroundMode{get{return GroundEnabled&&GroundKind(target);}}
        bool GroundVisible(Unit unit)
        {
            if(!GroundEnabled||!GroundKind(unit)||!FlakGroundPolicy.InRange((unit.transform.position-muzzle.position).sqrMagnitude,Advanced))return false;
            if(CoastalIllumination.IsLowLight(LevelInfo.i,muzzle.position,.15f)&&!CoastalIllumination.IsIlluminated(unit))return false;
            return !ShellObscurants.SmokeBlocks(muzzle.position,unit.transform.position)&&unit.LineOfSight(muzzle.position,10f);
        }
        bool GroundReady{get{return planned&&aimSamples>=3&&Operational&&owner.IsServer&&Eligible(target)&&groundTrack.Fresh(Time.time)&&GroundVisible(target);}}
        void ObserveGround(float now)
        {
            if(!Eligible(target)||!GroundVisible(target))return;
            float height=target.definition?Mathf.Max(1,target.definition.height*.45f):1.3f;
            groundTrack.Observe(V((target.transform.position+Vector3.up*height).ToGlobalPosition()),now);
            nextObservation=now+(Advanced?.25f:.4f);
        }
        void StepGround(float now)
        {
            if(!owner.IsServer||!Eligible(target)||!groundTrack.Fresh(now)||!GroundVisible(target)){planned=false;return;}
            if(!planned){
                if((float)Reload.GetValue(gun)>(Advanced?1.8f:1.55f))return;
                planned=true;aimSamples=0;nextAim=0;
                flightEstimate=Vector3.Distance(muzzle.position,V(groundTrack.Position))/gun.info.muzzleVelocity;
            }
            if(now>=nextAim){
                nextAim=now+.2f;var destination=V(groundTrack.Predict(now,flightEstimate));var relative=destination-muzzle.position;
                OpticalVector vector;double seconds;
                if(!FlakBarrageSolution.Solve(new OpticalVector(relative.x,relative.y,relative.z),gun.info.muzzleVelocity,gun.info.dragCoef,9.81*gun.info.gravMult,Time.fixedDeltaTime,out vector,out seconds,FlakGroundPolicy.Lifetime)){planned=false;return;}
                direction=new Vector3((float)vector.X,(float)vector.Y,(float)vector.Z);point=destination.ToGlobalPosition();flightEstimate=seconds;aimSamples++;
                fuse=(float)FlakGroundPolicy.Lifetime;
            }
            var local=transform.parent.InverseTransformDirection(direction);float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float elevation=Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            if(elevation< -5||elevation>85){planned=false;return;}
            transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,yaw,0),(Advanced?20:16)*Time.fixedDeltaTime);
            pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(-elevation,0,0),(Advanced?16:12)*Time.fixedDeltaTime);
            if(now>=nextPose){nextPose=now+.2f;F88FlakNetwork.Pose(owner,transform.localEulerAngles.y,pitch.localEulerAngles.x);}
            if(!ReadyToFire||Vector3.Angle(muzzle.forward,direction)>FlakGroundPolicy.Spread(Advanced))return;
            var ready=ReadyStation();if(ready!=null&&ready.Ammo>0)gun.Fire(owner,null,Vector3.zero,ready,point);
        }
    }
}
