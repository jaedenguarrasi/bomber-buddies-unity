using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The player across every mode: the soldier on foot, the craft he can fly, getting in and out, bailing out, dying and
    /// coming back. A mode tells it where to put the player (the hangar, the air over the island, a command post).
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public static PlayerManager Instance { get; private set; }
        public MaterialLibrary lib;
        public FootController foot;
        public CameraRig rig;
        public Camera cam;
        public PlaneController plane;            // the craft the player is in (or last left)
        public readonly List<PlaneController> craft = new List<PlaneController>();
        /// <summary>Ground vehicles of ours, and the one being driven.</summary>
        public readonly List<GroundVehicle> rides = new List<GroundVehicle>();
        public GroundVehicle drive;
        public bool OnFoot { get; private set; }
        public bool Respawning { get; private set; }
        public bool over;
        /// <summary>Where the player comes back: set by the mode. Return false to fall back to the air over the island.</summary>
        public System.Func<bool> placeOnRespawn;
        /// <summary>The docking volume test, set by the mode (null when there are no hangars).</summary>
        public System.Func<Vector3, Carrier> hangarAt;
        /// <summary>A deck height under a point (NaN when none), for landing a helicopter on a ship.</summary>
        public System.Func<Vector3, float> cockpitFloor;
        public float respawnDelay = 2.6f;

        public static PlayerManager Create(Transform parent, MaterialLibrary lib, FootController foot, CameraRig rig, Camera cam)
        {
            var go = new GameObject("Player manager");
            go.transform.SetParent(parent, false);
            var pm = go.AddComponent<PlayerManager>();
            pm.lib = lib; pm.foot = foot; pm.rig = rig; pm.cam = cam;
            Instance = pm;
            if (foot != null)
            {
                foot.health.OnDied += info => { if (!pm.Respawning && !pm.over && pm.OnFoot) pm.StartCoroutine(pm.Respawn("You're down")); };
                pm.OnFoot = true;
            }
            return pm;
        }

        public PlaneController NewCraft(PlaneController.Craft kind, Vector3 at, float yawDeg, Carrier docked)
        {
            var c = PlaneController.Build(transform, lib, cam, kind);
            c.Place(at, yawDeg, docked, 0f, false);
            c.health.OnDied += info =>
            {
                if (c == plane && c.Active && !Respawning && !over) StartCoroutine(Respawn("Shot down"));
                else if (!c.Active) { craft.Remove(c); Destroy(c.gameObject, 3f); }
            };
            craft.Add(c);
            return c;
        }

        public int ParkedIn(System.Func<Vector3, bool> inside)
        {
            int n = 0;
            foreach (var c in craft) if (c != null && !c.Active && c.health.alive && inside(c.transform.position)) n++;
            return n;
        }

        public void SetOnFoot(bool on)
        {
            OnFoot = on;
            if (foot != null) foot.gameObject.SetActive(on);
            if (rig != null) rig.enabled = on;
            if (foot != null && foot.rifle != null && foot.rifle.viewModel != null) foot.rifle.viewModel.gameObject.SetActive(on && rig != null && rig.firstPerson);
            if (GameState.Hud != null) { GameState.Hud.drive = on ? null : drive; GameState.Hud.SetFlight(on || drive != null ? null : plane); }
        }

        /// <summary>Take the controls of a craft that is parked, docked or in the air.</summary>
        public void Fly(PlaneController c, Vector3 at, float yawDeg, Carrier docked, float speed)
        {
            plane = c;
            float hpK = c.health.alive ? c.health.Fraction : 1f;
            c.Place(at, yawDeg, docked, speed);
            c.health.current = c.health.max * Mathf.Clamp(hpK, 0.05f, 1f);
            SetOnFoot(false);
        }

        /// <summary>Out of the docked or landed craft onto the ground beside it.</summary>
        public void ExitToFoot()
        {
            if (plane == null || foot == null) return;
            if (plane.docked == null && !plane.Landed) return;
            var S = plane.docked;
            plane.Park();
            var right = Quaternion.Euler(0f, 90f, 0f) * plane.Forward; right.y = 0f;
            var pos = plane.transform.position + right.normalized * 14f * U;
            pos.y = (S != null ? S.L2W(0f, S.FloorY, 0f).y : (GameState.GroundHeight != null ? GameState.GroundHeight(pos.x, pos.z) : pos.y)) + 0.1f;
            SetOnFoot(true);
            foot.Teleport(pos, plane.yaw * Mathf.Rad2Deg);
            foot.ControlsLocked = false;
            if (GameState.Hud != null) GameState.Hud.Comms("On foot");
        }

        /// <summary>Out of the craft in mid-air: the soldier falls, Jump opens the chute, the craft is lost.</summary>
        public void BailOut()
        {
            if (plane == null || foot == null) return;
            var pos = plane.transform.position;
            plane.Park();
            plane.gameObject.SetActive(false);
            SetOnFoot(true);
            foot.Teleport(pos, plane.yaw * Mathf.Rad2Deg);
            foot.parachute = false;
            foot.ControlsLocked = false;
            if (GameState.Hud != null) GameState.Hud.Announce("Bailed out! Jump opens your parachute", 3f);
        }

        /// <summary>Into the nearest parked craft when standing beside one.</summary>
        public bool TryGetIn()
        {
            if (foot == null) return false;
            PlaneController near = null; float best = 18f * U;
            foreach (var c in craft)
            {
                if (c == null || c.Active || !c.health.alive) continue;
                float d = Vector3.Distance(foot.transform.position, c.transform.position);
                if (d < best && Mathf.Abs(foot.transform.position.y - c.transform.position.y) < 20f * U) { best = d; near = c; }
            }
            GroundVehicle ride = null; float bestR = 7f;
            foreach (var r in rides) { if (r == null || r.Active || !r.health.alive) continue; float d = Vector3.Distance(foot.transform.position, r.transform.position); if (d < bestR) { bestR = d; ride = r; } }
            if (ride != null && (near == null || bestR < best)) { Drive(ride); return true; }
            if (near == null) { if (GameState.Hud != null) GameState.Hud.Announce("No vehicle close enough to get in"); return false; }
            Fly(near, near.transform.position, near.yaw * Mathf.Rad2Deg, hangarAt != null ? hangarAt(near.transform.position) : null, 0f);
            if (GameState.Hud != null) GameState.Hud.Comms("In the " + near.CraftName);
            return true;
        }

        /// <summary>A fresh plane in the air over the island, the original's respawnInner for aircraft: y = max(320, ground + 190), facing the centre, speed 180.</summary>
        public void SpawnInAir(Vector3 near)
        {
            float g = World3.Instance != null ? World3.Instance.ObstacleMetres(near.x, near.z) : (GameState.GroundHeight != null ? GameState.GroundHeight(near.x, near.z) : 0f);
            // and nothing tall on the way in: the highest roof within 120 m sets the floor
            if (World3.Instance != null) for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2f; g = Mathf.Max(g, World3.Instance.ObstacleMetres(near.x + Mathf.Cos(a) * 60f, near.z + Mathf.Sin(a) * 60f), World3.Instance.ObstacleMetres(near.x + Mathf.Cos(a) * 120f, near.z + Mathf.Sin(a) * 120f)); }
            var at = new Vector3(near.x, Mathf.Max(320f * U, g + 190f * U), near.z);
            var toCentre = -new Vector3(at.x, 0f, at.z);
            float yaw = toCentre.sqrMagnitude > 1f ? Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg : 0f;
            var kind = Progress.Craft == "heli" ? PlaneController.Craft.Heli : Progress.Craft == "heli2" ? PlaneController.Craft.Heli2 : Progress.Craft == "ac130" ? PlaneController.Craft.AC130 : PlaneController.Craft.Plane;
            var c = NewCraft(kind, at, yaw, null);
            Fly(c, at, yaw, null, c.IsHeli ? 60f : 180f);
            if (GameState.Deaths == 0 && c.st != null) GameState.Lives = c.st.lives;
            StartCoroutine(Grace(c.health, 3f));
        }

        /// <summary>After the hangar: every aircraft of ours picks up the new upgrades, and so does the soldier.</summary>
        public void ApplyLoadout() { foreach (var c in craft) if (c != null) c.ApplyLoadout(); foreach (var r in rides) if (r != null) r.ApplyLoadout(); if (foot != null) foot.ApplyKit(); }

        /// <summary>A few seconds of grace after a spawn: nothing can hurt you while you get your bearings.</summary>
        public IEnumerator Grace(Health h, float seconds)
        {
            if (h == null) yield break;
            h.invulnerable = true;
            yield return new WaitForSeconds(seconds);
            if (h != null) h.invulnerable = false;
        }

        /// <summary>Into the driver's seat of a ground vehicle.</summary>
        public void Drive(GroundVehicle v)
        {
            drive = v; v.TakeControl(true);
            if (plane != null && plane.Active) plane.Park();
            SetOnFoot(false);
            if (GameState.Hud != null) GameState.Hud.Comms("In the " + v.Name);
        }

        /// <summary>Out of the vehicle onto the ground beside it.</summary>
        public void ExitVehicle()
        {
            if (drive == null || foot == null) return;
            var v = drive; drive = null; v.TakeControl(false);
            var right = Quaternion.Euler(0f, v.yaw + 90f, 0f) * Vector3.forward;
            var pos = v.transform.position + right * 3f; pos.y = (GameState.GroundHeight != null ? GameState.GroundHeight(pos.x, pos.z) : pos.y) + 0.1f;
            SetOnFoot(true);
            foot.Teleport(pos, v.camYaw);
            foot.ControlsLocked = false;
            if (GameState.Hud != null) GameState.Hud.Comms("On foot");
        }

        /// <summary>The vehicle you were in is wrecked: you are thrown out, hurt.</summary>
        public void VehicleLost(GroundVehicle v)
        {
            if (drive != v) return;
            ExitVehicle();
            if (foot != null && foot.health != null && foot.health.alive) foot.health.TakeDamage(new DamageInfo { amount = foot.health.max * 0.6f, point = foot.transform.position, direction = Vector3.up, source = v.gameObject });
        }

        /// <summary>A vehicle class: start at the wheel, on the ground at the spawn.</summary>
        public void SpawnDriving(Vector3 near)
        {
            var at = near; at.y = GameState.GroundHeight != null ? GameState.GroundHeight(near.x, near.z) : near.y;
            var toCentre = -new Vector3(at.x, 0f, at.z);
            float yaw = toCentre.sqrMagnitude > 1f ? Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg : 0f;
            var kind = Progress.Craft == "tank" ? GroundVehicle.Kind.Tank : Progress.Craft == "scout" ? GroundVehicle.Kind.Scout : GroundVehicle.Kind.Bike;
            var v = GroundVehicle.Build(transform, lib, cam, kind, at, yaw);
            rides.Add(v);
            if (foot != null) { foot.health.ResetFull(); foot.ApplyKit(); foot.Teleport(at, yaw); }
            Drive(v);
            StartCoroutine(Grace(v.health, 4f));
        }

        /// <summary>A soldier class: start on the ground at the spawn, facing the island's centre.</summary>
        public void SpawnOnFoot(Vector3 near)
        {
            var at = near; at.y = (GameState.GroundHeight != null ? GameState.GroundHeight(near.x, near.z) : near.y) + 0.2f;
            var toCentre = -new Vector3(at.x, 0f, at.z);
            float yaw = toCentre.sqrMagnitude > 1f ? Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg : 0f;
            if (plane != null && plane.Active) plane.Park();
            SetOnFoot(true);
            foot.health.ResetFull();
            foot.ApplyKit();
            foot.Teleport(at, yaw);
            foot.ControlsLocked = false;
            StartCoroutine(Grace(foot.health, 4f));
            if (GameState.Hud != null) GameState.Hud.Comms("On foot");
        }

        public IEnumerator Respawn(string why)
        {
            Respawning = true;
            GameState.Deaths++;
            if (GameState.Hud != null) GameState.Hud.Announce(why);
            if (GameState.Deaths >= GameState.Lives)
            {
                yield return new WaitForSeconds(2f);
                if (MatchEnd.Instance != null) MatchEnd.Instance.Show(false, why + "  No lives left.");
                Respawning = false; yield break;
            }
            if (OnFoot && foot != null) { foot.ControlsLocked = true; if (foot.model != null) foot.model.Die(Vector3.forward); if (Gore.Instance != null) Gore.Instance.Splat(foot.transform.position, 1f); }
            yield return new WaitForSeconds(respawnDelay);
            if (over) yield break;
            if (foot != null)
            {
                var old = foot.model;
                foot.model = SoldierModelBase.Create(foot.transform, lib, 0, 8);
                if (old != null) Destroy(old.gameObject);
                foot.health.ResetFull();
                if (foot.rifle != null) foot.rifle.ammo = foot.rifle.magSize;
                foot.ControlsLocked = false;
            }
            if (GameState.Hud != null) GameState.Hud.Comms("Back in the fight");
            if (over) yield break;
            bool placed = placeOnRespawn != null && placeOnRespawn();
            if (!placed) SpawnInAir(new Vector3(Random.Range(-60f, 60f), 0f, -300f));
            Respawning = false;
        }

        void Update()
        {
            craft.RemoveAll(c => c == null);
            if (over) return;
            if (OnFoot && foot != null)
            {
                if (InputBridge.Instance != null && InputBridge.Instance.InteractPressed) TryGetIn();
                // into the sea
                var fp = foot.transform.position;
                float sea = World3.SEA * World3.U;
                if (fp.y < sea + 0.3f && GameState.GroundHeight != null && GameState.GroundHeight(fp.x, fp.z) < sea + 0.3f && !Respawning && foot.health.alive)
                    foot.health.TakeDamage(new DamageInfo { amount = 9999f, point = fp, direction = Vector3.down, source = gameObject });
            }
        }
    }
}
