using System.Collections.Generic;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class F88Casings
    {
        static readonly List<F88CasingProp> Pool=new List<F88CasingProp>();
        internal static void Eject(GameObject source,Transform pose,Unit owner)
        {
            if(!source||!Datum.origin)return;
            F88CasingProp free=null,oldest=null;
            for(int i=Pool.Count-1;i>=0;i--){var p=Pool[i];if(!p){Pool.RemoveAt(i);continue;}if(!p.gameObject.activeSelf)free=p;if(!oldest||p.RetireAt<oldest.RetireAt)oldest=p;}
            if(!free){if(Pool.Count>=64)free=oldest;else{var go=Object.Instantiate(source,Datum.origin,false);go.SetActive(false);go.name="F88 spent casing";free=go.AddComponent<F88CasingProp>();free.Initialize();Pool.Add(free);}}
            free.Begin(pose,owner);
        }
        internal static void Shutdown(){foreach(var p in Pool)if(p){if(Application.isPlaying)Object.Destroy(p.gameObject);else Object.DestroyImmediate(p.gameObject);}Pool.Clear();}
    }
    public sealed class F88CasingProp:MonoBehaviour
    {
        Rigidbody body;Collider shell;Collider[] ignored;internal float RetireAt;
        internal void Initialize(){body=GetComponent<Rigidbody>();shell=GetComponent<Collider>();}
        internal void Begin(Transform pose,Unit owner)
        {
            if(ignored!=null)foreach(var c in ignored)if(c)Physics.IgnoreCollision(shell,c,false);
            transform.SetPositionAndRotation(pose.position,pose.rotation);body.position=pose.position;body.rotation=pose.rotation;
            gameObject.SetActive(true);ignored=owner?owner.GetComponentsInChildren<Collider>(true):null;
            if(ignored!=null)foreach(var c in ignored)if(c)Physics.IgnoreCollision(shell,c,true);
            body.velocity=(owner&&owner.rb?owner.rb.GetPointVelocity(pose.position):Vector3.zero)-pose.forward*.6f+pose.up*.1f;
            body.angularVelocity=pose.right*.7f;body.WakeUp();RetireAt=Time.time+8;
        }
        void Update(){if(Time.time>=RetireAt){body.velocity=body.angularVelocity=Vector3.zero;gameObject.SetActive(false);}}
    }
}
