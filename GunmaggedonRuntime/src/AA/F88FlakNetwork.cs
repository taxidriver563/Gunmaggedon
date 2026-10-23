using System;
using System.Collections.Generic;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Networking;
using UnityEngine;
namespace GunmageddonAudio
{
    internal struct FlakHello {internal byte Version;}
    internal struct FlakBurst {internal GlobalPosition Position;internal double Time;}
    internal struct FlakPose {internal uint Unit;internal float Yaw,Pitch;}
    internal static class F88FlakNetwork
    {
        static NetworkServer server;static NetworkClient client;static bool greeted;
        static readonly HashSet<INetworkPlayer> Peers=new HashSet<INetworkPlayer>();
        internal static void Install()
        {
            Writer<FlakHello>.Write=(w,m)=>w.WriteByte(m.Version);Reader<FlakHello>.Read=r=>new FlakHello{Version=r.ReadByte()};
            Writer<FlakBurst>.Write=(w,m)=>{w.WriteSingle(m.Position.x);w.WriteSingle(m.Position.y);w.WriteSingle(m.Position.z);w.WriteDouble(m.Time);};
            Reader<FlakBurst>.Read=r=>new FlakBurst{Position=new GlobalPosition(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Time=r.ReadDouble()};
            Writer<FlakPose>.Write=(w,m)=>{w.WriteUInt32(m.Unit);w.WriteSingle(m.Yaw);w.WriteSingle(m.Pitch);};Reader<FlakPose>.Read=r=>new FlakPose{Unit=r.ReadUInt32(),Yaw=r.ReadSingle(),Pitch=r.ReadSingle()};
            MessagePacker.RegisterMessage<FlakHello>();MessagePacker.RegisterMessage<FlakBurst>();MessagePacker.RegisterMessage<FlakPose>();
        }
        internal static void Tick()
        {
            var m=NetworkManagerNuclearOption.i;var s=m&&m.Server&&m.Server.Active?m.Server:null;var c=m&&m.Client&&m.Client.Active?m.Client:null;
            if(server!=s){if(server)server.MessageHandler.UnregisterHandler<FlakHello>();server=s;Peers.Clear();if(server)server.MessageHandler.RegisterHandler<FlakHello>(Hello,false);}
            if(client!=c){if(client){client.MessageHandler.UnregisterHandler<FlakBurst>();client.MessageHandler.UnregisterHandler<FlakPose>();}client=c;greeted=false;
                if(client){client.MessageHandler.RegisterHandler<FlakBurst>(ReceiveBurst,false);client.MessageHandler.RegisterHandler<FlakPose>(ReceivePose,false);}}
            if(client&&!client.IsHost&&!greeted&&client.Player!=null&&client.Player.IsAuthenticated){client.Send(new FlakHello{Version=1},Channel.Reliable);greeted=true;}
            Peers.RemoveWhere(p=>!p.IsConnected||!p.IsAuthenticated);
        }
        static void Hello(INetworkPlayer peer,FlakHello hello){if(server&&!peer.IsHost&&peer.IsAuthenticated&&hello.Version==1)Peers.Add(peer);}
        static bool Receiving {get{return client&&!client.IsHost&&!server&&Datum.origin&&!NetworkManagerNuclearOption.IsLoadingScene;}}
        internal static void Burst(GlobalPosition position)
        {if(server)foreach(var p in Peers)p.Send(new FlakBurst{Position=position,Time=server.World.Time.Time},Channel.Reliable);}
        internal static void Pose(Unit source,float yaw,float pitch)
        {
            if(!server)return;var id=source.GetComponent<NetworkIdentity>();if(!id||id.NetId==0)return;
            foreach(var p in Peers)p.Send(new FlakPose{Unit=id.NetId,Yaw=yaw,Pitch=pitch},Channel.Unreliable);
        }
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        static void ReceiveBurst(INetworkPlayer sender,FlakBurst b)
        {if(Receiving&&!double.IsNaN(b.Time)&&!double.IsInfinity(b.Time)&&Finite(b.Position.x)&&Finite(b.Position.y)&&Finite(b.Position.z))F88FlakEffects.Play(F88FlakRuntime.EffectPrefab,b.Position,Mathf.Clamp((float)(client.World.Time.Time-b.Time),0,34));}
        static void ReceivePose(INetworkPlayer sender,FlakPose p)
        {
            if(!Receiving||!Finite(p.Yaw)||!Finite(p.Pitch))return;NetworkIdentity id;
            if(client.World.TryGetIdentity(p.Unit,out id)){var c=id.GetComponentInChildren<F88FlakController>();if(c)c.RemotePose(p.Yaw,p.Pitch);}
        }
        internal static void Shutdown()
        {
            if(server)server.MessageHandler.UnregisterHandler<FlakHello>();if(client){client.MessageHandler.UnregisterHandler<FlakBurst>();client.MessageHandler.UnregisterHandler<FlakPose>();}
            Peers.Clear();server=null;client=null;greeted=false;
        }
    }
}
