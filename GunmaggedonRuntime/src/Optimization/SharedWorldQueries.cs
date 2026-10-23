using System;
using System.Collections.Generic;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class SharedWorldQueries
    {
        struct Key:IEquatable<Key>
        {
            internal int X,Y,Z,R;
            internal FactionHQ Team;
            public bool Equals(Key k){return X==k.X&&Y==k.Y&&Z==k.Z&&R==k.R&&ReferenceEquals(Team,k.Team);}
            public override bool Equals(object o){return o is Key&&Equals((Key)o);}
            public override int GetHashCode(){unchecked{return (((X*397^Y)*397^Z)*397^R)*397+(ReferenceEquals(Team,null)?0:Team.GetInstanceID());}}
        }
        sealed class Snapshot {internal readonly List<Unit> Units=new List<Unit>();internal readonly List<Radar> Radars=new List<Radar>();internal readonly List<TrackingInfo> Contacts=new List<TrackingInfo>();internal bool RadarReady;}
        static readonly Dictionary<Key,Snapshot> snapshots=new Dictionary<Key,Snapshot>();static readonly Stack<Snapshot> free=new Stack<Snapshot>();
        static int frame=-1;static float step=-1;
        static Snapshot Get(GlobalPosition position,float radius,FactionHQ team=null,Transform observer=null)
        {
            if(frame!=Time.frameCount||step!=Time.fixedTime){foreach(var s in snapshots.Values){s.Units.Clear();s.Radars.Clear();s.Contacts.Clear();s.RadarReady=false;if(free.Count<128)free.Push(s);}snapshots.Clear();frame=Time.frameCount;step=Time.fixedTime;}
            var k=new Key{X=Mathf.FloorToInt(position.x/250),Y=Mathf.FloorToInt(position.y/250),Z=Mathf.FloorToInt(position.z/250),R=Mathf.CeilToInt(radius/250),Team=team};
            Snapshot result;if(snapshots.TryGetValue(k,out result))return result;
            result=free.Count>0?free.Pop():new Snapshot();
            var center=new GlobalPosition(k.X*250+125,k.Y*250+125,k.Z*250+125);
            OptimizationMetrics.Begin("GridQuery");try{if(team)team.GetTargetsWithinRange(result.Contacts,observer,k.R*250+433.013f,false);else BattlefieldGrid.GetUnitsInRangeNonAlloc(center,k.R*250+216.507f,result.Units);OptimizationMetrics.Count("GridQueries");}finally{OptimizationMetrics.End();}
            snapshots.Add(k,result);return result;
        }
        internal static void Units(GlobalPosition position,float radius,List<Unit> output)
        {
            output.Clear();var s=Get(position,radius);float squared=radius*radius;var local=position.ToLocalPosition();
            foreach(var u in s.Units)if(u&&(u.transform.position-local).sqrMagnitude<=squared)output.Add(u);
        }
        internal static void KnownContacts(FactionHQ team,Transform observer,float radius,List<TrackingInfo> output){output.Clear();if(team&&observer)output.AddRange(Get(observer.position.ToGlobalPosition(),radius,team,observer).Contacts);}
        internal static void Radars(GlobalPosition position,float radius,List<Radar> output)
        {
            output.Clear();var s=Get(position,radius);
            if(!s.RadarReady){foreach(var u in s.Units)if(u){scratch.Clear();u.GetComponentsInChildren<Radar>(false,scratch);foreach(var r in scratch)if(r)s.Radars.Add(r);}scratch.Clear();s.RadarReady=true;}
            // Snapshot only contains candidates: all evidence/state is rechecked by callers.
            float squared=radius*radius;foreach(var r in s.Radars)if(r){var u=r.GetAttachedUnit();if(u&&(u.transform.position-position.ToLocalPosition()).sqrMagnitude<=squared)output.Add(r);}
        }
        static readonly List<Radar> scratch=new List<Radar>();
        internal static void Clear(){snapshots.Clear();free.Clear();scratch.Clear();frame=-1;step=-1;}
    }
    internal static class SharedShipBounds
    {
        sealed class Entry {internal readonly List<Collider> Colliders=new List<Collider>();internal Bounds Bounds;internal GlobalPosition Center;internal float Step=float.NaN,Refresh=-1;}
        static readonly Dictionary<Unit,Entry> entries=new Dictionary<Unit,Entry>();static readonly List<Unit> expired=new List<Unit>();
        internal static Bounds Get(Unit unit)
        {
            Entry e;if(!entries.TryGetValue(unit,out e)){e=new Entry();entries.Add(unit,e);}
            if(Time.time>=e.Refresh){unit.GetComponentsInChildren<Collider>(true,e.Colliders);e.Refresh=Time.time+.25f;e.Step=float.NaN;}
            if(e.Step==Time.fixedTime){var rebased=e.Bounds;rebased.center=e.Center.ToLocalPosition();return rebased;}
            OptimizationMetrics.Begin("ShipBounds");try{
                bool found=false;var b=new Bounds(unit.transform.position,Vector3.zero);
                foreach(var c in e.Colliders)if(c&&!c.isTrigger&&c.enabled&&c.gameObject.activeInHierarchy){if(!found){b=c.bounds;found=true;}else b.Encapsulate(c.bounds);}
                if(!found){var d=unit.definition;b=new Bounds(unit.transform.position+Vector3.up*(d?d.height*.3f:2),d?new Vector3(d.width,Mathf.Max(2,d.height),d.length):new Vector3(5,4,20));}
                e.Bounds=b;e.Center=b.center.ToGlobalPosition();e.Step=Time.fixedTime;OptimizationMetrics.Count("BoundsBuilds");return b;
            }finally{OptimizationMetrics.End();}
        }
        internal static void Maintain(){expired.Clear();foreach(var p in entries)if(!p.Key)expired.Add(p.Key);foreach(var u in expired)entries.Remove(u);expired.Clear();}
        internal static void Clear(){entries.Clear();expired.Clear();}
    }
}
