using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class L25Tracking
    {
        static readonly FieldInfo Owner=AccessTools.Field(typeof(AimSolver),"attachedUnit"),Target=AccessTools.Field(typeof(AimSolver),"currentTarget"),From=AccessTools.Field(typeof(AimSolver),"firingTransform"),Info=AccessTools.Field(typeof(AimSolver),"weaponInfo");
        internal static void Install(Harmony h){h.Patch(AccessTools.Method(typeof(AimSolver),"GetAimVector"),prefix:new HarmonyMethod(typeof(L25Tracking),nameof(Aim)));}
        static bool Aim(AimSolver __instance,ref float __0,ref Vector3 __result)
        {
            var info=Info.GetValue(__instance) as WeaponInfo;var owner=Owner.GetValue(__instance) as Unit;
            if(!info||info.name!="L25_AA_Weapon"||!owner||!owner.LocalSim||!owner.definition||owner.definition.jsonKey!="Gunmaggedon_L25_AA")return true;
            var m=owner.GetComponentInChildren<L25TurretMotion>(true);var from=From.GetValue(__instance) as Transform;var target=Target.GetValue(__instance) as Unit;
            if(!m||!from)return true;float now=Time.time;
            if(m.Target!=target){m.Target=target;m.Visible=false;m.NextSight=0;m.Track.Reset(target?target.GetInstanceID():0,now,owner.GetInstanceID());}
            Vector3 relative=target?target.transform.position-from.position:Vector3.zero;float range=relative.magnitude;
            if(!(target is Aircraft)||range>3500||range<1){m.Visible=false;__result=from.forward;__0=0;return false;}
            if(now>=m.NextSight){m.NextSight=now+(float)L25TrackingSolution.SampleInterval(range);m.Visible=target.LineOfSight(from.position,10f);if(m.Visible){m.Track.Sample(new OpticalVector(relative.x,relative.y,relative.z),now);m.LastSeen=now;}}
            if(!m.Visible||!m.Track.Ready(now)){__result=from.forward;__0=0;return false;}
            float flight=AAOpticalRanging.FlightSeconds(range,info.muzzleVelocity,info.dragCoef);var estimate=m.Track.Predict(flight,now);
            var aim=new Vector3((float)estimate.X,(float)estimate.Y,(float)estimate.Z)+Vector3.up*(.5f*9.81f*info.gravMult*flight*flight);
            __result=aim.normalized;__0=range;return false;
        }
    }
}
