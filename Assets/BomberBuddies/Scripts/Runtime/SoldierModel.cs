using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BomberBuddies
{
    /// <summary>
    /// A real soldier: a rigged, textured humanoid from Assets/BomberBuddies/Characters/Resources with motion-capture clips,
    /// blended in code (no animator graph to maintain). Idle/walk/run/crouch/aim/jump/death, the spine bent to the aim,
    /// a rifle in the right hand, and a team tint on the uniform.
    /// </summary>
    public class SoldierModel : SoldierModelBase
    {
        // file names under Characters/Resources (the character) and Characters/Resources/Animations (the clips)
        public const string CharacterName = "Soldier";
        static readonly string[] ClipNames = { "Rifle Idle", "Rifle Walk", "Rifle Run", "Rifle Aim Idle", "Crouch Idle", "Crouch Walk", "Jump", "Death" };
        const int IDLE = 0, WALK = 1, RUN = 2, AIM = 3, CROUCH_IDLE = 4, CROUCH_WALK = 5, JUMP = 6, DEATH = 7;

        static GameObject prefab;
        static AnimationClip[] clips;
        static bool looked;

        public static bool Available
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    prefab = Resources.Load<GameObject>(CharacterName);
                    if (prefab != null)
                    {
                        clips = new AnimationClip[ClipNames.Length];
                        for (int i = 0; i < ClipNames.Length; i++) clips[i] = Resources.Load<AnimationClip>("Animations/" + ClipNames[i]);
                        if (clips[IDLE] == null) { Debug.LogWarning("SoldierModel: character found but no 'Animations/Rifle Idle' clip; using the block figure"); prefab = null; }
                    }
                }
                return prefab != null;
            }
        }

        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly float[] weights = new float[ClipNames.Length];
        Renderer[] renderers = new Renderer[0];
        Transform spine, chest, head, rightHand, leftHand, rifleT;
        float aimPitchSmooth, deathT, crouchK, speedK;
        bool dead, lastAirborne;
        Quaternion baseRot;

        public override bool IsDead { get { return dead; } }

        public override void Build(MaterialLibrary lib, int teamId)
        {
            team = teamId;
            var body = Instantiate(prefab, transform);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            renderers = body.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; var sk = r as SkinnedMeshRenderer; if (sk != null) sk.updateWhenOffscreen = true; }
            TintTeam();

            if (animator.isHuman)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            BuildRifle(lib);

            graph = PlayableGraph.Create("Soldier " + GetInstanceID());
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, ClipNames.Length);
            for (int i = 0; i < ClipNames.Length; i++)
            {
                var clip = clips[i] != null ? clips[i] : clips[IDLE];
                var p = AnimationClipPlayable.Create(graph, clip);
                p.SetApplyFootIK(true);
                graph.Connect(p, 0, mixer, i);
                mixer.SetInputWeight(i, i == IDLE ? 1f : 0f);
            }
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        void TintTeam()
        {
            // enemies get a darker, cooler uniform so the two sides read at a glance
            if (team == 0) return;
            foreach (var r in renderers)
            {
                var mats = r.materials;   // instances: the asset is untouched
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_BaseColor")) continue;
                    var c = mats[i].GetColor("_BaseColor");
                    mats[i].SetColor("_BaseColor", new Color(c.r * 0.55f, c.g * 0.58f, c.b * 0.7f, c.a));
                }
                r.materials = mats;
            }
        }

        void BuildRifle(MaterialLibrary lib)
        {
            // not parented to a bone: every frame it is laid from the right hand (grip) towards the left hand (handguard),
            // which works whatever the rig's bone axes are
            var rifle = new GameObject("Rifle").transform;
            rifle.SetParent(transform, false);
            rifleT = rifle;
            var metal = lib != null ? lib.Get("gunmetal") : null; var wood = lib != null ? lib.Get("wood") : null;
            Part(PrimitiveType.Cube, "Receiver", rifle, new Vector3(0f, 0f, 0.1f), new Vector3(0.045f, 0.08f, 0.45f), metal);
            Part(PrimitiveType.Cube, "Stock", rifle, new Vector3(0f, -0.02f, -0.22f), new Vector3(0.045f, 0.09f, 0.2f), wood);
            Part(PrimitiveType.Cube, "Handguard", rifle, new Vector3(0f, -0.005f, 0.38f), new Vector3(0.05f, 0.065f, 0.18f), wood);
            Part(PrimitiveType.Cube, "Magazine", rifle, new Vector3(0f, -0.09f, 0.1f), new Vector3(0.035f, 0.13f, 0.065f), metal);
            var barrel = Part(PrimitiveType.Cylinder, "Barrel", rifle, new Vector3(0f, 0.015f, 0.62f), new Vector3(0.022f, 0.16f, 0.022f), metal);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(rifle, false); muzzle.localPosition = new Vector3(0f, 0.015f, 0.8f);
            var extra = rifle.GetComponentsInChildren<Renderer>(true);
            var list = new List<Renderer>(renderers); list.AddRange(extra); renderers = list.ToArray();
        }

        static GameObject Part(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public override void SetVisible(bool on)
        {
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = on;
        }

        public override void Animate(float speed, float aimPitch, float crouch, bool airborne, float dt)
        {
            if (!mixer.IsValid()) return;
            float k = Mathf.Min(1f, 8f * dt);
            crouchK = Mathf.Lerp(crouchK, crouch, k);
            speedK = Mathf.Lerp(speedK, speed, k);
            aimPitchSmooth = Mathf.Lerp(aimPitchSmooth, aimPitch, k);
            if (dead)
            {
                deathT += dt;
                Set(DEATH, 1f, k * 2f, true);
                return;
            }
            float walk = Mathf.Clamp01(speedK / 3.2f);
            float run = Mathf.Clamp01((speedK - 3.6f) / 3.4f);
            float stand = 1f - crouchK;
            // target weight for each clip; they always sum to one
            float[] want = new float[ClipNames.Length];
            if (airborne) want[JUMP] = 1f;
            else
            {
                want[CROUCH_WALK] = crouchK * walk;
                want[CROUCH_IDLE] = crouchK * (1f - walk);
                want[RUN] = stand * run;
                want[WALK] = stand * (walk - run);
                want[IDLE] = stand * (1f - walk);
            }
            for (int i = 0; i < want.Length; i++) Set(i, want[i], k * 1.5f, false);
            if (airborne != lastAirborne && airborne) mixer.GetInput(JUMP).SetTime(0.0);
            lastAirborne = airborne;
            // the clips have their own speed; scale the run so the feet keep up with the body
            mixer.GetInput(RUN).SetSpeed(Mathf.Clamp(speedK / 5.5f, 0.8f, 1.4f));
            mixer.GetInput(WALK).SetSpeed(Mathf.Clamp(speedK / 1.9f, 0.7f, 1.5f));
        }

        void Set(int i, float target, float k, bool exclusive)
        {
            if (exclusive) { for (int j = 0; j < weights.Length; j++) { weights[j] = Mathf.Lerp(weights[j], j == i ? 1f : 0f, k); mixer.SetInputWeight(j, weights[j]); } return; }
            weights[i] = Mathf.Lerp(weights[i], target, k);
            mixer.SetInputWeight(i, weights[i]);
        }

        void LateUpdate()
        {
            // after the animation has posed the body: bend the spine and head to the aim pitch
            if (animator == null) return;
            if (dead) { PlaceRifle(); return; }
            float p = Mathf.Clamp(aimPitchSmooth, -60f, 60f);
            if (spine != null) spine.localRotation = spine.localRotation * Quaternion.AngleAxis(-p * 0.35f, Vector3.right);
            if (chest != null) chest.localRotation = chest.localRotation * Quaternion.AngleAxis(-p * 0.35f, Vector3.right);
            if (head != null) head.localRotation = head.localRotation * Quaternion.AngleAxis(-p * 0.2f, Vector3.right);
            PlaceRifle();
        }

        void PlaceRifle()
        {
            if (rifleT == null || rightHand == null) return;
            Vector3 grip = rightHand.position;
            Vector3 fwd = leftHand != null ? leftHand.position - grip : transform.forward;
            if (fwd.sqrMagnitude < 0.0004f) fwd = transform.forward;
            Vector3 up = chest != null ? chest.up : Vector3.up;
            var rot = Quaternion.LookRotation(fwd.normalized, up);
            // the grip sits a little below and ahead of the palm; the handguard lands under the left hand
            rifleT.position = grip + rot * new Vector3(0f, -0.04f, 0.02f);
            rifleT.rotation = rot;
        }

        public override void Die(Vector3 fromDirection)
        {
            if (dead) return;
            dead = true;
            deathT = 0f;
            if (mixer.IsValid())
            {
                var d = mixer.GetInput(DEATH);
                d.SetTime(0.0);
                d.SetSpeed(1.0);
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
