using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The dragon (updateDragon3): circles high, dives with fire, and when you are on foot it lands to bite. Numbers from the
    /// original (units): circles 240 out and 90–110 up at 115, dives at 165 to 40 above you breathing fire inside 240, lands
    /// 46 off and bites inside 40 for 60. Built from blocks with flapping wings; "a mountain with wings".
    /// </summary>
    public class Dragon : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public Health health;
        /// <summary>Burning village: a house to go for instead of the player, when the mode gives one.</summary>
        public static System.Func<Vector3, Vector3?> HouseTarget;
        Vector3? house; float houseT;
        float yaw, pitch, bank, stT, biteCd, flap, breathT; string state = "circle"; int orbit = 1; bool landed; Vector3 vel;
        Transform model, wingL, wingR, neck, jaw, tail;
        MaterialLibrary lib;

        public static Dragon Build(Transform parent, MaterialLibrary lib, Vector3 at)
        {
            var go = new GameObject("Dragon"); go.transform.SetParent(parent, false); go.transform.position = at; go.layer = 9;
            var d = go.AddComponent<Dragon>(); d.lib = lib; d.yaw = Random.Range(0f, Mathf.PI * 2f); d.stT = Random.Range(5f, 8f); d.orbit = Random.value < 0.5f ? 1 : -1;
            var hp = go.AddComponent<Health>(); hp.team = 1; hp.max = 5200f * GalacticMode.K * 0.5f; hp.current = hp.max; hp.isAir = true; hp.aimHeight = 2f; hp.armor = 0.55f; d.health = hp;
            var zone = go.AddComponent<HitZone>(); zone.owner = hp; zone.multiplier = 1f;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 1.5f, 0f); col.size = new Vector3(5f, 4f, 9f);
            d.BuildModel();
            hp.OnDied += info =>
            {
                GameState.EnemyKilled(false, 6000);
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position, 3f, 10f, 0f, go, 1);
                if (GameState.Hud != null) GameState.Hud.Announce("The dragon is down!", 4f);
                Sfx.Play("roar_big", 1f, 0.6f, go.transform.position);
                d.health.isAir = false; d.state = "dead";
                foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                Destroy(go, 12f);
            };
            Sfx.Play("roar_big", 1f, 0.85f, at);
            if (GameState.Hud != null) GameState.Hud.Announce("Something huge is coming...", 3f);
            return d;
        }

        void BuildModel() { model = BuildDragonModel(transform, lib, 9, out wingL, out wingR, out neck, out tail); }

        /// <summary>The dragon's body, for the enemy and for the player's own dragon: returns the model root; wings, neck and tail come back by name.</summary>
        public static Transform BuildDragonModel(Transform parent, MaterialLibrary lib, int layer, out Transform wingL, out Transform wingR, out Transform neck, out Transform tail)
        {
            var model = new GameObject("Model").transform; model.SetParent(parent, false);
            wingL = wingR = null; var mat = lib != null ? lib.Get("lowpoly") : null;
            var hide = MeshBuilder.Rgb(96, 40, 36); var belly = MeshBuilder.Rgb(160, 120, 70); var horn = MeshBuilder.Rgb(230, 220, 190); var eye = MeshBuilder.Emissive(MeshBuilder.Rgb(255, 180, 40));
            var mb = new MeshBuilder();
            mb.Frustum(0f, 10f, 0f, 60f, 24f, 20f, 0.7f, 0.8f, 0f, hide);           // body
            mb.DBox(0f, 2f, 0f, 50f, 8f, 16f, belly);
            mb.Spawn("Body", model, mat, false).layer = layer;
            var nb = new MeshBuilder(); nb.Frustum(0f, 0f, 0f, 36f, 10f, 10f, 0.7f, 0.7f, 0f, hide); nb.DBox(0f, 2f, 40f, 24f, 11f, 16f, hide); nb.DBox(0f, -2f, 44f, 20f, 3f, 16f, belly);
            for (int i = 0; i < 2; i++) nb.DBox(i == 0 ? -5f : 5f, 9f, 34f, 2f, 7f, 2f, horn);
            nb.DBox(-6f, 4f, 48f, 2f, 2f, 1f, eye); nb.DBox(6f, 4f, 48f, 2f, 2f, 1f, eye);
            neck = nb.Spawn("Neck", model, mat, false).transform; neck.localPosition = new Vector3(0f, 16f * U, 26f * U); neck.localRotation = Quaternion.Euler(-20f, 0f, 0f); neck.gameObject.layer = layer;
            var tb = new MeshBuilder(); tb.Frustum(0f, 0f, 0f, 70f, 12f, 12f, 0.2f, 0.2f, 0f, hide);
            tail = tb.Spawn("Tail", model, mat, false).transform; tail.localPosition = new Vector3(0f, 10f * U, -28f * U); tail.localRotation = Quaternion.Euler(0f, 180f, 0f); tail.gameObject.layer = layer;
            for (int side = 0; side < 2; side++)
            {
                var wb = new MeshBuilder(); float sx = side == 0 ? -1f : 1f;
                wb.DBox(sx * 30f, 0f, 0f, 60f, 1.2f, 36f, MeshBuilder.Rgb(70, 28, 26));
                wb.DBox(sx * 30f, 0.8f, 10f, 60f, 2.2f, 3f, hide); wb.DBox(sx * 58f, 1f, -8f, 6f, 3f, 40f, hide);
                var w = wb.Spawn(side == 0 ? "WingL" : "WingR", model, mat, false).transform; w.localPosition = new Vector3(sx * 10f * U, 18f * U, 0f); w.gameObject.layer = layer;
                if (side == 0) wingL = w; else wingR = w;
            }
            for (int i = 0; i < 2; i++) { var lb = new MeshBuilder(); lb.DBox(0f, -8f, 0f, 7f, 16f, 8f, hide); lb.DBox(0f, -16f, 3f, 8f, 3f, 12f, belly); var l = lb.Spawn("Leg", model, mat, false).transform; l.localPosition = new Vector3((i == 0 ? -10f : 10f) * U, 4f * U, -8f * U); l.gameObject.layer = layer; }
                    return model;
        }

        Health Target()
        {
            var pm = PlayerManager.Instance;
            if (pm != null && pm.plane != null && pm.plane.Active) return pm.plane.health;
            if (pm != null && pm.drive != null && pm.drive.Active) return pm.drive.health;
            return GameState.Player != null ? GameState.Player.health : null;
        }

        void Update()
        {
            if (state == "dead") { transform.position += Vector3.down * Time.deltaTime * 1.5f; return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var pm = PlayerManager.Instance;
            var t = Target(); bool onFoot = pm != null && pm.OnFoot;
            var p = transform.position;
            float g = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : 0f;
            Vector3 tp = t != null ? t.transform.position : Vector3.zero;
            // Burning village: most of the time it is the houses it wants, not you
            houseT -= dt;
            if (HouseTarget != null && houseT <= 0f) { houseT = Random.Range(10f, 16f); house = Random.value < 0.7f ? HouseTarget(transform.position) : null; }
            if (house.HasValue && HouseTarget != null && state != "land") { tp = house.Value; }
            float dT = Mathf.Max(1f, new Vector2(tp.x - p.x, tp.z - p.z).magnitude);
            stT -= dt; if (biteCd > 0f) biteCd -= dt; breathT -= dt;
            float gx = tp.x, gz = tp.z, want = Mathf.Max(g + 90f * U, tp.y + 110f * U), spd = 115f * U, turn = 1.1f; bool breathing = false;
            if (state == "circle")
            {
                float a = Mathf.Atan2(p.x - tp.x, p.z - tp.z) + orbit * 0.5f;
                gx = tp.x + Mathf.Sin(a) * 240f * U; gz = tp.z + Mathf.Cos(a) * 240f * U;
                if (stT <= 0f)
                {
                    state = (onFoot && Random.value < 0.35f) ? "land" : "dive"; stT = state == "dive" ? 5.5f : 8f; landed = false;
                    Sfx.Play("roar_big", 1f, 0.85f, p);
                    if (GameState.Hud != null) GameState.Hud.Comms(state == "dive" ? "The dragon is diving" : "The dragon is coming down");
                }
            }
            else if (state == "dive")
            {
                spd = 165f * U; want = tp.y + 40f * U; turn = 1.5f;
                if (dT < 240f * U) breathing = true;
                if (stT <= 0f || dT < 34f * U) { state = "circle"; stT = Random.Range(6f, 11f); }
            }
            else
            {
                float a = Mathf.Atan2(p.x - tp.x, p.z - tp.z);
                gx = tp.x + Mathf.Sin(a) * 46f * U; gz = tp.z + Mathf.Cos(a) * 46f * U;
                float gg = GameState.GroundHeight != null ? GameState.GroundHeight(gx, gz) : g;
                want = landed ? g : gg; spd = landed ? 0f : 90f * U; turn = 1.6f;
                if (!landed && Mathf.Abs(p.y - g) < 3f * U + 0.5f && new Vector2(gx - p.x, gz - p.z).magnitude < 30f * U) { landed = true; if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(1f); Sfx.Play("stomp", 0.9f, 0.6f, p); }
                if (landed)
                {
                    float wantYaw = Mathf.Atan2(tp.x - p.x, tp.z - p.z); yaw = Mathf.MoveTowardsAngle(yaw * Mathf.Rad2Deg, wantYaw * Mathf.Rad2Deg, 1.4f * Mathf.Rad2Deg * dt) * Mathf.Deg2Rad;
                    if (t != null && t.alive && dT < 40f * U && Mathf.Abs(tp.y - p.y) < 30f * U && biteCd <= 0f)
                    {
                        biteCd = 1.9f;
                        t.TakeDamage(new DamageInfo { amount = 60f * GalacticMode.K * 0.6f, point = tp, direction = (tp - p).normalized, source = gameObject, foot = true });
                        if (GameState.Player != null && t == GameState.Player.health) GameState.Player.Knock(0.6f, "The dragon has you");
                        Sfx.Play("roar_big", 0.8f, 1.1f, p);
                    }
                    else if (dT < 260f * U) breathing = true;
                    // the tail sweeps the squad
                    foreach (var h in Health.All) if (h != null && h.alive && h.team == 0 && !h.isAir && h != t && Vector3.Distance(h.transform.position, p) < 44f * U && Random.value < dt * 1.5f) h.TakeDamage(new DamageInfo { amount = 70f, point = h.transform.position, direction = Vector3.up, source = gameObject });
                    if (stT <= 0f) { state = "circle"; stT = Random.Range(7f, 12f); landed = false; }
                }
            }
            if (spd > 0f)
            {
                float ga = Mathf.Atan2(gx - p.x, gz - p.z);
                float da = Mathf.Clamp(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, ga * Mathf.Rad2Deg) * Mathf.Deg2Rad, -turn * dt, turn * dt);
                yaw += da; bank = Mathf.Lerp(bank, Mathf.Clamp(da / dt * 0.5f, -0.6f, 0.6f), Mathf.Min(1f, 3f * dt));
                p.x += Mathf.Sin(yaw) * spd * dt; p.z += Mathf.Cos(yaw) * spd * dt;
            }
            else bank *= 0.9f;
            float dy = Mathf.Clamp(want - p.y, -(state == "land" ? 95f : 70f) * U * dt, 70f * U * dt); p.y += dy;
            pitch = Mathf.Lerp(pitch, Mathf.Clamp((want - p.y) / U * 0.012f, -0.45f, 0.45f), Mathf.Min(1f, 2f * dt));
            flap += dt * (landed ? 1.2f : state == "dive" ? 2.2f : 4.2f);
            float edge = World3.WORLD3 * U * World3.Spread * 0.5f - 60f * U;
            if (Mathf.Abs(p.x) > edge || Mathf.Abs(p.z) > edge) yaw = Mathf.Atan2(-p.x, -p.z);
            if (p.y < g + 1f * U && !landed) p.y = g + 1f * U;
            transform.position = p;
            transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank * Mathf.Rad2Deg);
            vel = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * spd;
            health.isAir = !landed;
            // the wings
            float wa = Mathf.Sin(flap) * (landed ? 8f : 32f);
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, wa); if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -wa);
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(flap * 0.5f) * 10f, 0f);
            if (neck != null) neck.localRotation = Quaternion.Euler(breathing ? -5f : -20f, 0f, 0f);
            // the fire: a stream of short-lived burning shells toward the target (breath3)
            if (breathing && t != null && breathT <= 0f)
            {
                breathT = 0.08f;
                var mouth = transform.position + transform.rotation * new Vector3(0f, 18f * U, 62f * U);
                var dir = (tp + Vector3.up * 0.5f - mouth).normalized; dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, 4f), 0f, 0f) * dir;
                if (Projectiles.Instance != null) Projectiles.Instance.FireShell(mouth, dir, 280f * U, 7f * U, 14f * U, 6f * GalacticMode.K, 1, gameObject, true, 70f * U);
                if (Mathf.Repeat(Time.time, 0.5f) < 0.08f) Sfx.Play("breath", 0.6f, 1f, mouth);
            }
        }
    }
}
