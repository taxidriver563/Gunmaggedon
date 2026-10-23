using System;
using System.Collections.Generic;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Networking;
using UnityEngine;

namespace GunmageddonAudio
{
    // Explicit serializers avoid depending on Unity/Mirage's assembly weaver.
    internal struct PayloadHello { internal byte Version; }
    internal struct PayloadRelease
    {
        internal SpecialShellKind Kind;
        internal GlobalPosition Position;
        internal float Gravity;
        internal double ReleasedAt;
        internal Vector3[] Velocities;
    }
    internal struct CloudState
    {
        internal uint Id;
        internal SpecialShellKind Kind;
        internal GlobalPosition Position;
        internal float Age, Lifetime;
    }
    internal struct PayloadClouds { internal CloudState[] States; }

    internal static class SpecialPayloadNetwork
    {
        static NetworkServer server;
        static NetworkClient client;
        static object mission;
        static bool greeted;
        static float nextSnapshot;
        static uint nextId;
        static readonly HashSet<INetworkPlayer> peers = new HashSet<INetworkPlayer>();
        static readonly Dictionary<uint, ShellCloud> remote = new Dictionary<uint, ShellCloud>();
        static readonly List<GameObject> flights = new List<GameObject>();
        static readonly List<uint> expired = new List<uint>();

