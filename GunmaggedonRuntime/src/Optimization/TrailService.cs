using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GunmageddonAudio
{
    internal static class TrailService
    {
        static readonly List<SpottingTrail> active=new List<SpottingTrail>();static bool subscribed;
        static readonly RuntimePool<SpottingTrail> pool=new RuntimePool<SpottingTrail>(256,Create,t=>t.ResetTrail());
        static SpottingTrail Create(){var go=new GameObject("Coastal pooled trail");go.SetActive(false);return go.AddComponent<SpottingTrail>();}
        internal static SpottingTrail Rent(Vector3 position,Color color,float lifetime,Material material)
        {
            OptimizationRuntime.EnsureMission();var lease=pool.Rent();var t=lease.Value;t.Lease=lease.Id;RuntimePool<SpottingTrail>.Place(t,Datum.origin,position);t.Initialize(position.ToGlobalPosition(),color,lifetime,material);t.gameObject.SetActive(true);Register(t);return t;
        }
        internal static void Register(SpottingTrail t){if(!active.Contains(t))active.Add(t);if(subscribed)return;Camera.onPreCull+=CameraCull;RenderPipelineManager.beginCameraRendering+=PipelineCamera;subscribed=true;}
        internal static void Unregister(SpottingTrail t){active.Remove(t);if(active.Count==0)Unsubscribe();}
        static void Unsubscribe(){if(!subscribed)return;Camera.onPreCull-=CameraCull;RenderPipelineManager.beginCameraRendering-=PipelineCamera;subscribed=false;}
        static void CameraCull(Camera camera){Refresh(false);}
        static void PipelineCamera(ScriptableRenderContext context,Camera camera){Refresh(false);}
        internal static void Tick(){Refresh(true);}
        static void Refresh(bool retire)
        {
            OptimizationMetrics.Begin("Trails");try{for(int i=active.Count-1;i>=0;i--){if(i>=active.Count)continue;var t=active[i];if(!t){active.RemoveAt(i);continue;}t.Refresh(Time.time);if(retire&&t.Expired){if(!pool.Release(t,t.Lease))RuntimePool<SpottingTrail>.Remove(t.gameObject);}}}finally{OptimizationMetrics.End();}
        }
        internal static void Clear(){pool.Clear();active.Clear();Unsubscribe();}
    }
    public sealed class SpottingTrail:MonoBehaviour
    {
        struct SamplePoint {internal GlobalPosition Position;internal float Born;internal SamplePoint(GlobalPosition p,float t){Position=p;Born=t;}}
        SamplePoint[] samples=new SamplePoint[64];Vector3[] upload=new Vector3[64];int start,count,revision,rendered=-1;
        LineRenderer line;Color color;bool frozen;float lastRefresh=float.NegativeInfinity,lastLength;Vector3 lastHead;
        internal long Lease;internal bool Expired {get{return frozen&&count==0;}}
        public float Lifetime {get;private set;}
        public float MaximumLength {get;set;}=float.PositiveInfinity;
        public float MinimumSampleDistance {get;set;}=8;
        void Awake(){EnsureRenderer();}
        void EnsureRenderer(){if(line)return;line=GetComponent<LineRenderer>();if(!line)line=gameObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=0;line.enabled=false;line.numCornerVertices=3;line.numCapVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.alignment=LineAlignment.View;}
        internal void ResetTrail(){TrailService.Unregister(this);EnsureRenderer();start=count=0;frozen=false;revision=0;rendered=-1;lastRefresh=float.NegativeInfinity;lastHead=Vector3.zero;MaximumLength=float.PositiveInfinity;MinimumSampleDistance=8;line.positionCount=0;line.enabled=false;line.startWidth=1.5f;line.endWidth=.25f;line.startColor=line.endColor=Color.white;line.SetPropertyBlock(null);line.sharedMaterial=null;Array.Clear(samples,0,samples.Length);}
        public void Initialize(GlobalPosition position,Color tint,float lifetime,Material material){ResetTrail();color=tint;Lifetime=lifetime;line.sharedMaterial=material;Sample(position,Time.time);}
        void OnEnable(){TrailService.Register(this);}
        void OnDisable(){TrailService.Unregister(this);}
        SamplePoint At(int i){return samples[(start+i)%samples.Length];}
        void Put(int i,SamplePoint value){samples[(start+i)%samples.Length]=value;}
        void Append(SamplePoint p){if(count==samples.Length){var grown=new SamplePoint[samples.Length*2];for(int i=0;i<count;i++)grown[i]=At(i);samples=grown;start=0;}Put(count++,p);}
        public void Sample(GlobalPosition position,float now)
        {
            if(frozen)return;revision++;var p=new SamplePoint(position,now);if(count==0){Append(p);return;}
            float movement=Vector3.Distance(At(count-1).Position.ToLocalPosition(),position.ToLocalPosition());
            if(movement<.001f)Put(count-1,p);else if(count==1)Append(p);else if(Vector3.Distance(At(count-2).Position.ToLocalPosition(),position.ToLocalPosition())<MinimumSampleDistance)Put(count-1,p);else Append(p);
        }
        public void Freeze(){frozen=true;}
        private void BeforeCameraCull(Camera camera){Refresh(Time.time);}
        private void BeforePipelineCamera(ScriptableRenderContext context,Camera camera){Refresh(Time.time);}
        public void Refresh(float now)
        {
            EnsureRenderer();Vector3 head=count==0?Vector3.zero:At(count-1).Position.ToLocalPosition();
            if(now==lastRefresh&&revision==rendered&&head==lastHead&&MaximumLength==lastLength)return;
            lastRefresh=now;rendered=revision;lastHead=head;lastLength=MaximumLength;
            while(count>0&&now-At(0).Born>=Lifetime){samples[start]=default(SamplePoint);start=(start+1)%samples.Length;count--;}
            if(upload.Length<count)Array.Resize(ref upload,Mathf.NextPowerOfTwo(count));
            int n=0;float remaining=MaximumLength;Vector3 previous=Vector3.zero;
            for(int i=0;i<count;i++){Vector3 point=At(count-1-i).Position.ToLocalPosition();if(i>0){float distance=Vector3.Distance(previous,point);if(distance>remaining){upload[n++]=Vector3.Lerp(previous,point,remaining/distance);break;}remaining-=distance;}upload[n++]=point;previous=point;}
            line.positionCount=n;if(n>0){line.SetPositions(upload);OptimizationMetrics.Count("TrailUploads");}line.enabled=n>=2;
            if(count==0)return;float fade=Mathf.Clamp01(1-(now-At(count-1).Born)/Lifetime);var h=color;h.a=.8f*fade;var tail=color;tail.a=0;line.startColor=h;line.endColor=tail;
        }
    }
}
