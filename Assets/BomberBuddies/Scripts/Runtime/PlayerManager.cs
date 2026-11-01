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
            if (GameState.Hud != null) GameState.Hud.SetFlight(on ? null : plane);
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
            var c = NewCraft(Progress.Craft == "heli" ? PlaneController.Craft.Heli : PlaneController.Craft.Plane, at, yaw, null);
            Fly(c, at, yaw, null, Progress.Craft == "heli" ? 60f : 180f);
        }

        /// <summary>After the hangar: every aircraft of ours picks up the new upgrades.</summary>
        public void ApplyLoadout() { foreach (var c in craft) if (c != null) c.ApplyLoadout(); }

        public IEnumerator Respawn(string why)
        {
            Respawning = true;
            GameState.Deaths++;
            if (GameState.Hud != null) GameState.Hud.Announce(why);
            if (OnFoot && foot != null) { foot.ControlsLocked = true; if (foot.model != null) foot.model.Die(Vector3.forward); }
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
