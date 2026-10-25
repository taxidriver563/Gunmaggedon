using System;
using System.Collections.Generic;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Networking;
using UnityEngine;
namespace GunmageddonAudio
{
    internal struct FlakHello {internal byte Version;}
    internal struct FlakBurst {internal GlobalPosition Position;internal double Time;internal bool Heavy;}
    internal struct FlakPose {internal uint Unit;internal float Yaw,Pitch;}
    internal struct R230ChargeCue {internal uint Unit;internal GlobalPosition Point;internal float Progress;internal bool Active;}
    internal struct R230NuclearBurst {internal GlobalPosition Point;internal uint Owner;}
    internal static class F88FlakNetwork
    {
        static NetworkServer server;static NetworkClient client;static bool greeted;
        static readonly HashSet<INetworkPlayer> Peers=new HashSet<INetworkPlayer>();
        internal static void Install()
        {
            Writer<FlakHello>.Write=(w,m)=>w.WriteByte(m.Version);Reader<FlakHello>.Read=r=>new FlakHello{Version=r.ReadByte()};
            Writer<FlakBurst>.Write=(w,m)=>{w.WriteSingle(m.Position.x);w.WriteSingle(m.Position.y);w.WriteSingle(m.Position.z);w.WriteDouble(m.Time);w.WriteByte(m.Heavy?(byte)1:(byte)0);};
            Reader<FlakBurst>.Read=r=>new FlakBurst{Position=new GlobalPosition(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Time=r.ReadDouble(),Heavy=r.ReadByte()!=0};
            Writer<FlakPose>.Write=(w,m)=>{w.WriteUInt32(m.Unit);w.WriteSingle(m.Yaw);w.WriteSingle(m.Pitch);};Reader<FlakPose>.Read=r=>new FlakPose{Unit=r.ReadUInt32(),Yaw=r.ReadSingle(),Pitch=r.ReadSingle()};
            Writer<R230ChargeCue>.Write=(w,m)=>{w.WriteUInt32(m.Unit);w.WriteSingle(m.Point.x);w.WriteSingle(m.Point.y);w.WriteSingle(m.Point.z);w.WriteSingle(m.Progress);w.WriteByte(m.Active?(byte)1:(byte)0);};
            Reader<R230ChargeCue>.Read=r=>new R230ChargeCue{Unit=r.ReadUInt32(),Point=new GlobalPosition(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Progress=r.ReadSingle(),Active=r.ReadByte()!=0};
            Writer<R230NuclearBurst>.Write=(w,m)=>{w.WriteSingle(m.Point.x);w.WriteSingle(m.Point.y);w.WriteSingle(m.Point.z);w.WriteUInt32(m.Owner);};
            Reader<R230NuclearBurst>.Read=r=>new R230NuclearBurst{Point=new GlobalPosition(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Owner=r.ReadUInt32()};
            MessagePacker.RegisterMessage<R230NuclearBurst>();
            MessagePacker.RegisterMessage<FlakHello>();MessagePacker.RegisterMessage<FlakBurst>();MessagePacker.RegisterMessage<FlakPose>();MessagePacker.RegisterMessage<R230ChargeCue>();
        }
        internal static void Tick()
        {
            var m=NetworkManagerNuclearOption.i;var s=m&&m.Server&&m.Server.Active?m.Server:null;var c=m&&m.Client&&m.Client.Active?m.Client:null;
            if(server!=s){if(server)server.MessageHandler.UnregisterHandler<FlakHello>();server=s;Peers.Clear();if(server)server.MessageHandler.RegisterHandler<FlakHello>(Hello,false);}
            if(client!=c){if(client){client.MessageHandler.UnregisterHandler<FlakBurst>();client.MessageHandler.UnregisterHandler<FlakPose>();client.MessageHandler.UnregisterHandler<R230ChargeCue>();client.MessageHandler.UnregisterHandler<R230NuclearBurst>();}client=c;greeted=false;
                if(client){client.MessageHandler.RegisterHandler<FlakBurst>(ReceiveBurst,false);client.MessageHandler.RegisterHandler<FlakPose>(ReceivePose,false);client.MessageHandler.RegisterHandler<R230ChargeCue>(ReceiveWarning,false);client.MessageHandler.RegisterHandler<R230NuclearBurst>(ReceiveNuclear,false);}}
            if(client&&!client.IsHost&&!greeted&&client.Player!=null&&client.Player.IsAuthenticated){client.Send(new FlakHello{Version=5},Channel.Reliable);greeted=true;}
            Peers.RemoveWhere(p=>!p.IsConnected||!p.IsAuthenticated);
        }
        static void Hello(INetworkPlayer peer,FlakHello hello){if(server&&!peer.IsHost&&peer.IsAuthenticated&&hello.Version==5)Peers.Add(peer);}
        static bool Receiving {get{return client&&!client.IsHost&&!server&&Datum.origin&&!NetworkManagerNuclearOption.IsLoadingScene;}}
        internal static void Burst(GlobalPosition position)
        {BurstProfile(position,false);}
        internal static void BurstProfile(GlobalPosition position,bool heavy)
        {if(server)foreach(var p in Peers)p.Send(new FlakBurst{Position=position,Time=server.World.Time.Time,Heavy=heavy},Channel.Reliable);}
        internal static void Pose(Unit source,float yaw,float pitch)
        {
            if(!server)return;var id=source.GetComponent<NetworkIdentity>();if(!id||id.NetId==0)return;
            foreach(var p in Peers)p.Send(new FlakPose{Unit=id.NetId,Yaw=yaw,Pitch=pitch},Channel.Unreliable);
        }
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        internal static void NuclearBurst(GlobalPosition point,uint owner)
        {if(server)foreach(var peer in Peers)peer.Send(new R230NuclearBurst{Point=point,Owner=owner},Channel.Reliable);}
        static void ReceiveNuclear(INetworkPlayer sender,R230NuclearBurst burst)
        {
            if(!Receiving||!Finite(burst.Point.x)||!Finite(burst.Point.y)||!Finite(burst.Point.z))return;
            NetworkIdentity identity;var unit=client.World.TryGetIdentity(burst.Owner,out identity)?identity.GetComponent<Unit>():null;
            R230NuclearRuntime.Play(burst.Point,unit?unit.persistentID:PersistentID.None);
        }
        internal static void Warning(Unit source,GlobalPosition point,float progress,bool active)
        {if(!server||!source)return;var id=source.GetComponent<NetworkIdentity>();if(!id||id.NetId==0)return;foreach(var p in Peers)p.Send(new R230ChargeCue{Unit=id.NetId,Point=point,Progress=progress,Active=active},Channel.Reliable);}
        static void ReceiveWarning(INetworkPlayer sender,R230ChargeCue p)
        {if(!Receiving||!Finite(p.Point.x)||!Finite(p.Point.y)||!Finite(p.Point.z)||!Finite(p.Progress))return;NetworkIdentity id;if(client.World.TryGetIdentity(p.Unit,out id)){var c=id.GetComponentInChildren<R230Controller>();if(c)c.RemoteWarning(p.Point,Mathf.Clamp01(p.Progress),p.Active);}}
        static void ReceiveBurst(INetworkPlayer sender,FlakBurst b)
        {if(Receiving&&!double.IsNaN(b.Time)&&!double.IsInfinity(b.Time)&&Finite(b.Position.x)&&Finite(b.Position.y)&&Finite(b.Position.z))F88FlakEffects.Play(b.Heavy?F88FlakRuntime.HeavyEffectPrefab:F88FlakRuntime.EffectPrefab,b.Position,Mathf.Clamp((float)(client.World.Time.Time-b.Time),0,34));}
        static void ReceivePose(INetworkPlayer sender,FlakPose p)
        {
            if(!Receiving||!Finite(p.Yaw)||!Finite(p.Pitch))return;NetworkIdentity id;
            if(client.World.TryGetIdentity(p.Unit,out id)){var c=id.GetComponentInChildren<F88FlakController>();if(c)c.RemotePose(p.Yaw,p.Pitch);var r=id.GetComponentInChildren<R230Controller>();if(r)r.RemotePose(p.Yaw,p.Pitch);}
        }
        internal static void Shutdown()
        {
            if(server)server.MessageHandler.UnregisterHandler<FlakHello>();if(client){client.MessageHandler.UnregisterHandler<FlakBurst>();client.MessageHandler.UnregisterHandler<FlakPose>();client.MessageHandler.UnregisterHandler<R230ChargeCue>();client.MessageHandler.UnregisterHandler<R230NuclearBurst>();}
            Peers.Clear();server=null;client=null;greeted=false;
        }
    }
}
