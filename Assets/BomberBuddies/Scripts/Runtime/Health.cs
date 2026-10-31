using System;
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
            current -= info.amount;
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
