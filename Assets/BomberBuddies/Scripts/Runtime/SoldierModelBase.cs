using UnityEngine;

namespace BomberBuddies
{
    /// <summary>What the game needs from a soldier's body, whoever provides it: the block mannequin or a real rigged model.</summary>
    public abstract class SoldierModelBase : MonoBehaviour
    {
        public int team;
        /// <summary>Where shots leave the carried weapon.</summary>
        public Transform muzzle;
        public abstract void Build(MaterialLibrary lib, int teamId);
        public abstract void SetVisible(bool on);
        /// <summary>Pose for this frame: speed along the ground (m/s), aim pitch in degrees (up positive), crouch 0..1, airborne.</summary>
        public abstract void Animate(float speed, float aimPitch, float crouch, bool airborne, float dt);
        public abstract void Die(Vector3 fromDirection);
        public abstract bool IsDead { get; }
        /// <summary>Gore: the head on a headshot, limbs in a blast. The base body does nothing.</summary>
        public virtual void Dismember(Vector3 fromDir, bool headshot, bool blast) { }

        /// <summary>A real model when one has been added to the project, the block figure otherwise.</summary>
        public static SoldierModelBase Create(Transform parent, MaterialLibrary lib, int team, int layer)
        {
            var go = new GameObject("Model");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            SoldierModelBase model;
            if (SoldierModel.Available) model = go.AddComponent<SoldierModel>();
            else model = go.AddComponent<Mannequin>();
            model.Build(lib, team);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return model;
        }
    }
}
