using System;
using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;
        public bool headshot;
        public bool foot;        // fired by a rifle on foot: ship parts (turrets, cores) shrug these off
        public bool explosive;   // blast damage: ship parts use their bomb multiplier
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }

    /// <summary>Hit points for anyone. Fires events the AI, the HUD and the ragdoll listen to.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        public float max = 100f;
        public float current = 100f;
        public int team = 0;                 // 0 player side, 1 enemy
        public bool alive = true;
        public bool invulnerable;
        public bool isAir;                   // flying right now (turrets and AA only shoot these; ground troops ignore them)
        public bool stationary;              // a gun emplacement: does not count for capturing posts
        public float aimHeight = 1.2f;       // where others aim, above the feet
        public float armor = 1f, bombMult = 1f;   // bullets do amount × armor, blasts amount × bombMult
        /// <summary>In a linked game: the host's id for this thing (0 = not tracked).</summary>
        public int netId;

        /// <summary>Everyone alive in the fight, so AI can pick targets by side without knowing what they are.</summary>
        public static readonly List<Health> All = new List<Health>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>The nearest live enemy of a side within range; air = true/false restricts to flying or grounded targets.</summary>
        public static Health Nearest(int myTeam, Vector3 from, float range, bool? air = null)
        {
            Health best = null; float bestD = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var h = All[i];
                if (h == null || !h.alive || h.team == myTeam || !h.gameObject.activeInHierarchy) continue;
                if (air.HasValue && h.isAir != air.Value) continue;
                float d = (h.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = h; }
            }
            return best;
        }

        public static int Count(int team, Vector3 from, float range, bool groundOnly, bool movingOnly)
        {
            int n = 0; float r2 = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var h = All[i];
                if (h == null || !h.alive || h.team != team || !h.gameObject.activeInHierarchy) continue;
                if (groundOnly && h.isAir) continue;
                if (movingOnly && h.stationary) continue;
                if ((h.transform.position - from).sqrMagnitude < r2) n++;
            }
            return n;
        }

        public event Action<DamageInfo> OnDamaged;
        public event Action<DamageInfo> OnDied;

        public float Fraction { get { return max > 0f ? Mathf.Clamp01(current / max) : 0f; } }

        public void ResetFull()
        {
            current = max;
            alive = true;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!alive || invulnerable) return;
            if (NetSync.Instance != null && NetSync.Instance.Intercept(this, info)) return;   // a puppet: the real one is on another phone
            // difficulty: what the enemy does to your side scales with Enemy damage, what you do to them with their toughness
            float diff = 1f;
            if (team == 0 && info.source != null && info.source.layer != 8) diff = Settings.EnemyDmg;
            else if (team != 0 && team != 3 && info.source != null && info.source.layer == 8) diff = 1f / Mathf.Max(0.1f, Settings.EnemyHp);
            current -= (info.explosive ? info.amount * bombMult : info.amount * armor) * diff;
            if (OnDamaged != null) OnDamaged(info);
            if (current <= 0f)
            {
                current = 0f;
                alive = false;
                if (OnDied != null) OnDied(info);
            }
        }

        public void Heal(float amount)
        {
            if (!alive) return;
            current = Mathf.Min(max, current + amount);
        }
    }

    /// <summary>A hit zone on a body part: forwards hits to the owner's Health with a multiplier (the head hurts more).</summary>
    public class HitZone : MonoBehaviour, IDamageable
    {
        public Health owner;
        public float multiplier = 1f;
        public bool head;

        public void TakeDamage(DamageInfo info)
        {
            if (owner == null) return;
            info.amount *= multiplier;
            info.headshot = head;
            owner.TakeDamage(info);
        }
    }
}
