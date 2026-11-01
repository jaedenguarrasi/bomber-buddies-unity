using System;
using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The campaign (TUTS / CAMPAIGN in the original): Boot camp, Flight school and Chapter 1 as a list of steps.
    /// Each step says what to do (in the words for the controls you are using), sets up what it needs, and knows when it is done.</summary>
    public class Tutorial : MonoBehaviour
    {
        class Step { public Func<string> say; public Action setup; public Action<float> tick; public Func<bool> done; }
        public static Tutorial Instance { get; private set; }
        readonly List<Step> steps = new List<Step>();
        int at = -1; float t, t0, acc, lookAcc, lastYaw, lastPitch; bool shown;
        Vector3 flag; bool showFlag; readonly List<Health> dummies = new List<Health>();
        MaterialLibrary lib; World3 world; EnemySpawner spawner; string modeId;
        int waveStart; readonly List<Health> targets = new List<Health>(); int phase;

        public static Tutorial Create(Transform parent, MaterialLibrary lib, World3 world, EnemySpawner spawner, string modeId)
        {
            var go = new GameObject("Tutorial"); go.transform.SetParent(parent, false);
            var tu = go.AddComponent<Tutorial>(); Instance = tu; tu.lib = lib; tu.world = world; tu.spawner = spawner; tu.modeId = modeId;
            if (spawner != null && modeId != "ch1") spawner.enabled = false;
            if (modeId == "tutorial") tu.BootCamp(); else if (modeId == "tutflight") tu.FlightSchool(); else if (modeId == "tutspecial") tu.GrappleSchool(); else tu.Chapter1();
            if (GameState.Hud != null) GameState.Hud.AddMarker(() => tu.flag + Vector3.up * 1.5f, () => "Flag", () => new Color(1f, 0.9f, 0.3f), () => tu.showFlag);
            return tu;
        }

        /// <summary>The right words for the controls in hand: a pad, a keyboard, or the touch screen.</summary>
        static string K(string pad, string kb, string touch)
        {
            var ib = InputBridge.Instance;
            if (ib != null && ib.IsTouch) return touch;
            if (UnityEngine.InputSystem.Gamepad.current != null && !string.IsNullOrEmpty(UnityEngine.InputSystem.Gamepad.current.description.interfaceName)) return pad;
            return kb;
        }
        static FootController Foot { get { return GameState.Player; } }
        static PlaneController Plane { get { return PlayerManager.Instance != null ? PlayerManager.Instance.plane : null; } }
        static bool OnFoot { get { return PlayerManager.Instance != null && PlayerManager.Instance.OnFoot && Foot != null; } }
        Vector3 Ahead(float d) { var p = OnFoot ? Foot.transform.position : (Plane != null ? Plane.transform.position : Vector3.zero); var f = OnFoot ? Quaternion.Euler(0f, Foot.transform.eulerAngles.y, 0f) * Vector3.forward : (Plane != null ? Plane.transform.forward : Vector3.forward); f.y = 0f; var q = p + f.normalized * d; q.y = world != null ? world.GroundMetres(q.x, q.z) : q.y; return q; }
        bool Near(float r) { var p = OnFoot ? Foot.transform.position : (Plane != null ? Plane.transform.position : Vector3.zero); return Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(flag.x, 0f, flag.z)) < r; }
        void Dummies(int n, float d, float spread)
        {
            dummies.Clear(); if (spawner == null || !OnFoot) return;
            float a0 = Foot.transform.eulerAngles.y * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float a = a0 + (i - (n - 1) / 2f) * spread; var p = Foot.transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * d; p.y = world.GroundMetres(p.x, p.z);
                var e = spawner.Spawn(p, false); e.kind = "dummy"; e.enabled = false; e.transform.LookAt(Foot.transform.position); dummies.Add(e.health);
            }
        }
        bool DummiesDown() { foreach (var h in dummies) if (h != null && h.alive) return false; return true; }

        void BootCamp()
        {
            steps.Add(new Step { say = () => "Sergeant: On your feet, soldier. Look around: " + K("push the right stick", "move the mouse", "drag a thumb on the right half of the screen") + ".", done = () => lookAcc > 1.2f });
            steps.Add(new Step { say = () => "Walk to the flag: " + K("push the left stick", "W A S D", "the stick on the left of the screen") + ".", setup = () => { flag = Ahead(14f); showFlag = true; }, done = () => Near(3f) });
            steps.Add(new Step { say = () => "Sprint: " + K("hold L3 (press the left stick in) while you move", "hold Shift while you move", "push the stick all the way out") + ". Run to the next flag.", setup = () => { flag = Ahead(22f); showFlag = true; }, done = () => Near(3f) });
            steps.Add(new Step { say = () => "Jump: " + K("A", "Space", "the Jump button") + ".", setup = () => { showFlag = false; acc = 0f; }, tick = dt => { if (OnFoot && !Foot.Grounded) acc = 1f; }, done = () => acc > 0f && OnFoot && Foot.Grounded });
            steps.Add(new Step { say = () => "Crouch: tap " + K("B", "C", "the Crouch button") + ". You are a smaller target and your aim steadies.", done = () => OnFoot && Foot.Crouching });
            steps.Add(new Step { say = () => "Aim down the sight: hold " + K("L2", "the right mouse button", "the Aim button") + ".", setup = () => acc = 0f, tick = dt => { if (OnFoot && Foot.Aiming) acc += dt; }, done = () => acc > 1f });
            steps.Add(new Step { say = () => "Three targets ahead. Put them down: aim and " + K("pull R2", "click", "hold the Fire button") + ". Head shots count double.", setup = () => Dummies(3, 12f, 0.35f), done = DummiesDown });
            steps.Add(new Step { say = () => "Watch the Gun bar at the top left: hold the trigger too long and the barrel runs hot and stops. Ease off and it cools.", setup = () => t0 = t, done = () => t - t0 > 6f });
            steps.Add(new Step { say = () => "Grenade: tap " + K("R1", "G", "the Bomb button") + ". Land one out in front.", setup = () => acc = GameState.Grenades, done = () => GameState.Grenades < acc });
            steps.Add(new Step { say = () => "Hurt? The item wheel: " + K("D-pad down, then pick the med kit", "I, then click the med kit", "the I button, then the med kit") + ". Open it now.", setup = () => acc = 0f, tick = dt => { if (ItemWheel.Instance != null && ItemWheel.Instance.IsOpen) acc = 1f; }, done = () => acc > 0f });
            steps.Add(new Step { say = () => "The map: " + K("Select", "M", "the map button") + " shows the island, your team and the enemy. Open it and close it.", setup = () => acc = 0f, tick = dt => { if (GameState.Hud != null && GameState.Hud.MapOpen) acc = 1f; }, done = () => acc > 0f && GameState.Hud != null && !GameState.Hud.MapOpen });
            steps.Add(new Step { say = () => "Two more. Put them down.", setup = () => Dummies(2, 16f, 0.5f), done = DummiesDown });
            steps.Add(new Step { say = () => "That is the basics. Flight school and Chapter 1 are open in the Campaign tab. Good hunting.", setup = () => { Progress.Campaign = Mathf.Max(Progress.Campaign, 1); t0 = t; }, done = () => t - t0 > 6f });
        }

        void FlightSchool()
        {
            steps.Add(new Step { say = () => "Flight school. Throttle up: " + K("D-pad up and hold", "hold E", "slide the throttle on the right all the way up") + ".", done = () => Plane != null && Plane.thr > 0.9f });
            steps.Add(new Step { say = () => "Level out: ease the stick back to the middle and watch the horizon. Hold it level for two seconds.", setup = () => acc = 0f, tick = dt => { if (Plane != null && Mathf.Abs(Plane.pitch) < 0.14f && Mathf.Abs(Plane.bank) < 0.2f) acc += dt; else acc = 0f; }, done = () => acc > 2f });
            steps.Add(new Step { say = () => "Turn: push the stick left or right and hold it. The wings bank and the nose comes round. Turn all the way round.", setup = () => { acc = 0f; lastYaw = Plane != null ? Plane.yaw : 0f; }, tick = dt => { if (Plane != null) { acc += Mathf.Abs(Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, Plane.yaw * Mathf.Rad2Deg)); lastYaw = Plane.yaw; } }, done = () => acc > 330f });
            steps.Add(new Step { say = () => "Fly to the flag. Small corrections, early: the plane goes where the nose points.", setup = () => { flag = Ahead(500f); flag.y += 60f; showFlag = true; }, done = () => Plane != null && Vector3.Distance(Plane.transform.position, flag) < 60f });
            steps.Add(new Step { say = () => "Guns: line the nose up on the ground and hold " + K("R2", "the left mouse button", "Fire") + ". Short bursts, and watch the Gun bar.", setup = () => { showFlag = false; acc = 0f; }, tick = dt => { if (Plane != null && InputBridge.Instance != null && InputBridge.Instance.Fire) acc += dt; }, done = () => acc > 1.5f });
            steps.Add(new Step { say = () => "Bombs: the ring on the ground shows where your bomb will land. Press " + K("R1", "G or the middle mouse button", "Bomb") + ".", setup = () => acc = Plane != null ? Plane.bombs : 0, done = () => Plane != null && Plane.bombs < acc });
            steps.Add(new Step { say = () => "Boost: " + K("L3 (press the left stick in)", "Shift", "the Boost button") + " for a burst of speed. Watch the Boost bar; it refills.", setup = () => acc = 0f, tick = dt => { if (Plane != null && Plane.Boosting) acc += dt; }, done = () => acc > 1f });
            steps.Add(new Step { say = () => "Free look: hold " + K("L1", "C", "the view button") + " and look around with the stick; the guns follow the reticle.", setup = () => acc = 0f, tick = dt => { if (InputBridge.Instance != null && InputBridge.Instance.LookHeld) acc += dt; }, done = () => acc > 1f });
            steps.Add(new Step { say = () => "Wings earned. Every plane in the hangar flies like this. The helicopter hovers and lifts with the throttle; try it when you are ready.", setup = () => { Progress.Campaign = Mathf.Max(Progress.Campaign, 2); t0 = t; }, done = () => t - t0 > 7f });
        }

        void GrappleSchool()
        {
            steps.Add(new Step { say = () => "Grapple school. You have two lines. Aim high at the tower and hold " + K("L1", "C", "the view button") + ": the left line sticks and you swing. Let go to drop it.", setup = () => acc = 0f, tick = dt => { if (OnFoot && Foot.Swinging && !Foot.Grounded) acc += dt; }, done = () => acc > 1.5f });
            steps.Add(new Step { say = () => "Now " + K("R1", "G", "the Bomb button") + " is the right line. Swing to the flag, dropping each line at the bottom of the swing.", setup = () => { flag = Ahead(60f); showFlag = true; }, done = () => Near(8f) });
            steps.Add(new Step { say = () => "Zip: a quick tap of a line pulls you straight to whatever it hits. Zip up onto a roof.", setup = () => { showFlag = false; Foot.Zipped = false; acc = 0f; }, tick = dt => { if (OnFoot && Foot.Zipped && Foot.transform.position.y - world.GroundMetres(Foot.transform.position.x, Foot.transform.position.z) > 6f) acc = 1f; }, done = () => acc > 0f });
            steps.Add(new Step { say = () => "Jump while you swing and you let go with everything you had. Cover 40 m in one go to the flag.", setup = () => { flag = Ahead(45f); showFlag = true; }, done = () => Near(8f) });
            steps.Add(new Step { say = () => "Fists: up close, " + K("R2", "click", "Fire") + ". Two of them ahead.", setup = () => { showFlag = false; Dummies(2, 10f, 0.4f); }, done = DummiesDown });
            steps.Add(new Step { say = () => "That is the Grapple. He is in the hangar when you want him.", setup = () => { Progress.Campaign = Mathf.Max(Progress.Campaign, 2); t0 = t; }, done = () => t - t0 > 6f });
        }

        void Chapter1()
        {
            steps.Add(new Step { say = () => "Chapter 1. The enemy crossed the river in the night. Hold the crossroads through three waves.", setup = () => { phase = 1; waveStart = GameState.Wave; }, done = () => GameState.Wave >= waveStart + 3 && spawner != null && spawner.Remaining == 0 });
            steps.Add(new Step { say = () => "Good. Now push up the road: two anti-air guns are keeping our helicopter off. Silence both.", setup = () =>
            {
                phase = 2; if (spawner != null) spawner.enabled = false; targets.Clear();
                for (int i = 0; i < 2; i++) { var p = Ahead(160f + i * 90f) + new Vector3((i == 0 ? -1f : 1f) * 35f, 0f, 0f); p.y = world.GroundMetres(p.x, p.z); var v = EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.AA, p, 1); targets.Add(v.health); var hh = v.health; if (GameState.Hud != null) GameState.Hud.AddMarker(() => hh.transform.position + Vector3.up * 3f, () => "AA gun", () => new Color(1f, 0.4f, 0.3f), () => hh != null && hh.alive); for (int j = 0; j < 3; j++) { var q = p + new Vector3(UnityEngine.Random.Range(-10f, 10f), 0f, UnityEngine.Random.Range(-10f, 10f)); q.y = world.GroundMetres(q.x, q.z); spawner.Spawn(q); } }
            }, done = () => { foreach (var h in targets) if (h != null && h.alive) return false; return true; } });
            steps.Add(new Step { say = () => "Both guns down. The helicopter is coming in. Chapter 1 complete.", setup = () => { Progress.Campaign = Mathf.Max(Progress.Campaign, 3); GameState.Earn(2500); t0 = t; var e = EnemyPlane.Build(transform, lib, Foot != null ? Foot.transform.position + new Vector3(300f, 120f, 300f) : Vector3.zero, 0f, 0, true); }, done = () => t - t0 > 5f });
        }

        void Update()
        {
            float dt = Time.deltaTime; t += dt;
            if (MatchEnd.Over) return;
            if (OnFoot) { var rig = PlayerManager.Instance.rig; if (rig != null) { lookAcc += Mathf.Abs(Mathf.DeltaAngle(lastYaw, rig.yaw)) * Mathf.Deg2Rad + Mathf.Abs(rig.pitch - lastPitch) * Mathf.Deg2Rad; lastYaw = rig.yaw; lastPitch = rig.pitch; } }
            if (at < 0) { if (OnFoot || (Plane != null && Plane.Active)) Next(); else return; }
            if (at >= steps.Count) return;
            var s = steps[at];
            if (s.tick != null) s.tick(dt);
            if (GameState.Hud != null && (int)(t * 2f) % 2 == 0 && !shown) { GameState.Hud.SetModeLine("Step " + (at + 1) + " / " + steps.Count); shown = true; }
            if (s.done != null && s.done())
            {
                if (at == steps.Count - 1) { if (MatchEnd.Instance != null) MatchEnd.Instance.Show(true, modeId == "ch1" ? "Chapter 1 complete." : modeId == "tutflight" ? "Wings earned." : modeId == "tutspecial" ? "Grapple school complete." : "Boot camp complete."); at++; return; }
                Sfx.Play("ui_click", 0.6f, 1.3f); Next();
            }
        }

        void Next()
        {
            at++; shown = false; if (at >= steps.Count) return;
            var s = steps[at];
            if (s.setup != null) s.setup();
            if (GameState.Hud != null) { GameState.Hud.Announce(s.say(), 9f); GameState.Hud.Comms(s.say()); }
        }
    }
}
