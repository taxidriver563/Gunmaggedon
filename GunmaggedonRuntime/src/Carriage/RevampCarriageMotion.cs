using UnityEngine;

namespace GunmageddonAudio
{
    // Attached by the eventual unit adapter, or by the editor-only rig preview.
    // Kept out of the exported prefab: only native Unity physics is serialized.
    public sealed class RevampCarriageMotion : MonoBehaviour
    {
        sealed class Corner
        {
            internal WheelCollider Wheel;internal Transform Spin,Upper,Lower,Upright;
            internal Vector3 UpperRest,LowerRest,UprightRest;internal float Height,Side,Arm;
        }
        readonly Corner[] corners=new Corner[4];Rigidbody body;Transform sides,front,rear,hook;Collider[] feet;
        Vector3 frontRest,rearRest;ModelDetail detail;float stow;bool initialized;
        public bool Towed {get;private set;}
        public Transform TowTarget {get;private set;}
        public float StowFraction {get{return stow;}}
        public bool Deployed {get{return !Towed&&stow<=.001f;}}
        public bool ReadyToTow {get{return Towed&&stow>=.999f;}}
        public const float PistonRetraction=.85f;
        // PhysX treats an exactly-zero-torque wheel as an idle/sticky tire.
        // A 0.00001 Nm wake value permits an unpowered trailer to roll.
        const float FreeRollingWakeTorque=.00001f;
        public void Initialize()
        {
            if(initialized)return;body=GetComponent<Rigidbody>();sides=transform.Find("StationarySupports");
            front=transform.Find("FrontPiston");rear=transform.Find("RearPiston");
            if(!body||!sides||!front||!rear)throw new System.InvalidOperationException("Incomplete revamp carriage rig");
            var frontReference=transform.Find("RigReferences/FrontPistonRest");var rearReference=transform.Find("RigReferences/RearPistonRest");
            frontRest=frontReference?frontReference.localPosition:front.localPosition;rearRest=rearReference?rearReference.localPosition:rear.localPosition;hook=transform.Find("TowHook");
            var contacts=new System.Collections.Generic.List<Collider>();contacts.AddRange(front.GetComponentsInChildren<Collider>(true));contacts.AddRange(rear.GetComponentsInChildren<Collider>(true));contacts.AddRange(sides.GetComponentsInChildren<Collider>(true));feet=contacts.ToArray();
            var ids=new[]{"FL","FR","BL","BR"};
            for(int i=0;i<4;i++){
                var rig=transform.Find("Suspension/"+ids[i]);var wheel=rig.Find("WheelContact").GetComponent<WheelCollider>();
                var c=new Corner{Wheel=wheel,Spin=rig.Find("WheelSpin"),Upper=rig.Find("UpperArm"),Lower=rig.Find("LowerArm"),Upright=rig.Find("Upright")};
                c.UpperRest=c.Upper.localPosition;c.LowerRest=c.Lower.localPosition;c.UprightRest=c.Upright.localPosition;
                c.Height=c.Spin.localPosition.y;c.Side=Mathf.Sign(c.Upright.localPosition.x);c.Arm=Mathf.Abs(c.Upright.localPosition.x-c.UpperRest.x);corners[i]=c;
            }
            detail=new ModelDetail(this);initialized=true;Advance(0);ApplyVisuals();
        }
        void Awake(){Initialize();}
        public void SetTowed(bool value,Transform target=null){if(!initialized)Initialize();Towed=value;TowTarget=value?target:null;body.WakeUp();Advance(0);}
        // Explicit time step permits isolated editor physics validation as well.
        public void Advance(float seconds)
        {
            if(!initialized)return;stow=Mathf.MoveTowards(stow,Towed?1:0,Mathf.Max(0,seconds)/1.25f);
            sides.gameObject.SetActive(!Towed);
            float steering=0;
            if(Towed){var direction=TowTarget?TowTarget.position-hook.position:body.velocity;
                var local=transform.InverseTransformDirection(direction);if(local.sqrMagnitude>.04f)steering=Mathf.Clamp(Mathf.Atan2(local.x,Mathf.Max(.5f,Mathf.Abs(local.z)))*Mathf.Rad2Deg,-28,28);}
            for(int i=0;i<corners.Length;i++){var wheel=corners[i].Wheel;wheel.brakeTorque=Towed?0:20000;wheel.motorTorque=Towed?FreeRollingWakeTorque:0;wheel.steerAngle=i<2?steering:0;}
            // Support contact is gameplay/physics, independent of render detail.
            var delta=Vector3.up*(stow*PistonRetraction);front.localPosition=frontRest+delta;rear.localPosition=rearRest+delta;
            foreach(var c in feet)if(c)c.enabled=Deployed;
        }
        void FixedUpdate(){Advance(Time.fixedDeltaTime);}
        void LateUpdate(){ApplyVisuals();}
        public void ApplyVisuals()
        {
            if(!initialized||detail!=null&&!detail.Animate)return;
            foreach(var c in corners){Vector3 p;Quaternion q;c.Wheel.GetWorldPose(out p,out q);
                float dy=Mathf.Clamp(c.Spin.parent.InverseTransformPoint(p).y-c.Height,-.25f,.25f);
                c.Spin.SetPositionAndRotation(p,q);
                float angle=Mathf.Atan2(dy,Mathf.Max(.01f,c.Arm))*Mathf.Rad2Deg*c.Side;
                c.Upper.localRotation=c.Lower.localRotation=Quaternion.AngleAxis(angle,Vector3.forward);
                c.Upright.localPosition=c.UprightRest+Vector3.up*dy;
            }
        }
    }
}