        internal static void Install()
        {
            Writer<PayloadHello>.Write = (w, m) => w.WriteByte(m.Version);
            Reader<PayloadHello>.Read = r => new PayloadHello { Version = r.ReadByte() };
            Writer<PayloadRelease>.Write = (w, m) => {
                w.WriteByte((byte)m.Kind); Position(w, m.Position); w.WriteSingle(m.Gravity); w.WriteDouble(m.ReleasedAt);
                w.WriteByte((byte)m.Velocities.Length);
                foreach (var v in m.Velocities) { w.WriteSingle(v.x); w.WriteSingle(v.y); w.WriteSingle(v.z); }
            };
            Reader<PayloadRelease>.Read = r => {
                var m = new PayloadRelease { Kind = (SpecialShellKind)r.ReadByte(), Position = Position(r), Gravity = r.ReadSingle(), ReleasedAt = r.ReadDouble() };
                int count = r.ReadByte(); if (count > 64) throw new InvalidOperationException("Invalid payload count.");
                m.Velocities = new Vector3[count];
                for (int i = 0; i < count; i++) m.Velocities[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                return m;
            };
            Writer<PayloadClouds>.Write = (w, m) => {
                w.WriteByte((byte)m.States.Length);
                foreach (var c in m.States) { w.WriteUInt32(c.Id); w.WriteByte((byte)c.Kind); Position(w, c.Position); w.WriteSingle(c.Age); w.WriteSingle(c.Lifetime); }
            };
            Reader<PayloadClouds>.Read = r => {
                int count = r.ReadByte(); if (count > 64) throw new InvalidOperationException("Invalid cloud count.");
                var states = new CloudState[count];
                for (int i = 0; i < count; i++) states[i] = new CloudState { Id = r.ReadUInt32(), Kind = (SpecialShellKind)r.ReadByte(), Position = Position(r), Age = r.ReadSingle(), Lifetime = r.ReadSingle() };
                return new PayloadClouds { States = states };
            };
            MessagePacker.RegisterMessage<PayloadHello>();
            MessagePacker.RegisterMessage<PayloadRelease>();
            MessagePacker.RegisterMessage<PayloadClouds>();
        }

        static void Position(NetworkWriter w, GlobalPosition p) { w.WriteSingle(p.x); w.WriteSingle(p.y); w.WriteSingle(p.z); }
        static GlobalPosition Position(NetworkReader r) { return new GlobalPosition { x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle() }; }

        internal static void Tick()
        {
            var manager = NetworkManagerNuclearOption.i;
            var s = manager && manager.Server && manager.Server.Active ? manager.Server : null;
            var c = manager && manager.Client && manager.Client.Active ? manager.Client : null;
            if (server != s) {
                if (server) server.MessageHandler.UnregisterHandler<PayloadHello>();
                server = s; peers.Clear();
                if (server) server.MessageHandler.RegisterHandler<PayloadHello>(Hello, false);
            }
            if (client != c) {
                if (client) { client.MessageHandler.UnregisterHandler<PayloadRelease>(); client.MessageHandler.UnregisterHandler<PayloadClouds>(); }
                client = c; greeted = false; ClearRemote();
                if (client) { client.MessageHandler.RegisterHandler<PayloadRelease>(ReceiveRelease, false); client.MessageHandler.RegisterHandler<PayloadClouds>(ReceiveClouds, false); }
            }
            var currentMission = manager ? manager.NetworkMission : null;
            if (!ReferenceEquals(mission, currentMission)) { mission = currentMission; ClearRemote(); nextSnapshot = 0f; }
            if (client && !client.IsHost && client.Player != null && client.Player.IsAuthenticated && !greeted) {
                client.Send(new PayloadHello { Version = 1 }, Channel.Reliable); greeted = true;
            }
            if (server && Time.unscaledTime >= nextSnapshot) {
                nextSnapshot = Time.unscaledTime + 1f;
                peers.RemoveWhere(p => !p.IsConnected || !p.IsAuthenticated);
                var snapshot = Snapshot();
                foreach (var peer in peers) peer.Send(snapshot, Channel.Reliable);
            }
        }

        static void Hello(INetworkPlayer peer, PayloadHello message)
        {
            if (!server || peer.IsHost || !peer.IsAuthenticated || message.Version != 1) return;
            peers.Add(peer); peer.Send(Snapshot(), Channel.Reliable);
        }
        static PayloadClouds Snapshot()
        {
            var states = new List<CloudState>();
            foreach (var c in ShellObscurants.Clouds) if (c && !c.Remote && c.isActiveAndEnabled && Time.time - c.Born < c.Lifetime) {
                if (c.NetworkId == 0) c.NetworkId = ++nextId;
                states.Add(new CloudState { Id = c.NetworkId, Kind = c.Kind, Position = c.Position, Age = Mathf.Max(0, Time.time - c.Born), Lifetime = c.Lifetime });
            }
            return new PayloadClouds { States = states.ToArray() };
        }
        internal static void Release(SpecialShellKind kind, GlobalPosition position, float gravity, Vector3[] velocities)
        {
            if (!server) return;
            var message = new PayloadRelease { Kind = kind, Position = position, Gravity = gravity, ReleasedAt = server.World.Time.Time, Velocities = velocities };
            foreach (var peer in peers) if (peer.IsConnected && peer.IsAuthenticated) peer.Send(message, Channel.Reliable);
        }
        internal static void CloudsChanged() { nextSnapshot = 0f; }
        static bool Receiving { get { return client && !client.IsHost && !server && Datum.origin && NetworkManagerNuclearOption.i && !NetworkManagerNuclearOption.IsLoadingScene; } }
        static void ReceiveRelease(INetworkPlayer sender, PayloadRelease message)
        {
            if (!Receiving || !ValidKind(message.Kind) || message.Velocities == null || message.Velocities.Length == 0) return;
            var go = SpecialShells.PayloadObject(message.Kind, message.Position, message.Kind == SpecialShellKind.Cluster);
            go.GetComponent<SpecialPayloadFlight>().Initialize(message.Kind, null, message.Position, Vector3.zero,
                message.Velocities.Length, message.Gravity, message.Velocities);
            go.GetComponent<SpecialPayloadFlight>().CatchUp(Mathf.Clamp((float)(client.World.Time.Time - message.ReleasedAt), 0f, 2f));
            flights.RemoveAll(o => !o); flights.Add(go);
        }
        internal static void UntrackFlight(GameObject go){flights.Remove(go);}
        static bool ValidKind(SpecialShellKind kind) { return kind == SpecialShellKind.Cluster || kind == SpecialShellKind.Smoke || kind == SpecialShellKind.Chaff; }
        static void ReceiveClouds(INetworkPlayer sender, PayloadClouds message)
        {
            if (!Receiving) return;
            ApplyClouds(message);
        }
        internal static void ApplyClouds(PayloadClouds message)
        {
            var present = new HashSet<uint>();
            foreach (var s in message.States) {
                if ((s.Kind != SpecialShellKind.Smoke && s.Kind != SpecialShellKind.Chaff) || s.Id == 0 || s.Age < 0 || s.Age >= s.Lifetime || s.Lifetime > 180f) continue;
                present.Add(s.Id); ShellCloud cloud;
                if (!remote.TryGetValue(s.Id, out cloud) || !cloud) {
                    SpecialShells.EnsureMaterials();
                    var go = new GameObject("Remote coastal " + s.Kind + " cloud"); go.transform.SetParent(Datum.origin, false); SpecialShells.Track(go);
                    cloud = go.AddComponent<ShellCloud>(); cloud.Remote = true;
                    cloud.Initialize(s.Kind, s.Position, s.Lifetime); cloud.NetworkId = s.Id; remote[s.Id] = cloud;
                    cloud.PresentAge(s.Age);
                }
                cloud.Born = Time.time - s.Age; cloud.Position = s.Position; cloud.transform.position = s.Position.ToLocalPosition();
            }
            expired.Clear(); foreach (var pair in remote) if (!present.Contains(pair.Key)) expired.Add(pair.Key);
            foreach (var id in expired) { Remove(remote[id]); remote.Remove(id); }
            expired.Clear();
        }
        static void Remove(ShellCloud cloud)
        {
            if (!cloud) return;
            ShellObscurants.Clouds.Remove(cloud); cloud.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(cloud.gameObject); else UnityEngine.Object.DestroyImmediate(cloud.gameObject);
        }
        static void ClearRemote()
        {
            foreach (var cloud in remote.Values) Remove(cloud); remote.Clear();
            while(flights.Count>0){var go=flights[flights.Count-1];flights.RemoveAt(flights.Count-1);if(!go)continue;var flight=go.GetComponent<SpecialPayloadFlight>();if(flight)ClusterVisualPools.Flights.Release(flight,flight.Lease);else{go.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(go);else UnityEngine.Object.DestroyImmediate(go);}}
        }
        internal static void Shutdown()
        {
            if (server) server.MessageHandler.UnregisterHandler<PayloadHello>();
            if (client) { client.MessageHandler.UnregisterHandler<PayloadRelease>(); client.MessageHandler.UnregisterHandler<PayloadClouds>(); }
            server = null; client = null; mission = null; greeted = false; peers.Clear(); ClearRemote();
        }
    }
}
