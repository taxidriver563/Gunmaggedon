using System;
using System.Collections.Generic;
using UnityEngine;
namespace GunmageddonAudio
{
    // One instantaneous fragment pass per authoritative airburst. Smoke never
    // ticks damage. Per-call pooled scratch also permits nested damage callbacks.
    internal sealed class F88Fragments
    {
        struct Candidate {internal UnitPart Part;internal Collider Collider;internal float Distance;}
        struct Budget {internal double Remaining;internal int Parts;}
        static readonly Stack<F88Fragments> Pool=new Stack<F88Fragments>();
        Collider[] buffer=new Collider[64];
        readonly Dictionary<UnitPart,Candidate> nearest=new Dictionary<UnitPart,Candidate>();
        readonly List<Candidate> candidates=new List<Candidate>();
        readonly Dictionary<Aircraft,Budget> budgets=new Dictionary<Aircraft,Budget>();
        internal static void Burst(Vector3 position,PersistentID source){Run(position,source,false);}
        internal static void HeavyBurst(Vector3 position,PersistentID source){Run(position,source,true);}
        static void Run(Vector3 position,PersistentID source,bool heavy)
        {
            var q=Pool.Count==0?new F88Fragments():Pool.Pop();
            try{q.Apply(position,source,heavy);}finally{Array.Clear(q.buffer,0,q.buffer.Length);q.nearest.Clear();q.candidates.Clear();q.budgets.Clear();Pool.Push(q);}
        }
        void Apply(Vector3 position,PersistentID source,bool heavy)
        {
            int count;float radius=(float)(heavy?FlakFragmentationPolicy.HeavyRadius:FlakFragmentationPolicy.Radius);
            while(true){count=Physics.OverlapSphereNonAlloc(position,radius,buffer,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);if(count<buffer.Length||buffer.Length>=1024)break;Array.Resize(ref buffer,buffer.Length*2);}
            // Saturation is ambiguous; preserve targets rather than silently
            // dropping aircraft in unusually dense formations.
            var hits=count==buffer.Length?Physics.OverlapSphere(position,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore):buffer;
            if(!ReferenceEquals(hits,buffer))count=hits.Length;
            for(int i=0;i<count;i++){
                var collider=hits[i];if(!collider)continue;var part=collider.GetComponentInParent<UnitPart>();
                var aircraft=part?part.parentUnit as Aircraft:null;
                if(!aircraft||aircraft.Networkdisabled||!aircraft.LocalSim||!aircraft.IsServer)continue;
                float distance=Vector3.Distance(position,collider.ClosestPoint(position));if(distance>=radius)continue;
                Candidate old;if(nearest.TryGetValue(part,out old)&&old.Distance<=distance)continue;
                nearest[part]=new Candidate{Part=part,Collider=collider,Distance=distance};
            }
            candidates.AddRange(nearest.Values);candidates.Sort((a,b)=>{int d=a.Distance.CompareTo(b.Distance);return d!=0?d:a.Part.GetInstanceID().CompareTo(b.Part.GetInstanceID());});
            foreach(var candidate in candidates){
                var part=candidate.Part;var collider=candidate.Collider;if(!part||!collider)continue;var aircraft=part.parentUnit as Aircraft;if(!aircraft||aircraft.Networkdisabled)continue;
                Budget budget;if(!budgets.TryGetValue(aircraft,out budget))budget=new Budget{Remaining=heavy?FlakFragmentationPolicy.HeavyAircraftBudget*FlakFragmentationPolicy.HeavyFalloff(candidate.Distance):FlakFragmentationPolicy.AircraftBudget*FlakFragmentationPolicy.Falloff(candidate.Distance)};
                if(budget.Parts>=(heavy?FlakFragmentationPolicy.HeavyMaximumParts:FlakFragmentationPolicy.MaximumParts)||budget.Remaining<=0)continue;
                var armor=((IDamageable)part).GetArmorProperties();if(armor==null)continue;
                double hp=heavy?FlakFragmentationPolicy.HeavyHitPoints(candidate.Distance,armor.pierceArmor,armor.pierceTolerance,budget.Remaining):FlakFragmentationPolicy.HitPoints(candidate.Distance,armor.pierceArmor,armor.pierceTolerance,budget.Remaining);if(hp<=0)continue;
                var nearestPoint=collider.ClosestPoint(position);RaycastHit obstruction;
                if(Physics.Linecast(position,nearestPoint,out obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&obstruction.collider.GetComponentInParent<UnitPart>()!=part)continue;
                // Reserve before the native callback, which can destroy parts or
                // trigger another explosion. Armor/attribution/RPCs remain native.
                budget.Remaining-=hp;budget.Parts++;budgets[aircraft]=budget;
                float pierce=Mathf.Min((float)(heavy?FlakFragmentationPolicy.HeavyPenetrationLimit:FlakFragmentationPolicy.PenetrationLimit),Mathf.Max(0,armor.pierceArmor)+(float)hp*Mathf.Max(.01f,armor.pierceTolerance));
                ((IDamageable)part).TakeDamage(pierce,0,1,0,0,source);
            }
        }
        internal static void Clear(){Pool.Clear();}
    }
}
