using System;
using System.Collections.Generic;

namespace GunmageddonAudio
{
    // One lease per faction/target (aircraft or requested ship). The caller supplies a simulation clock and
    // liveness check, so disabled or destroyed lamps cannot strand a token.
    internal sealed class SearchlightClaims<TTeam,TTarget,TOwner>
        where TTeam:class where TTarget:class where TOwner:class
    {
        struct Key:IEquatable<Key>
        {
            internal TTeam Team;internal TTarget Target;
            public bool Equals(Key other){return EqualityComparer<TTeam>.Default.Equals(Team,other.Team)&&EqualityComparer<TTarget>.Default.Equals(Target,other.Target);}
            public override bool Equals(object value){return value is Key&&Equals((Key)value);}
            public override int GetHashCode(){return EqualityComparer<TTeam>.Default.GetHashCode(Team)*397^EqualityComparer<TTarget>.Default.GetHashCode(Target);}
        }
        sealed class Lease { internal TOwner Owner;internal float Expires; }
        readonly Dictionary<Key,Lease> leases=new Dictionary<Key,Lease>();
        readonly List<Key> remove=new List<Key>();
        internal bool Available(TTeam team,TTarget target,TOwner owner,float now,Func<TOwner,TTeam,TTarget,bool> valid)
        {
            if(team==null||target==null||owner==null)return false;
            var key=new Key{Team=team,Target=target};Lease lease;
            if(!leases.TryGetValue(key,out lease))return true;
            if(now>=lease.Expires||!valid(lease.Owner,team,target)){leases.Remove(key);return true;}
            return EqualityComparer<TOwner>.Default.Equals(lease.Owner,owner);
        }
        internal bool Take(TTeam team,TTarget target,TOwner owner,float now,Func<TOwner,TTeam,TTarget,bool> valid)
        {
            if(!Available(team,target,owner,now,valid))return false;
            var key=new Key{Team=team,Target=target};Lease lease;
            if(!leases.TryGetValue(key,out lease)){lease=new Lease();leases.Add(key,lease);}
            lease.Owner=owner;lease.Expires=now+1.25f;return true;
        }
        internal void Release(TOwner owner)
        {
            remove.Clear();foreach(var pair in leases)if(EqualityComparer<TOwner>.Default.Equals(pair.Value.Owner,owner))remove.Add(pair.Key);
            foreach(var key in remove)leases.Remove(key);remove.Clear();
        }
        internal void Clear(){leases.Clear();remove.Clear();}
    }
}
