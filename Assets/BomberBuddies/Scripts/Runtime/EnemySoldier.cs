using UnityEngine;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>Enemy infantry: wanders until it sees you, then closes to rifle range, stops, shoots in bursts, and shifts position
    /// between bursts. Hearing a shot or taking a hit puts it on alert.</summary>
    public class EnemySoldier : MonoBehaviour
    {
        public float sightRange = 75f, fieldOfView = 150f, attackRange = 26f, loseAfter = 6f;
        public float damage = 7f, burstGap = 0.12f, pauseBetweenBursts = 2.2f, spreadDeg = 6.5f, reactionTime = 1.6f;
        public SoldierModelBase model;
        public Health health;
        /// <summary>A ship's guard: stands his post (no navmesh needed) and shoots whoever comes through the door.</summary>
        public bool guard;

        NavMeshAgent agent;
        FootController player;
        enum State { Wander, Chase, Attack, Dead }
        State state = State.Wander;
        float wanderT, lastSeen = -99f, burstT, burstLeft, pauseT, shiftT, lookYaw, deadT;
        Vector3 lastKnown, eye = new Vector3(0f, 1.55f, 0f), muzzleFallback = new Vector3(0.2f, 1.35f, 0.4f);

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) { guard = true; gameObject.layer = 9; lookYaw = transform.eulerAngles.y; return; }
            agent.speed = 3.6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 14f;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.updateRotation = false;
            agent.stoppingDistance = 1.5f;
            gameObject.layer = 9;
            lookYaw = transform.eulerAngles.y;
        }

        public void Init(FootController target, Health hp)
        {
            player = target;
            health = hp;
            health.OnDied += OnDied;
            health.OnDamaged += OnDamaged;
            wanderT = Random.Range(0.5f, 3f);
        }

        float noticedAt = -99f;

        void OnDamaged(DamageInfo info)
        {
            if (state == State.Dead) return;
            if (player != null) { lastKnown = player.transform.position; lastSeen = Time.time; if (state == State.Wander) state = State.Chase; }
        }

        void OnDied(DamageInfo info)
        {
            state = State.Dead;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (agent != null && agent.enabled) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (model != null) model.Die(info.direction);
            GameState.EnemyKilled(info.headshot);
            deadT = 0f;
        }

        public void SetFacing(float yawDeg) { lookYaw = yawDeg; transform.rotation = Quaternion.Euler(0f, yawDeg, 0f); }

        public void Alert(Vector3 at)
        {
            if (state == State.Dead) return;
            lastKnown = at; lastSeen = Time.time;
            if (state == State.Wander) state = State.Chase;
        }

        bool CanSee(out Vector3 targetPoint)
        {
            targetPoint = Vector3.zero;
            if (player == null || player.health == null || !player.health.alive) return false;
            var headPos = transform.position + eye;
            targetPoint = player.transform.position + Vector3.up * (player.EyeHeight - 0.35f);
            var to = targetPoint - headPos;
            float d = to.magnitude;
            if (d > sightRange) return false;
            if (state == State.Wander)
            {
                float ang = Vector3.Angle(new Vector3(to.x, 0f, to.z), Quaternion.Euler(0f, lookYaw, 0f) * Vector3.forward);
                if (ang > fieldOfView * 0.5f && d > 7f) return false;
            }
            RaycastHit hit;
            if (Physics.Raycast(headPos, to / d, out hit, d, ~(1 << 9), QueryTriggerInteraction.Ignore))
            {
                return hit.collider.gameObject.layer == 8 || hit.collider.GetComponentInParent<FootController>() != null;
            }
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (state == State.Dead)
            {
                deadT += dt;
                if (model != null) model.Animate(0f, 0f, 0f, false, dt);
                if (deadT > 12f) { transform.position += Vector3.down * dt * 0.4f; if (deadT > 16f) Destroy(gameObject); }
                return;
            }
            if (guard || agent == null) { GuardUpdate(dt); return; }
            if (!agent.isOnNavMesh) { if (model != null) model.Animate(0f, 0f, 0f, false, dt); return; }

            Vector3 target;
            bool seen = CanSee(out target);
            if (seen) { if (Time.time - lastSeen > 3f) noticedAt = Time.time; lastKnown = player.transform.position; lastSeen = Time.time; }
            float dist = player != null ? Vector3.Distance(transform.position, player.transform.position) : 999f;

            switch (state)
            {
                case State.Wander:
                    if (seen) { state = State.Chase; break; }
                    wanderT -= dt;
                    if (wanderT <= 0f || (!agent.pathPending && agent.remainingDistance < 1.5f && wanderT < 2f))
                    {
                        wanderT = Random.Range(4f, 9f);
                        var p = transform.position + new Vector3(Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
                        NavMeshHit nh;
                        if (NavMesh.SamplePosition(p, out nh, 6f, NavMesh.AllAreas)) agent.SetDestination(nh.position);
                    }
                    agent.speed = 2.0f;
                    break;
                case State.Chase:
                    agent.speed = 4.2f;
                    if (seen && dist < attackRange) { state = State.Attack; agent.ResetPath(); pauseT = Random.Range(0.2f, 0.6f); shiftT = Random.Range(2.5f, 4.5f); break; }
                    if (Time.time - lastSeen > loseAfter && !agent.pathPending && agent.remainingDistance < 2f) { state = State.Wander; wanderT = 1f; break; }
                    agent.SetDestination(lastKnown);
                    break;
                case State.Attack:
                    if (!seen && Time.time - lastSeen > 1.5f) { state = State.Chase; break; }
                    if (dist > attackRange * 1.3f) { state = State.Chase; break; }
                    shiftT -= dt;
                    if (shiftT <= 0f && !agent.hasPath)
                    {
                        shiftT = Random.Range(2.5f, 5f);
                        var side = Vector3.Cross(Vector3.up, (lastKnown - transform.position).normalized) * (Random.value < 0.5f ? 1f : -1f);
                        var p = transform.position + side * Random.Range(4f, 8f) + (lastKnown - transform.position).normalized * Random.Range(-3f, 3f);
                        NavMeshHit nh;
                        if (NavMesh.SamplePosition(p, out nh, 4f, NavMesh.AllAreas)) agent.SetDestination(nh.position);
                    }
                    if (agent.hasPath && !agent.pathPending && agent.remainingDistance < 0.8f) agent.ResetPath();
                    agent.speed = 3.2f;
                    if (seen && Time.time - noticedAt > reactionTime) Fire(target, dt);
                    break;
            }

            // turning: face the way we walk, or the target when we have one
            Vector3 face = state == State.Attack || (state == State.Chase && seen) ? (lastKnown - transform.position) : (agent.velocity.sqrMagnitude > 0.2f ? agent.velocity : Vector3.zero);
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                float wantYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                lookYaw = Mathf.MoveTowardsAngle(lookYaw, wantYaw, 420f * dt);
            }
            transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
            if (model != null)
            {
                float aimPitch = 0f;
                if (state == State.Attack && player != null) { var to = target - (transform.position + eye); aimPitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg; }
                model.Animate(agent.velocity.magnitude, aimPitch, 0f, false, dt);
            }
        }

        void GuardUpdate(float dt)
        {
            Vector3 target;
            bool seen = CanSee(out target);
            if (seen) { if (Time.time - lastSeen > 3f) noticedAt = Time.time; lastKnown = player.transform.position; lastSeen = Time.time; state = State.Attack; }
            else if (Time.time - lastSeen > loseAfter) state = State.Wander;
            if (state == State.Attack && seen && Time.time - noticedAt > reactionTime) Fire(target, dt);
            Vector3 face = state == State.Attack ? lastKnown - transform.position : Vector3.zero;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) lookYaw = Mathf.MoveTowardsAngle(lookYaw, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 420f * dt);
            transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
            if (model != null)
            {
                float aimPitch = 0f;
                if (state == State.Attack) { var to = target - (transform.position + eye); aimPitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg; }
                model.Animate(0f, aimPitch, 0f, false, dt);
            }
        }

        void Fire(Vector3 target, float dt)
        {
            if (pauseT > 0f) { pauseT -= dt; if (pauseT <= 0f) burstLeft = Random.Range(3, 6); return; }
            burstT -= dt;
            if (burstT > 0f) return;
            if (burstLeft <= 0) { pauseT = pauseBetweenBursts * Random.Range(0.7f, 1.4f); return; }
            burstLeft--;
            burstT = burstGap;
            var from = model != null && model.muzzle != null ? model.muzzle.position : transform.TransformPoint(muzzleFallback);
            float spread = spreadDeg * (!guard && agent != null && agent.enabled && agent.velocity.magnitude > 0.5f ? 1.6f : 1f) * Mathf.Lerp(0.6f, 1.4f, Mathf.InverseLerp(5f, attackRange, Vector3.Distance(from, target)));
            var dir = (target - from).normalized;
            dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, spread), 0f, 0f) * dir;
            var end = from + dir * 200f;
            RaycastHit hit;
            if (Physics.Raycast(from, dir, out hit, 200f, ~(1 << 9), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var d = hit.collider.GetComponent<IDamageable>(); if (d == null) d = hit.collider.GetComponentInParent<IDamageable>();
                if (d != null) d.TakeDamage(new DamageInfo { amount = damage, point = hit.point, direction = dir, source = gameObject, foot = true });
                if (Gunfire.Instance != null) Gunfire.Instance.Impact(hit.point, hit.normal, d != null);
            }
            if (Gunfire.Instance != null)
            {
                Gunfire.Instance.TracerLine(from, end, new Color(1f, 0.55f, 0.3f), 0.025f, 0.07f);
                Gunfire.Instance.MuzzleFlash(from, dir, 0.8f);
            }
        }
    }
}
