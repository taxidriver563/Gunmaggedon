using System;
using System.Collections.Generic;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class IncendiaryCanisterRuntime
    {
        internal const string Key = "Gunmaggedon_F120S_AA", Weapon = "F120S_AA_Weapon";
        internal static GameObject EffectPrefab;
        internal const float SmokeLifetime = 5f;
        static readonly RuntimePool<ParticleSystem> smokePool = new RuntimePool<ParticleSystem>(16, CreateSmoke,
            ps => { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = null; });
        static ParticleSystem CreateSmoke()
        {
            var go = new GameObject("F120S fragment smoke"); go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom; main.maxParticles = 2048;
            main.startLifetime = SmokeLifetime; main.startSpeed = 0; main.startSize = 1.4f;
            main.startColor = new Color(.48f, .46f, .42f, .38f); main.gravityModifier = 0;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.5f));
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.65f, .4f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            return ps;
        }
        sealed class Piece
        {
            internal bool Active;
            internal GlobalPosition Position;
            internal Vector3 Velocity;
            internal float LaunchSpeed;
            internal LineRenderer Head;
            internal long Lease;
            internal SpottingTrail Trail;
            internal readonly Vector3[] Points = new Vector3[2];
        }
        sealed class Flight
        {
            internal readonly Piece[] Pieces = new Piece[IncendiaryCanisterPolicy.FragmentCount];
            internal PersistentID Source;
            internal bool Authority;
            internal bool Spent;
            internal float Age;
            internal ParticleSystem Smoke;
            internal long SmokeLease;
            internal float NextSmoke, SmokeUntil, CatchupAge;
            internal Flight() { for (int i = 0; i < Pieces.Length; i++) Pieces[i] = new Piece(); }
        }
        static readonly List<Flight> flights = new List<Flight>();
        static readonly Stack<Flight> idle = new Stack<Flight>();
        static int sequence, epoch;
        static bool presenting;
        internal static bool Matches(Gun gun) { return gun && gun.info && gun.info.name == Weapon &&
            gun.attachedUnit && gun.attachedUnit.definition && gun.attachedUnit.definition.jsonKey == Key; }
        internal static void Burst(GlobalPosition position, Vector3 velocity, PersistentID source)
        {
            int seed = unchecked(++sequence * 73856093);
            Begin(position, velocity, source, seed, true, 0);
            F88FlakNetwork.Incendiary(position, velocity, seed);
        }
        internal static void Begin(GlobalPosition position, Vector3 velocity, PersistentID source, int seed, bool authority, float age)
        {
            if (!Datum.origin || age >= IncendiaryCanisterPolicy.Lifetime) return;
            OptimizationRuntime.EnsureMission();
            SpecialShells.EnsureMaterials();
            F88FlakEffects.Play(EffectPrefab, position, age);
            var flight = idle.Count > 0 ? idle.Pop() : new Flight();
            flight.Source = source; flight.Authority = authority; flight.Age = 0; flight.Spent = false;
            flight.NextSmoke = 0; flight.SmokeUntil = 0; flight.CatchupAge = age;
            var smokeLease = smokePool.Rent(); flight.Smoke = smokeLease.Value; flight.SmokeLease = smokeLease.Id;
            RuntimePool<ParticleSystem>.Place(flight.Smoke, Datum.origin, Datum.origin.position);
            var smokeMain = flight.Smoke.main; smokeMain.customSimulationSpace = Datum.origin;
            flight.Smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = SpecialShells.SmokeMaterial;
            flight.Smoke.gameObject.SetActive(true); flight.Smoke.Play(false);
            var glow = new MaterialPropertyBlock(); glow.SetColor("_Color", new Color(4, 2, .8f, 1)); glow.SetColor("_BaseColor", new Color(4, 2, .8f, 1));
            for (int i = 0; i < flight.Pieces.Length; i++)
            {
                var p = flight.Pieces[i]; p.Active = true; p.Position = position;
                var v = IncendiaryCanisterPolicy.Velocity(new OpticalVector(velocity.x, velocity.y, velocity.z), i, seed);
                p.Velocity = new Vector3((float)v.X, (float)v.Y, (float)v.Z); p.LaunchSpeed = p.Velocity.magnitude;
                var lease = ClusterVisualPools.Heads.Rent(); p.Head = lease.Value; p.Lease = lease.Id;
                RuntimePool<LineRenderer>.Place(p.Head, Datum.origin, position.ToLocalPosition());
                p.Head.useWorldSpace = false; p.Head.positionCount = 2; p.Head.sharedMaterial = SpecialShells.SparkMaterial;
                p.Head.alignment = LineAlignment.View; p.Head.startWidth = .65f; p.Head.endWidth = .12f;
                p.Head.startColor = new Color(1f, .75f, .2f); p.Head.endColor = new Color(1f, .15f, .01f, 0);
                p.Head.enabled = false; p.Head.gameObject.SetActive(true);
                p.Head.SetPropertyBlock(glow);
                p.Trail = CoastalRanging.BuildTrail(position.ToLocalPosition(), new Color(1f, .25f, .015f), .32f);
                if (p.Trail)
                {
                    p.Trail.MaximumLength = 45; p.Trail.MinimumSampleDistance = .25f;
                    var line = p.Trail.GetComponent<LineRenderer>(); line.startWidth = .4f; line.endWidth = .035f;
                    line.SetPropertyBlock(glow);
                }
            }
            flights.Add(flight);
            if (!presenting) { TrailService.RegisterPresentation(Render); presenting = true; }
            // Late remote cues catch up in fixed increments, without damage.
            while (age > .0001f) { float step = Mathf.Min(.02f, age); Advance(flight, step); age -= step; }
        }
        internal static void Tick(float step)
        {
            if (flights.Count == 0) return;
            OptimizationRuntime.EnsureMission();
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i]; int stamp = epoch;
                bool active = Advance(flight, step);
                if (stamp != epoch) return;
                if (active) continue;
                flights.RemoveAt(i); flight.Source = PersistentID.None; flight.Authority = false;
                ReleaseSmoke(flight);
                if (idle.Count < 16) idle.Push(flight);
            }
            if (flights.Count == 0 && presenting) { TrailService.UnregisterPresentation(Render); presenting = false; }
        }
        static bool Advance(Flight flight, float step)
        {
            flight.Age += step; bool any = false;
            if (flight.Spent) return flight.Age < flight.SmokeUntil;
            bool emitSmoke = flight.Age >= flight.NextSmoke;
            if (emitSmoke) flight.NextSmoke = flight.Age + .16f;
            int stamp = epoch;
            foreach (var p in flight.Pieces)
            {
                if (!p.Active) continue;
                Vector3 from = p.Position.ToLocalPosition();
                p.Velocity = (p.Velocity + Physics.gravity * step) * Mathf.Exp(-(float)IncendiaryCanisterPolicy.Drag * step);
                Vector3 delta = p.Velocity * step; RaycastHit hit = default(RaycastHit);
                bool impact = delta.sqrMagnitude > .0001f && Physics.SphereCast(from, .8f, delta.normalized, out hit,
                    delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (!impact) hit = default(RaycastHit);
                Vector3 to = impact ? hit.point : from + delta;
                p.Position = to.ToGlobalPosition(); if (p.Trail) p.Trail.Sample(p.Position, Time.time);
                if (emitSmoke && flight.Smoke)
                {
                    var smoke = new ParticleSystem.EmitParams { position = Datum.origin.InverseTransformPoint(to),
                        velocity = Vector3.up * .35f, startLifetime = SmokeLifetime - Mathf.Max(0, flight.CatchupAge - flight.Age),
                        startSize = 1.4f, startColor = new Color(.48f, .46f, .42f, .38f) };
                    flight.Smoke.Emit(smoke, 1); flight.SmokeUntil = flight.Age + SmokeLifetime;
                }
                if (impact || to.y <= Datum.LocalSeaY + .2f || flight.Age >= IncendiaryCanisterPolicy.Lifetime)
                {
                    // Retire before native damage callbacks can destroy owners.
                    var collider = impact ? hit.collider : null;
                    float energy = (float)IncendiaryCanisterPolicy.Energy(p.Velocity.magnitude, p.LaunchSpeed);
                    Retire(p);
                    if (flight.Authority && collider) Hit(collider, energy, flight.Source);
                    if (stamp != epoch) return false;
                    continue;
                }
                any = true;
            }
            if (!any) { flight.Source = PersistentID.None; flight.Authority = false; flight.Spent = true; }
            return any || flight.Age < flight.SmokeUntil;
        }
        internal static void Hit(Collider collider, float energy, PersistentID source)
        {
            var part = collider ? collider.GetComponent<IDamageable>() : null;
            var native = collider ? collider.GetComponentInParent<UnitPart>() : null;
            var unit = native ? native.parentUnit : null;
            // The host must also damage aircraft simulated by a remote pilot.
            if (part == null || !unit || !unit.IsServer || unit.Networkdisabled || energy <= 0) return;
            // Native armor, fire resistance, heat/ignition and attribution apply.
            part.TakeDamage(420f * energy, 0, 1, 65f * Mathf.Sqrt(energy), 0, source);
        }
        static void Retire(Piece p)
        {
            p.Active = false;
            if (p.Trail) p.Trail.Freeze(); p.Trail = null;
            if (p.Head) ClusterVisualPools.Heads.Release(p.Head, p.Lease); p.Head = null; p.Lease = 0;
            p.Velocity = Vector3.zero;
        }
        internal static void Render()
        {
            if (flights.Count == 0) return;
            OptimizationRuntime.EnsureMission();
            foreach (var flight in flights) foreach (var p in flight.Pieces) if (p.Active && p.Head)
            {
                p.Head.transform.position = p.Position.ToLocalPosition();
                p.Points[0] = Vector3.zero;
                p.Points[1] = p.Head.transform.InverseTransformDirection(-p.Velocity.normalized * Mathf.Min(8, p.Velocity.magnitude * .01f));
                p.Head.SetPositions(p.Points); p.Head.enabled = true;
            }
        }
        internal static void Clear()
        {
            epoch++;
            if (presenting) TrailService.UnregisterPresentation(Render); presenting = false;
            foreach (var flight in flights) { foreach (var p in flight.Pieces) Retire(p); ReleaseSmoke(flight); }
            flights.Clear(); idle.Clear(); sequence = 0;
            smokePool.Clear();
        }
        static void ReleaseSmoke(Flight flight)
        {
            if (flight.Smoke) smokePool.Release(flight.Smoke, flight.SmokeLease);
            flight.Smoke = null; flight.SmokeLease = 0; flight.NextSmoke = flight.SmokeUntil = flight.CatchupAge = 0;
        }
        internal static void Shutdown() { Clear(); EffectPrefab = null; }
    }
}
