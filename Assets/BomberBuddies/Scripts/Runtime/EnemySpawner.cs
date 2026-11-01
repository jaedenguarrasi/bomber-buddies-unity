using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>Waves of the enemy army: the mode table says how many riflemen, jeeps, tanks, AA guns and aircraft the n-th wave
    /// brings; they gather round a few rally camps out of sight of the player, and the next wave comes once they are all down
    /// (or, in Horde, every 40 s regardless).</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public MaterialLibrary library;
        public FootController player;
        public int baseCount = 4, perWave = 2, maxAlive = 24;
        public float minDistance = 70f, maxDistance = 150f, breather = 8f;
        public Vector3 villageCenter;
        public float villageRadius = 70f;

        public string modeId = "battle";
        public float seaLevel = 9f;
        readonly List<Health> alive = new List<Health>();
        readonly List<string> queue = new List<string>();     // what is still to come this wave, by type
        readonly List<Vector3> camps = new List<Vector3>();
        float waitT = 3f, hordeT;
        public int Alive { get { return alive.Count; } }
        public int Remaining { get { return alive.Count + queue.Count; } }
        public ModeDefs.Mode Mode { get { ModeDefs.Mode m; return ModeDefs.All.TryGetValue(modeId, out m) ? m : ModeDefs.All["battle"]; } }

        void Update()
        {
            alive.RemoveAll(h => h == null || !h.alive);
            if (queue.Count > 0)
            {
                if (alive.Count < maxAlive) { if (SpawnNext()) queue.RemoveAt(0); }
                GameState.Touch();
                return;
            }
            bool next = alive.Count == 0;
            if (Mode.horde) { hordeT -= Time.deltaTime; if (hordeT <= 0f) next = true; }   // a new wave every 40 s whether you are ready or not
            if (next)
            {
                waitT -= Time.deltaTime;
                if (waitT <= 0f || Mode.horde)
                {
                    StartWave(GameState.Wave + 1);
                    waitT = breather; hordeT = 40f;
                }
            }
        }

        /// <summary>The n-th wave: counts from the mode table, gathered round a few rally camps so there is something to attack.</summary>
        public void StartWave(int n)
        {
            GameState.Wave = n;
            var counts = ModeDefs.WaveCounts(Mode, n);
            camps.Clear();
            int nCamps = 2 + Mathf.Min(2, n / 2);
            for (int i = 0; i < nCamps; i++) camps.Add(LandSpot(GameState.Wave <= 1 ? 90f : 60f, 180f));
            queue.Clear();
            // emplacements first, then the heavy stuff, then the men; aircraft arrive on their own from the edge
            foreach (var t in new[] { "aa", "tank", "jeep", "inf", "eplane", "eheli" })
            {
                int k; if (!counts.TryGetValue(t, out k)) continue;
                for (int i = 0; i < k; i++) queue.Add(t);
            }
            if (GameState.Hud != null) GameState.Hud.Announce("Wave " + n);
            GameState.Touch();
        }

        Vector3 LandSpot(float minR, float maxR)
        {
            var centre = player != null ? player.transform.position : villageCenter;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(minR, maxR);
                var p = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Random.value < 0.5f) p = villageCenter + new Vector3(Random.Range(-villageRadius, villageRadius) * 1.6f, 0f, Random.Range(-villageRadius, villageRadius) * 1.6f);
                if (Vector3.Distance(p, centre) < minR) continue;
                float h = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : 0f;
                if (h <= seaLevel + 1f) continue;
                p.y = h;
                return p;
            }
            return villageCenter;
        }

        bool SpawnNext()
        {
            string type = queue[0];
            if (type == "eplane" || type == "eheli")
            {
                // in from the edge of the island, high up
                float a = Random.Range(0f, Mathf.PI * 2f);
                var centre = player != null ? player.transform.position : villageCenter;
                var at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 420f; at.y = 320f * GalacticMode.U + Mathf.Max(0f, GameState.GroundHeight != null ? GameState.GroundHeight(at.x, at.z) : 0f);
                var e = EnemyPlane.Build(transform, library, at, Mathf.Atan2(centre.x - at.x, centre.z - at.z), 1, type == "eheli");
                alive.Add(e.health);
                return true;
            }
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var camp = camps[Random.Range(0, camps.Count)];
                float spread = type == "aa" ? 30f : 36f;
                var p = camp + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
                float h = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : camp.y;
                if (h <= seaLevel + 1f) continue;
                p.y = h;
                if (player != null && Vector3.Distance(p, player.transform.position) < (GameState.Wave <= 1 ? 60f : 35f)) continue;
                if (type == "inf")
                {
                    NavMeshHit hit;
                    bool onMesh = NavMesh.SamplePosition(p, out hit, 10f, NavMesh.AllAreas);
                    var e = Spawn(onMesh ? hit.position : p, onMesh);
                    alive.Add(e.health);
                }
                else
                {
                    var kind = type == "jeep" ? EnemyVehicle.Kind.Jeep : type == "tank" ? EnemyVehicle.Kind.Tank : EnemyVehicle.Kind.AA;
                    var v = EnemyVehicle.Build(transform, library, kind, p, 1);
                    alive.Add(v.health);
                }
                return true;
            }
            // nowhere dry near the camps: drop it, keep the wave moving
            return true;
        }

        public EnemySoldier Spawn(Vector3 at, bool withAgent = true, int team = 1)
        {
            var go = new GameObject(team == 0 ? "Squad rifleman" : "Enemy");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            int layer = team == 0 ? 8 : 9;
            go.layer = layer;
            var hp = go.AddComponent<Health>(); hp.max = 100f; hp.current = 100f; hp.team = team;
            var body = go.AddComponent<CapsuleCollider>(); body.center = new Vector3(0f, 0.95f, 0f); body.height = 1.5f; body.radius = 0.3f;
            var bz = go.AddComponent<HitZone>(); bz.owner = hp; bz.multiplier = 1f;
            var headGo = new GameObject("HeadZone"); headGo.transform.SetParent(go.transform, false); headGo.transform.localPosition = new Vector3(0f, 1.68f, 0f); headGo.layer = layer;
            var hc = headGo.AddComponent<SphereCollider>(); hc.radius = 0.17f;
            var hz = headGo.AddComponent<HitZone>(); hz.owner = hp; hz.multiplier = 2.4f; hz.head = true;
            var model = SoldierModelBase.Create(go.transform, library, team, layer);
            NavMeshAgent agent = withAgent ? go.AddComponent<NavMeshAgent>() : null;
            var ai = go.AddComponent<EnemySoldier>();
            ai.model = model;
            ai.Init(player, hp);
            if (agent != null && !agent.isOnNavMesh) agent.Warp(at);
            return ai;
        }
    }
}
