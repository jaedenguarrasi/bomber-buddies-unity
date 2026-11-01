using UnityEngine;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>An islander (nearestCiv / ufoAbduct3): wanders the field in plain clothes, runs from saucers, and can be beamed up.
    /// Nobody shoots him on purpose: he is on nobody's side (team 3) and bullets pass him by.</summary>
    public class Civilian : MonoBehaviour
    {
        public Health health; public bool lifted; NavMeshAgent agent; float wanderT; SoldierModelBase model;
        public static int Taken_, Total;
        public static System.Action<Civilian> OnTaken;

        public static Civilian Spawn(Transform parent, MaterialLibrary lib, Vector3 at)
        {
            var go = new GameObject("Islander"); go.transform.SetParent(parent, false); go.transform.position = at; go.layer = 8;
            var hp = go.AddComponent<Health>(); hp.team = 3; hp.max = 60f; hp.current = 60f; hp.aimHeight = 1.2f; hp.invulnerable = true;
            var c = go.AddComponent<Civilian>(); c.health = hp;
            c.model = SoldierModelBase.Create(go.transform, lib, 0, 8);
            foreach (var r in c.model.GetComponentsInChildren<Renderer>()) { foreach (var m in r.materials) if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), new Color(Random.value, Random.value, Random.value), 0.6f)); }
            NavMeshHit hit; if (NavMesh.SamplePosition(at, out hit, 12f, NavMesh.AllAreas)) { go.transform.position = hit.position; c.agent = go.AddComponent<NavMeshAgent>(); c.agent.speed = 2.2f; c.agent.radius = 0.35f; c.agent.height = 1.8f; c.agent.updateRotation = true; }
            Total++;
            return c;
        }

        public void Taken()
        {
            Taken_++;
            if (GameState.Hud != null) GameState.Hud.Comms("An islander was taken  (" + Taken_ + ")");
            Sfx.Play("scream", 0.6f, 1.2f, transform.position);
            if (OnTaken != null) OnTaken(this);
            Destroy(gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (lifted) { if (agent != null) { agent.enabled = false; } if (model != null) model.Animate(0f, 0f, 0f, true, dt); return; }
            if (agent == null || !agent.isOnNavMesh) return;
            wanderT -= dt;
            if (wanderT <= 0f)
            {
                wanderT = Random.Range(3f, 8f);
                var p = transform.position + new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-25f, 25f));
                NavMeshHit nh; if (NavMesh.SamplePosition(p, out nh, 8f, NavMesh.AllAreas)) agent.SetDestination(nh.position);
            }
            // a saucer overhead: run
            foreach (var h in Health.All) if (h != null && h.alive && h.team == 1 && h.isAir && Vector3.Distance(h.transform.position, transform.position) < 40f) { agent.speed = 5.5f; var away = transform.position - h.transform.position; away.y = 0f; NavMeshHit nh; if (NavMesh.SamplePosition(transform.position + away.normalized * 20f, out nh, 8f, NavMesh.AllAreas)) agent.SetDestination(nh.position); break; }
            if (model != null) model.Animate(agent.velocity.magnitude, 0f, 0f, false, dt);
        }
    }
}
