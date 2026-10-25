using System.Collections.Generic;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class F88FlakEffects
    {
        sealed class Bank {internal GameObject Template,Parking;internal readonly List<F88BurstPuff> Pool=new List<F88BurstPuff>();}
        static readonly Dictionary<GameObject,Bank> Banks=new Dictionary<GameObject,Bank>();
        internal static void Play(GameObject source,GlobalPosition position,float age=0)
        {
            if(!source||!Datum.origin||age>33)return;
            Bank bank;if(!Banks.TryGetValue(source,out bank)){bank=new Bank();Banks.Add(source,bank);}
            if(!bank.Template) {
                bank.Parking=new GameObject("Heavy flak effect templates");bank.Parking.transform.SetParent(Datum.origin,false);bank.Parking.SetActive(false);
                bank.Template=Object.Instantiate(source,bank.Parking.transform,false);
                foreach(var b in bank.Template.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
                foreach(var a in bank.Template.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(a);
            }
            var Pool=bank.Pool;
            F88BurstPuff free=null,oldest=null;
            for(int i=Pool.Count-1;i>=0;i--){var p=Pool[i];if(!p){Pool.RemoveAt(i);continue;}if(!p.gameObject.activeSelf)free=p;if(!oldest||p.RetireAt<oldest.RetireAt)oldest=p;}
            if(!free) {
                if(Pool.Count>=128)free=oldest;
                else {var go=Object.Instantiate(bank.Template,Datum.origin,false);go.name="F88 independent flak puff";free=go.AddComponent<F88BurstPuff>();free.Initialize();Pool.Add(free);}
            }
            free.Begin(position,age);
            if(age<2)Plugin.PlaceholderSound(position.ToLocalPosition(),source,"F88 flak burst");
        }
        internal static void Shutdown()
        {
            foreach(var bank in Banks.Values){foreach(var p in bank.Pool)if(p)Remove(p.gameObject);bank.Pool.Clear();if(bank.Parking)Remove(bank.Parking);}
            Banks.Clear();
        }
        static void Remove(Object o){if(Application.isPlaying)Object.Destroy(o);else Object.DestroyImmediate(o);}
    }
    public sealed class F88BurstPuff:MonoBehaviour
    {
        ParticleSystem[] systems;float life;internal float RetireAt;
        internal void Initialize()
        {
            systems=GetComponentsInChildren<ParticleSystem>(true);
            foreach(var p in systems){p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var m=p.main;m.loop=false;m.playOnAwake=false;m.stopAction=ParticleSystemStopAction.None;m.simulationSpace=ParticleSystemSimulationSpace.Custom;m.customSimulationSpace=Datum.origin;life=Mathf.Max(life,m.duration+m.startLifetime.constantMax+1);}
        }
        internal void Begin(GlobalPosition position,float age)
        {
            foreach(var p in systems)p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            transform.position=position.ToLocalPosition();gameObject.SetActive(true);RetireAt=Time.time+life-age;
            foreach(var p in systems){var m=p.main;m.customSimulationSpace=Datum.origin;p.gameObject.SetActive(true);if(age>0)p.Simulate(age,false,true,false);p.Play(false);}
        }
        void Update(){if(Time.time>=RetireAt)gameObject.SetActive(false);}
    }
}
