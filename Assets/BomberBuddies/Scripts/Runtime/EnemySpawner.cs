using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>Waves of infantry: each wave a few more men, dropped on the navmesh out of sight of the player, the next wave once
    /// they are all down (with a short breather).</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public MaterialLibrary library;
        public FootController player;
        public int baseCount = 4, perWave = 2, maxAlive = 18;
        public float minDistance = 70f, maxDistance = 150f, breather = 8f;
        public Vector3 villageCenter;
        public float villageRadius = 70f;

        readonly List<EnemySoldier> alive = new List<EnemySoldier>();
        float waitT = 3f;
        int toSpawn;
        public int Alive { get { return alive.Count; } }
        public int Remaining { get { return alive.Count + toSpawn; } }

        void Update()
        {
            alive.RemoveAll(e => e == null || e.health == null || !e.health.alive);
            if (toSpawn > 0)
            {
                if (alive.Count < maxAlive) { if (SpawnOne()) toSpawn--; }
                GameState.Touch();
                return;
            }
            if (alive.Count == 0)
            {
                waitT -= Time.deltaTime;
                if (waitT <= 0f)
                {
                    GameState.Wave++;
                    toSpawn = baseCount + perWave * (GameState.Wave - 1);
                    waitT = breather;
                    if (GameState.Hud != null) GameState.Hud.Announce("Wave " + GameState.Wave);
                    GameState.Touch();
                }
            }
        }

        bool SpawnOne()
        {
            if (player == null) return false;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(minDistance, maxDistance);
                var p = player.transform.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                // most of them come in from the village side so the fight gathers round the square
                if (Random.value < 0.6f) p = villageCenter + new Vector3(Random.Range(-villageRadius, villageRadius), 0f, Random.Range(-villageRadius, villageRadius));
                if (Vector3.Distance(p, player.transform.position) < (GameState.Wave <= 1 ? 60f : 35f)) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(p, out hit, 10f, NavMesh.AllAreas)) continue;
                var e = Spawn(hit.position);
                alive.Add(e);
                return true;
            }
            return false;
        }

        public EnemySoldier Spawn(Vector3 at)
        {
            var go = new GameObject("Enemy");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.layer = 9;
            var hp = go.AddComponent<Health>(); hp.max = 100f; hp.current = 100f; hp.team = 1;
            var body = go.AddComponent<CapsuleCollider>(); body.center = new Vector3(0f, 0.95f, 0f); body.height = 1.5f; body.radius = 0.3f;
            var bz = go.AddComponent<HitZone>(); bz.owner = hp; bz.multiplier = 1f;
            var headGo = new GameObject("HeadZone"); headGo.transform.SetParent(go.transform, false); headGo.transform.localPosition = new Vector3(0f, 1.68f, 0f); headGo.layer = 9;
            var hc = headGo.AddComponent<SphereCollider>(); hc.radius = 0.17f;
            var hz = headGo.AddComponent<HitZone>(); hz.owner = hp; hz.multiplier = 2.4f; hz.head = true;
            var model = SoldierModelBase.Create(go.transform, library, 1, 9);
            var agent = go.AddComponent<NavMeshAgent>();
            var ai = go.AddComponent<EnemySoldier>();
            ai.model = model;
            ai.Init(player, hp);
            if (!agent.isOnNavMesh) agent.Warp(at);
            return ai;
        }
    }
}
