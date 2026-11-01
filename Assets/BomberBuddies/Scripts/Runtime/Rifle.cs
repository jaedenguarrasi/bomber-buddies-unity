using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The player's rifle: hitscan shots from the camera, spread that tightens when aiming, recoil, a magazine and a reload.
    /// In first person it carries its own view model under the camera; in third person the figure's rifle is the one you see.</summary>
    public class Rifle : MonoBehaviour
    {
        public FootController owner;
        public CameraRig rig;
        public float damage = 26f, headMultiplier = 2.2f, roundsPerMinute = 620f, spreadHip = 1.7f, spreadAim = 0.3f, range = 320f;
        public int magSize = 30, ammo = 30, pellets = 1;
        public bool beam; public string gunId = "rifle";
        public float reloadTime = 2.1f;
        public bool Aiming { get; private set; }
        public bool Reloading { get { return reloadT > 0f; } }
        public float ReloadFraction { get { return reloadT > 0f ? 1f - reloadT / reloadTime : 0f; } }
        public Transform viewModel, viewMuzzle;

        float cooldown, reloadT, kick, swayX, swayY, bobT;
        Vector3 vmRest = new Vector3(0.26f, -0.22f, 0.44f), vmAim = new Vector3(0f, -0.15f, 0.34f);

        public void BuildViewModel(MaterialLibrary lib)
        {
            // the original's service rifle: a black boxy gun held in tan hands with olive sleeves
            var metal = lib.Instance("gunmetal"); metal.SetColor("_BaseColor", new Color(0.09f, 0.09f, 0.1f)); metal.SetFloat("_Metallic", 0.3f); metal.SetFloat("_Smoothness", 0.45f);
            var wood = lib.Instance("dark"); wood.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.11f));
            var glove = lib.Instance("skin"); glove.SetColor("_BaseColor", new Color(214f / 255f, 170f / 255f, 130f / 255f));
            var sleeve = lib.Instance("uniform"); sleeve.SetColor("_BaseColor", new Color(76f / 255f, 91f / 255f, 46f / 255f));
            viewModel = new GameObject("RifleView").transform;
            viewModel.SetParent(rig.cam.transform, false);
            viewModel.localPosition = vmRest;
            Part(PrimitiveType.Cube, "Receiver", new Vector3(0f, 0f, 0.1f), new Vector3(0.05f, 0.09f, 0.5f), metal);
            Part(PrimitiveType.Cube, "Stock", new Vector3(0f, -0.02f, -0.25f), new Vector3(0.05f, 0.1f, 0.22f), wood);
            Part(PrimitiveType.Cube, "Handguard", new Vector3(0f, -0.005f, 0.42f), new Vector3(0.055f, 0.07f, 0.2f), wood);
            Part(PrimitiveType.Cube, "Magazine", new Vector3(0f, -0.1f, 0.12f), new Vector3(0.04f, 0.14f, 0.07f), metal);
            var barrel = Part(PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.02f, 0.68f), new Vector3(0.025f, 0.17f, 0.025f), metal);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cube, "RearSight", new Vector3(0f, 0.075f, -0.05f), new Vector3(0.04f, 0.035f, 0.03f), metal);
            Part(PrimitiveType.Cube, "FrontSight", new Vector3(0f, 0.08f, 0.6f), new Vector3(0.012f, 0.045f, 0.012f), metal);
            // the hands and sleeves that hold it
            Part(PrimitiveType.Sphere, "HandR", new Vector3(0.03f, -0.07f, -0.02f), new Vector3(0.09f, 0.08f, 0.1f), glove);
            Part(PrimitiveType.Capsule, "ForeR", new Vector3(0.12f, -0.16f, -0.16f), new Vector3(0.1f, 0.16f, 0.1f), sleeve).transform.localRotation = Quaternion.Euler(-45f, 30f, 0f);
            Part(PrimitiveType.Sphere, "HandL", new Vector3(-0.01f, -0.07f, 0.4f), new Vector3(0.09f, 0.08f, 0.11f), glove);
            Part(PrimitiveType.Capsule, "ForeL", new Vector3(-0.1f, -0.17f, 0.25f), new Vector3(0.1f, 0.17f, 0.1f), sleeve).transform.localRotation = Quaternion.Euler(-60f, -35f, 0f);
            viewMuzzle = new GameObject("Muzzle").transform; viewMuzzle.SetParent(viewModel, false); viewMuzzle.localPosition = new Vector3(0f, 0.02f, 0.86f);
            foreach (var r in viewModel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        GameObject Part(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(viewModel, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public void Tick(bool fire, bool aim, bool reloadPressed, float moveSpeed, bool sprinting, bool firstPerson)
        {
            float dt = Time.deltaTime;
            Aiming = aim && !Reloading && !sprinting;
            if (cooldown > 0f) cooldown -= dt;
            if (reloadT > 0f) { reloadT -= dt; if (reloadT <= 0f) { reloadT = 0f; ammo = magSize; } }
            if (reloadPressed && ammo < magSize && !Reloading) reloadT = reloadTime;
            if (fire && cooldown <= 0f && !Reloading && !sprinting)
            {
                if (ammo <= 0) reloadT = reloadTime;
                else Shoot();
            }
            // the view model: hangs at rest, comes up to the eye when aiming, bobs with the stride, swings a little as you turn
            if (viewModel != null)
            {
                viewModel.gameObject.SetActive(firstPerson);
                if (firstPerson)
                {
                    bobT += dt * Mathf.Clamp(moveSpeed, 0f, 8f) * 1.3f;
                    var look = InputBridge.Instance != null ? InputBridge.Instance.Look : Vector2.zero;
                    swayX = Mathf.Lerp(swayX, Mathf.Clamp(-look.x * 0.004f, -0.03f, 0.03f), Mathf.Min(1f, 10f * dt));
                    swayY = Mathf.Lerp(swayY, Mathf.Clamp(look.y * 0.004f, -0.03f, 0.03f), Mathf.Min(1f, 10f * dt));
                    float bob = Mathf.Clamp01(moveSpeed / 4f) * (Aiming ? 0.3f : 1f);
                    var target = Aiming ? vmAim : vmRest;
                    if (sprinting) target = vmRest + new Vector3(-0.08f, -0.12f, -0.1f);
                    var offset = new Vector3(Mathf.Sin(bobT) * 0.012f * bob + swayX, Mathf.Abs(Mathf.Cos(bobT)) * 0.014f * bob + swayY, -kick * 0.08f);
                    viewModel.localPosition = Vector3.Lerp(viewModel.localPosition, target + offset, Mathf.Min(1f, 12f * dt));
                    var tilt = Quaternion.Euler(-kick * 9f + (Reloading ? 35f * Mathf.Sin(ReloadFraction * Mathf.PI) : 0f), sprinting ? -35f : 0f, sprinting ? 12f : 0f);
                    viewModel.localRotation = Quaternion.Slerp(viewModel.localRotation, tilt, Mathf.Min(1f, 12f * dt));
                }
            }
            kick = Mathf.Max(0f, kick - dt * 7f);
        }

        /// <summary>One ray down the barrel: what it hits takes the damage. Returns where it ended.</summary>
        Vector3 Ray(Vector3 origin, Vector3 dir)
        {
            var end = origin + dir * range;
            RaycastHit hit;
            bool flesh = false;
            if (Physics.Raycast(origin, dir, out hit, range, ~(1 << 8), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var d = hit.collider.GetComponent<IDamageable>(); if (d == null) d = hit.collider.GetComponentInParent<IDamageable>();
                if (d != null)
                {
                    flesh = true;
                    d.TakeDamage(new DamageInfo { amount = damage, point = hit.point, direction = dir, source = owner != null ? owner.gameObject : gameObject, foot = true });
                    if (GameState.Hud != null) GameState.Hud.HitMarker(hit.collider.GetComponent<HitZone>() != null && hit.collider.GetComponent<HitZone>().head);
                }
                if (Gunfire.Instance != null) Gunfire.Instance.Impact(hit.point, hit.normal, flesh);
            }
            return end;
        }

        /// <summary>Fit a gun from the hangar table: damage, rate, reach, magazine, pellets, beam.</summary>
        public void Fit(Progress.Gun g, Progress.Stats st)
        {
            damage = (st != null ? st.dmg : g.dmg) * GalacticMode.K;
            roundsPerMinute = 60f / (st != null ? st.fireInt : g.interval);
            range = g.range * GalacticMode.U;
            spreadHip = g.spread; spreadAim = g.pellets > 1 ? g.spread : Mathf.Min(0.3f, g.spread * 0.25f);
            magSize = g.mag; ammo = magSize; pellets = g.pellets; beam = g.beam; gunId = g.id;
            reloadTime = g.mag <= 2 ? 2.6f : g.mag <= 8 ? 2.4f : g.mag >= 100 ? 4.5f : 2.1f;
        }

        void Shoot()
        {
            cooldown = 60f / roundsPerMinute;
            ammo--;
            kick = 1f;
            float spread = Aiming ? spreadAim : spreadHip * (owner != null && owner.MoveSpeed > 1f ? 1.4f : 1f);
            if (pellets > 1) spread = spreadHip;   // a shotgun's spread is the gun's, aimed or not
            var origin = rig.AimOrigin;
            Vector3 dir = rig.AimDirection, end = origin;
            for (int pellet = 0; pellet < pellets; pellet++)
            {
                dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * rig.AimDirection;
                dir = Quaternion.AngleAxis(Random.Range(0f, 360f), rig.AimDirection) * dir;   // spread spins round the aim line so it is round, not square
                end = Ray(origin, dir);
            }
            var muzzle = rig.firstPerson && viewMuzzle != null ? viewMuzzle : (owner != null && owner.model != null && owner.model.muzzle != null ? owner.model.muzzle : null);
            var from = muzzle != null ? muzzle.position : origin;
            if (Gunfire.Instance != null)
            {
                Gunfire.Instance.TracerLine(from, end, beam ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.9f, 0.6f), beam ? 0.06f : 0.025f, beam ? 0.14f : 0.06f);
                Gunfire.Instance.MuzzleFlash(from, dir, 1f);
            }
            rig.Kick((Aiming ? 0.55f : 0.95f) * (pellets > 1 ? 1.8f : damage > 100f ? 1.6f : 1f), Random.Range(-0.25f, 0.25f));
            Sfx.Play(Sfx.GunSound(gunId), 0.55f, gunId == "carbine" ? 1.1f : 1f);
            if (pellets > 1) Sfx.Play("pump", 0.5f);
            if (GameState.Hud != null) GameState.Hud.OnShot();
        }
    }
}
