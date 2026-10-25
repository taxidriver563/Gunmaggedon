using UnityEngine;
namespace GunmageddonAudio
{
    public sealed class ClusterImpactVisual:MonoBehaviour
    {
        internal ParticleSystem Particles;internal float Retire;internal long Lease;
    }
    internal static class ClusterVisualPools
    {
        static readonly RuntimePool<ClusterCarrierBurst> openings=new RuntimePool<ClusterCarrierBurst>(128,CreateOpening,o=>o.ResetEffect());
        static readonly RuntimePool<ClusterImpactVisual> water=new RuntimePool<ClusterImpactVisual>(128,()=>CreateImpact(true),ResetImpact);
        static readonly RuntimePool<ClusterImpactVisual> land=new RuntimePool<ClusterImpactVisual>(128,()=>CreateImpact(false),ResetImpact);
        internal static readonly RuntimePool<LineRenderer> Heads=new RuntimePool<LineRenderer>(512,CreateHead,l=>{l.enabled=false;l.positionCount=0;l.SetPropertyBlock(null);l.sharedMaterial=null;l.transform.SetParent(Datum.origin,false);});
        internal static readonly RuntimePool<SpecialPayloadFlight> Flights=new RuntimePool<SpecialPayloadFlight>(16,CreateFlight,f=>f.ResetFlight());
        static ClusterCarrierBurst CreateOpening(){var g=new GameObject("Cluster carrier opening burst");g.SetActive(false);return g.AddComponent<ClusterCarrierBurst>();}
        static SpecialPayloadFlight CreateFlight(){var g=new GameObject("Coastal payload flight");g.SetActive(false);return g.AddComponent<SpecialPayloadFlight>();}
        static LineRenderer CreateHead(){var g=new GameObject("Clusterlet head");g.SetActive(false);return g.AddComponent<LineRenderer>();}
        internal static void Opening(GlobalPosition p){OptimizationRuntime.EnsureMission();var lease=openings.Rent();var o=lease.Value;o.Lease=lease.Id;RuntimePool<ClusterCarrierBurst>.Place(o,Datum.origin,p.ToLocalPosition());o.Initialize(p);o.gameObject.SetActive(true);}
        static ClusterImpactVisual CreateImpact(bool sea)
        {
            var g=new GameObject(sea?"Cluster bomblet water splash":"Cluster bomblet impact");g.SetActive(false);var v=g.AddComponent<ClusterImpactVisual>();var ps=g.AddComponent<ParticleSystem>();v.Particles=ps;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.simulationSpace=ParticleSystemSimulationSpace.Custom;
            main.startLifetime=sea?1.2f:.45f;main.startSpeed=sea?10:12;main.startSize=sea?1.2f:.5f;main.startColor=sea?new Color(.75f,.85f,.9f,.9f):new Color(1,.6f,.15f);main.gravityModifier=sea?1:.6f;
            var emission=ps.emission;emission.rateOverTime=0;if(sea){var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=25;shape.radius=.5f;}return v;
        }
        static void ResetImpact(ClusterImpactVisual v){v.Particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);v.Retire=0;}
        internal static void Impact(Vector3 position,bool sea)
        {
            OptimizationRuntime.EnsureMission();var lease=(sea?water:land).Rent();var v=lease.Value;v.Lease=lease.Id;v.transform.SetParent(Datum.origin,false);v.transform.position=position;v.transform.rotation=sea?Quaternion.LookRotation(Vector3.up):Quaternion.identity;
            var main=v.Particles.main;main.customSimulationSpace=Datum.origin;v.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=sea?SpecialShells.SmokeMaterial:SpecialShells.SparkMaterial;
            v.gameObject.SetActive(true);v.Particles.Play();v.Particles.Emit(sea?24:14);v.Retire=Time.time+(sea?2:1.5f);
        }
        internal static void Tick()
        {
            for(int i=openings.Count-1;i>=0;i--){var l=openings.At(i);if(!l.Value||l.Value.Advance(Time.time))openings.Release(l.Value,l.Id);}
            TickImpacts(water);TickImpacts(land);
        }
        static void TickImpacts(RuntimePool<ClusterImpactVisual> p){for(int i=p.Count-1;i>=0;i--){var l=p.At(i);if(!l.Value||Time.time>=l.Value.Retire)p.Release(l.Value,l.Id);}}
        internal static void Clear(){Flights.Clear();Heads.Clear();openings.Clear();water.Clear();land.Clear();}
    }
}
