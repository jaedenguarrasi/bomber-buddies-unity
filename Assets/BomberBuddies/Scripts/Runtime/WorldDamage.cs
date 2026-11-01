using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>A tower's or house's cell, on its collider, so a hit knows what it hit.</summary>
    public class BuildingTag : MonoBehaviour { public int k; }

    /// <summary>
    /// The island taking damage, from the original: blasts dig craters (crater3: a bowl with scorch), towers have hit points
    /// (php) and shed floors from the top as they are hurt, and when a tower gives way the section above the hit tips over about
    /// its hinge and comes down on whatever is under it (fallTip / crumble), leaving rubble. Houses collapse to rubble too.
    /// Demolition pays for every building brought down.
    /// </summary>
    public partial class World3
    {
        public float[] php;                       // tower hit points, 0..255 like the original
        readonly Dictionary<int, float> homeHp = new Dictionary<int, float>();
        readonly HashSet<int> dirty = new HashSet<int>();
        readonly List<Debris> debris = new List<Debris>();
        readonly List<Fall> falls = new List<Fall>();
        class Debris { public Transform t; public Vector3 v; public float life, floor; public Vector3 spin; }
        class Fall { public Transform t; public Vector3 hinge, axis; public float angle, w, length, hw; public int k; public bool hitDone; public Color32 col; }
        public int buildingsDown;

        void InitDamage() { php = new float[N3 * N3]; for (int i = 0; i < php.Length; i++) php[i] = 255f; }

        /// <summary>A blast at a world point: a crater in the ground, damage to any tower or house in reach, rubble broken down.</summary>
        public void Blast(Vector3 at, float dmgRadiusM, float dmg)
        {
            if (php == null) InitDamage();
            // the cell under the blast
            float ox = at.x / (U * Spread) + MID3, oz = at.z / (U * Spread) + MID3;
            // crater3: a smooth bowl with a low raised lip; only in open ground, scaled with the blast
            if (dmgRadiusM >= 3f) Crater(ox, oz, dmgRadiusM / U / Spread * 0.55f, Mathf.Clamp(dmg * 0.02f, 1.5f, 7f), 120f);
            // buildings in reach
            float r = dmgRadiusM / U / Spread + BS;
            int x0 = Mathf.Max(1, Mathf.FloorToInt((ox - r) / BS)), x1 = Mathf.Min(N3 - 2, Mathf.CeilToInt((ox + r) / BS));
            int z0 = Mathf.Max(1, Mathf.FloorToInt((oz - r) / BS)), z1 = Mathf.Min(N3 - 2, Mathf.CeilToInt((oz + r) / BS));
            for (int iz = z0; iz <= z1; iz++) for (int ix = x0; ix <= x1; ix++)
            {
                int k = iz * N3 + ix;
                if (house3[k] >= 20 && house3[k] < 24) { float hw = bhw[k]; float dx = Mathf.Max(0f, Mathf.Abs(ix * BS - ox) - hw), dz = Mathf.Max(0f, Mathf.Abs(iz * BS - oz) - hw); if (Mathf.Sqrt(dx * dx + dz * dz) * U * Spread < dmgRadiusM + 2f) DamageTower(k, dmg, at); }
                else if (house3[k] == 30 || (house3[k] >= 1 && house3[k] <= 4)) { float d = Mathf.Sqrt((ix * BS - ox) * (ix * BS - ox) + (iz * BS - oz) * (iz * BS - oz)) * U * Spread; if (d < dmgRadiusM + 6f) DamageHome(k, dmg * (1f - Mathf.Clamp01(d / (dmgRadiusM + 6f)) * 0.5f), at); }
            }
            FlushDirty();
        }

        /// <summary>A bullet into a tower or house (BuildingTag on the collider): chips at it.</summary>
        public void Hit(int k, float dmg, Vector3 at)
        {
            if (php == null) InitDamage();
            if (house3[k] >= 20 && house3[k] < 24) DamageTower(k, dmg * 0.4f, at);
            else DamageHome(k, dmg * 0.5f, at);
            FlushDirty();
        }

        void Crater(float ox, float oz, float r, float depth, float scorch)
        {
            float R = r * 1.4f;
            int x0 = Mathf.Max(1, Mathf.CeilToInt((ox - R) / BS)), x1 = Mathf.Min(N3 - 2, Mathf.FloorToInt((ox + R) / BS));
            int z0 = Mathf.Max(1, Mathf.CeilToInt((oz - R) / BS)), z1 = Mathf.Min(N3 - 2, Mathf.FloorToInt((oz + R) / BS));
            for (int iz = z0; iz <= z1; iz++) for (int ix = x0; ix <= x1; ix++)
            {
                int k = iz * N3 + ix;
                if (house3[k] != 0 || road3[k] != 0) continue;   // not under a building; roads stay
                if (hm[k] < SEA + 1f) continue;
                float d = Mathf.Sqrt((ix * BS - ox) * (ix * BS - ox) + (iz * BS - oz) * (iz * BS - oz)) / r;
                float cut = d < 1f ? depth * (1f - d * d) : d < 1.4f ? -depth * 0.18f * (1.4f - d) / 0.4f : 0f;   // the bowl, then the lip
                if (cut == 0f) continue;
                hm[k] = Mathf.Max(FLOOR3, hm[k] - cut);
                if (tree3[k] != 0 && d < 0.9f) tree3[k] = 0;   // trees in the bowl go
                burn[k] = (byte)Mathf.Min(255f, burn[k] + scorch * Mathf.Clamp01(1.2f - d));
                MarkDirty(ix, iz);
            }
        }

        void MarkDirty(int ix, int iz)
        {
            // a cell's quad touches the chunk it is in and the one before it on each axis
            for (int dz = -1; dz <= 0; dz++) for (int dx = -1; dx <= 0; dx++)
            {
                int cx = Mathf.Clamp((ix + dx) / CH, 0, N3 / CH - 1), cz = Mathf.Clamp((iz + dz) / CH, 0, N3 / CH - 1);
                dirty.Add(cz * 1000 + cx);
            }
        }

        void FlushDirty()
        {
            foreach (var key in dirty) RebuildChunk(key % 1000, key / 1000);
            dirty.Clear();
        }

        // ---------- towers ----------
        void DamageTower(int k, float dmg, Vector3 at)
        {
            if (bfl[k] <= 0) return;
            float hw = bhw[k]; float tough = 1.1f + (hw - 12f) * 0.035f;
            float before = php[k];
            php[k] = Mathf.Max(0f, php[k] - dmg / (tough * 2.2f));
            int ix = k % N3, iz = k / N3; float x = ix * BS, z = iz * BS, h = hm[k];
            int floors = bfl[k];
            // the hit floor
            float hitY = at.y / U; int hitFloor = Mathf.Clamp(Mathf.FloorToInt((hitY - h) / FLOOR_H), 0, floors - 1);
            // bits fall from where it was hit
            var col = TOWER_COL[(house3[k] - 20) % 4];
            var p = ToWorld(x, h + hitFloor * FLOOR_H + FLOOR_H * 0.5f, z);
            var dir = (at - p); dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            for (int i = 0; i < Mathf.Clamp(Mathf.RoundToInt(dmg / 25f), 2, 8); i++) Shed(p + dir * hw * U * Spread + Vector3.up * Random.Range(-1f, 2f), dir * Random.Range(2f, 6f) + Vector3.up * Random.Range(1f, 5f) + Random.insideUnitSphere * 2f, col, Random.Range(0.4f, 1.1f));
            // every 20 % lost sheds a floor from the top (the top floor is gone); the first time it hurts, say so
            int stepsBefore = Mathf.FloorToInt((255f - before) / 51f), stepsAfter = Mathf.FloorToInt((255f - php[k]) / 51f);
            if (stepsAfter > stepsBefore && php[k] > 0f && floors > 2)
            {
                bfl[k] = Mathf.Max(2, floors - (stepsAfter - stepsBefore));
                var top = ToWorld(x, h + bfl[k] * FLOOR_H, z);
                for (int i = 0; i < 10; i++) Shed(top + new Vector3(Random.Range(-hw, hw) * U * Spread, 1f, Random.Range(-hw, hw) * U * Spread), Random.insideUnitSphere * 4f + Vector3.up * 3f, col, Random.Range(0.6f, 1.6f));
                if (GameState.Hud != null) GameState.Hud.Comms(stepsAfter == 1 ? "The top floor is gone" : "The corner is going");
                Sfx.Play("expl_big", 0.5f, 0.8f, top);
                MarkDirty(ix, iz);
            }
            if (php[k] <= 0f) Topple(k, hitFloor, dir);
        }

        /// <summary>The tower gives way at the hit floor: everything above tips over about its base edge and comes down.</summary>
        void Topple(int k, int atFloor, Vector3 dir)
        {
            int ix = k % N3, iz = k / N3; float x = ix * BS, z = iz * BS, h = hm[k]; float hw = bhw[k] - 1f; int floors = bfl[k];
            var col = TOWER_COL[(house3[k] - 20) % 4];
            int keep = Mathf.Clamp(atFloor, 0, floors - 1); int gone = floors - keep;
            if (gone <= 0) gone = floors;
            // the falling section as its own mesh, hinged on the far edge in the direction it falls
            var fm = new MeshBuilder();
            float len = gone * FLOOR_H;
            fm.Box(-hw, hw, 0f, len, -hw, hw, col, true);
            var glass = MeshBuilder.Rgb(104, 128, 142);
            for (int f = 0; f < gone; f++) { float y0 = f * FLOOR_H + 4.5f; fm.Box(-hw + 2f, hw - 2f, y0, y0 + 5f, -hw - 0.25f, hw + 0.25f, glass); fm.Box(-hw - 0.25f, hw + 0.25f, y0, y0 + 5f, -hw + 2f, hw - 2f, glass); }
            var go = fm.Spawn("Falling tower", transform, lowPoly, false);
            // the hinge: the base edge of the section on the falling side
            var baseC = ToWorld(x, h + keep * FLOOR_H, z);
            var d = dir; d.y = 0f; d.Normalize();
            var hinge = baseC + d * hw * U * Spread;
            go.transform.position = baseC;
            var axis = Vector3.Cross(Vector3.up, d).normalized;
            falls.Add(new Fall { t = go.transform, hinge = hinge, axis = axis, angle = 0f, w = 0.05f, length = len * U, hw = hw * U * Spread, k = k, col = col });
            // what stays: a stump, or rubble
            if (keep >= 1) { bfl[k] = keep; php[k] = 60f; } else { house3[k] = 28; bfl[k] = 0; php[k] = 0f; }
            buildingsDown++;
            GameState.Earn(200 + gone * 15);
            if (GameState.Hud != null) GameState.Hud.Announce(keep >= 1 ? "It is going over" : "The whole tower is going over", 2.5f);
            Sfx.Play("expl_huge", 0.9f, 0.7f, baseC); Sfx.Play("roar_big", 0.3f, 0.5f, baseC);
            MarkDirty(ix, iz);
        }

        // ---------- houses ----------
        void DamageHome(int k, float dmg, Vector3 at)
        {
            float hp; if (!homeHp.TryGetValue(k, out hp)) hp = house3[k] == 30 ? 160f : 90f;
            hp -= dmg; homeHp[k] = hp;
            int ix = k % N3, iz = k / N3;
            var p = ToWorld(ix * BS, hm[k] + 6f, iz * BS);
            for (int i = 0; i < 3; i++) Shed(p + Random.insideUnitSphere * 1.5f, Random.insideUnitSphere * 3f + Vector3.up * 3f, MeshBuilder.Rgb(150, 70, 60), Random.Range(0.3f, 0.7f));
            if (hp <= 0f)
            {
                house3[k] = 28; homes.Remove(k);
                for (int i = 0; i < 14; i++) Shed(p + new Vector3(Random.Range(-8f, 8f) * U, 0f, Random.Range(-8f, 8f) * U), Random.insideUnitSphere * 4f + Vector3.up * 4f, i % 3 == 0 ? MeshBuilder.Rgb(150, 70, 60) : MeshBuilder.Rgb(214, 200, 170), Random.Range(0.4f, 1f));
                buildingsDown++; GameState.Earn(40);
                if (GameState.Hud != null) GameState.Hud.Comms("That one is flat");
                Sfx.Play("expl_big", 0.6f, 0.9f, p);
                MarkDirty(ix, iz);
            }
        }

        /// <summary>What is left of a building (house3 == 28): a low heap of the stuff it was made of.</summary>
        void Rubble(int k, float x, float h, float z)
        {
            float s = 0.6f + Hash(k + 7.7f) * 0.5f;
            var col = MeshBuilder.Rgb(132, 124, 112);
            mb.Box(x - 9f * s, x + 9f * s, h - 1f, h + 2.5f * s, z - 9f * s, z + 9f * s, col, true);
            mb.Box(x - 5f * s, x + 6f * s, h + 2f * s, h + 5f * s, z - 4f * s, z + 5f * s, MeshBuilder.Mul(col, 0.9f), true);
            mb.Box(x + 2f, x + 7f * s, h + 2f, h + 7f * s, z - 7f * s, z - 2f, MeshBuilder.Mul(col, 0.8f), true);
            AddCollider(WM(x, h + 2f * s, z), new Vector3(18f * s * U, 4f * s * U, 18f * s * U), "Rubble");
        }

        // ---------- debris and falling sections ----------
        void Shed(Vector3 at, Vector3 vel, Color32 col, float size)
        {
            if (debris.Count > 90) { var d0 = debris[0]; if (d0.t != null) Destroy(d0.t.gameObject); debris.RemoveAt(0); }
            var m = new MeshBuilder(); float s = size / U;
            m.Box(-s * 0.5f, s * 0.5f, -s * 0.35f, s * 0.35f, -s * 0.4f, s * 0.4f, col, true);
            var go = m.Spawn("Debris", transform, lowPoly, false, false);
            go.transform.position = at; go.transform.rotation = Random.rotation;
            debris.Add(new Debris { t = go.transform, v = vel, life = Random.Range(3f, 5f), floor = GroundMetres(at.x, at.z), spin = Random.insideUnitSphere * 240f });
        }

        void UpdateDamage(float dt)
        {
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i]; if (d.t == null) { debris.RemoveAt(i); continue; }
                d.life -= dt; d.v.y -= 16f * dt;
                var p = d.t.position + d.v * dt;
                if (p.y <= d.floor + 0.1f) { p.y = d.floor + 0.1f; d.v = new Vector3(d.v.x * 0.5f, -d.v.y * 0.25f, d.v.z * 0.5f); d.spin *= 0.5f; if (d.v.sqrMagnitude < 0.3f) d.v = Vector3.zero; }
                d.t.position = p; d.t.Rotate(d.spin * dt);
                if (d.life <= 0f) { Destroy(d.t.gameObject); debris.RemoveAt(i); }
            }
            for (int i = falls.Count - 1; i >= 0; i--)
            {
                var F = falls[i]; if (F.t == null) { falls.RemoveAt(i); continue; }
                // it goes over faster and faster, like something that has lost its footing
                F.w += (0.9f + F.angle * 1.2f) * dt; F.angle += F.w * dt;
                float a = Mathf.Min(F.angle, Mathf.PI * 0.5f);
                F.t.rotation = Quaternion.AngleAxis(a * Mathf.Rad2Deg, F.axis);
                // the hinge edge stays where it was; the section swings about it
                var d = Vector3.Cross(F.axis, Vector3.up).normalized;   // the fall direction
                F.t.position = F.hinge - F.t.rotation * (d * F.hw);
                // as it goes over, anything along it takes the blow
                if (!F.hitDone && a > 1.25f)
                {
                    F.hitDone = true;
                    for (int q = 1; q <= 4; q++)
                    {
                        var tip = F.hinge + d * (F.length * q / 4f) * Mathf.Sin(a) + Vector3.up * (F.length * q / 4f) * Mathf.Cos(a);
                        tip.y = GroundMetres(tip.x, tip.z) + 0.5f;
                        if (Projectiles.Instance != null) Projectiles.Instance.Explode(tip, F.hw * 0.8f, F.hw * 1.6f + 6f, 400f, F.t.gameObject, 2);
                        for (int j = 0; j < 6; j++) Shed(tip + Random.insideUnitSphere * F.hw * 0.5f, Random.insideUnitSphere * 5f + Vector3.up * 5f, F.col, Random.Range(0.5f, 1.4f));
                    }
                    Sfx.Play("expl_huge", 1f, 0.6f, F.hinge);
                    if (GameState.Hud != null) GameState.Hud.Comms("Down");
                }
                if (F.angle > Mathf.PI * 0.5f + 1.2f) { Destroy(F.t.gameObject); falls.RemoveAt(i); }
                else if (a >= Mathf.PI * 0.5f) F.t.position += Vector3.down * 2.5f * dt;   // then it settles into the ground
            }
        }
    }
}
