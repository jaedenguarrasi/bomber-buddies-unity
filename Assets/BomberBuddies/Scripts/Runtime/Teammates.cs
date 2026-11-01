using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>The team hired in the hangar (MATES): a rifle squad, scout car, tank, gunship, medic and wingman that join every
    /// flight, fight on their own, and come back on the next flight (the player's next spawn) if they were lost.</summary>
    public class Teammates : MonoBehaviour
    {
        public static Teammates Instance { get; private set; }
        MaterialLibrary lib; EnemySpawner spawner; float seaLevel;
        readonly Dictionary<string, List<Health>> fielded = new Dictionary<string, List<Health>>();

        public static Teammates Create(Transform parent, MaterialLibrary lib, EnemySpawner spawner, float seaLevel)
        {
            var go = new GameObject("Teammates"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Teammates>(); t.lib = lib; t.spawner = spawner; t.seaLevel = seaLevel; Instance = t;
            return t;
        }

        /// <summary>Top the team up to what was hired, around a point on the ground (the player's spawn, or wherever they are).</summary>
        public void Deploy(Vector3 near)
        {
            foreach (var m in Progress.Mates)
            {
                int want = Progress.MatesOf(m.id) * m.n;
                List<Health> list; if (!fielded.TryGetValue(m.id, out list)) { list = new List<Health>(); fielded[m.id] = list; }
                list.RemoveAll(h => h == null || !h.alive);
                int arrived = 0;
                for (int i = list.Count; i < want; i++) { var h = SpawnOne(m.id, near); if (h != null) { list.Add(h); arrived++; } }
                if (arrived > 0 && GameState.Hud != null) GameState.Hud.Comms(m.name + (arrived > 1 ? " ×" + arrived : "") + " with you");
            }
        }

        Vector3 Ground(Vector3 near, float minR, float maxR)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(minR, maxR);
                var p = near + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : 0f;
                if (h <= seaLevel + 1f) continue;
                if (World3.Instance != null && World3.Instance.ObstacleMetres(p.x, p.z) > h + 0.5f) continue;   // not on a roof
                p.y = h; return p;
            }
            return new Vector3(near.x, GameState.GroundHeight != null ? GameState.GroundHeight(near.x, near.z) : 0f, near.z);
        }

        /// <summary>The commander buys one: a squad member, jeep, tank or helicopter near the spot he tapped.</summary>
        public Health SpawnMate(string id, Vector3 near) { return SpawnOne(id, near); }

        Health SpawnOne(string id, Vector3 near)
        {
            switch (id)
            {
                case "squad":
                case "medic":
                    {
                        var p = Ground(near, 8f, 30f);
                        NavMeshHit hit = default(NavMeshHit);
                        bool onMesh = NavMesh.SamplePosition(p, out hit, 10f, NavMesh.AllAreas);
                        var e = spawner.Spawn(onMesh ? hit.position : p, onMesh, 0);
                        e.kind = id == "medic" ? "medic" : "inf";
                        e.health.max = 120f; e.health.current = 120f;
                        return e.health;
                    }
                case "car": return EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Jeep, Ground(near, 15f, 40f), 0).health;
                case "tank": return EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Tank, Ground(near, 20f, 45f), 0).health;
                case "heli":
                case "wing":
                    {
                        float a = Random.Range(0f, Mathf.PI * 2f);
                        var at = near + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 120f;
                        at.y = Mathf.Max(near.y, GameState.GroundHeight != null ? GameState.GroundHeight(at.x, at.z) : 0f) + (id == "heli" ? 45f : 70f);
                        return EnemyPlane.Build(transform, lib, at, Mathf.Atan2(near.x - at.x, near.z - at.z), 0, id == "heli").health;
                    }
            }
            return null;
        }
    }
}
