using System;
using System.Collections.Generic;
using UnityEngine;

namespace GunmageddonAudio
{
    internal sealed class ClusterDamageQuery
    {
        private static readonly Stack<ClusterDamageQuery> Pool = new Stack<ClusterDamageQuery>();
        private Collider[] buffer = new Collider[64];
        private bool active;
        internal Collider[] Hits;
        internal int Count;
        internal readonly HashSet<UnitPart> Damaged = new HashSet<UnitPart>();

        internal static ClusterDamageQuery Rent(Vector3 position)
        {
            // Damage callbacks can nest; each active invocation owns independent scratch.
            var query = Pool.Count == 0 ? new ClusterDamageQuery() : Pool.Pop();
            query.active = true;
            try
            {
                while (true)
                {
                    query.Count = Physics.OverlapSphereNonAlloc(position, 7f, query.buffer,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (query.Count < query.buffer.Length || query.buffer.Length >= 1024) break;
                    Array.Resize(ref query.buffer, query.buffer.Length * 2);
                }
                // A saturated buffer is ambiguous. Preserve all colliders in dense scenes.
                query.Hits = query.Count == query.buffer.Length
                    ? Physics.OverlapSphere(position, 7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    : query.buffer;
                if (!ReferenceEquals(query.Hits, query.buffer)) query.Count = query.Hits.Length;
                return query;
            }
            catch { query.Release(); throw; }
        }

        internal void Release()
        {
            if (!active) return;
            active = false; Damaged.Clear(); Array.Clear(buffer, 0, buffer.Length);
            Hits = null; Count = 0; Pool.Push(this);
        }

        internal static void Clear() { Pool.Clear(); }
    }
}
