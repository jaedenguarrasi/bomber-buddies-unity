using System;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// A fixed piece of a carrier that can be destroyed: a cannon, a quick gun, a core, the shield generator. Armour works like the
    /// original: a hit does amount × armour, blasts do amount × bombMult, and a rifle round from someone on foot only does 12 %.
    /// </summary>
    public class ShipPart : MonoBehaviour, IDamageable
    {
        public string kind = "score";
        public float hp = 1000f, maxHp = 1000f, armor = 1f, bombMult = 1f;
        public bool alive = true;
        public Carrier ship;
        public event Action<ShipPart> OnDied;
        public float Fraction { get { return maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f; } }

        public void Setup(string kindName, float hitPoints, float armour, float blastMult, Carrier owner)
        {
            kind = kindName; hp = maxHp = hitPoints; armor = armour; bombMult = blastMult; ship = owner;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!alive) return;
            float amount = info.explosive ? info.amount * bombMult : info.amount * armor;
            if (info.foot && !info.explosive) amount *= 0.12f;
            hp -= amount;
            if (GameState.Hud != null && info.source != null && info.source.layer == 8) GameState.Hud.HitMarker(false);
            if (hp <= 0f)
            {
                hp = 0f; alive = false;
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(transform.position + Vector3.up * 0.5f, 0.5f, kind == "score" ? 5f : 2.5f, 0f, gameObject, 1);
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var r in GetComponentsInChildren<Renderer>()) { var m = r.material; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.09f)); if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black); }
                foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
                var sg = GetComponent<ShipGun>(); if (sg != null) sg.enabled = false;
                GameState.Kills++; GameState.Touch();
                if (OnDied != null) OnDied(this);
            }
        }
    }
}
