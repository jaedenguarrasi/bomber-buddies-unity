using System;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The deploy screen (the original's oDeploy): before each spawn the hangar sheet opens in deploy mode with a
    /// Where tab (map and landing points) in front of Vehicle / class, Weapons, Upgrades and Teammates, then Deploy.
    /// The battle carries on behind it.</summary>
    public class DeployScreen : MonoBehaviour
    {
        public static DeployScreen Instance { get; private set; }
        Transform canvasParent;

        public static DeployScreen Create(Transform hudCanvas)
        {
            var go = new GameObject("Deploy"); go.transform.SetParent(hudCanvas, false);
            var d = go.AddComponent<DeployScreen>(); Instance = d; d.canvasParent = hudCanvas.parent; return d;
        }

        public bool IsOpen { get { return Hangar.IsOpen && Hangar.Instance.DeployMode; } }
        public void Hide() { if (IsOpen) Hangar.Instance.gameObject.SetActive(false); }

        /// <summary>Open before a spawn. onDeploy gets the chosen spot, or null for the base.</summary>
        public void Open(Vector3 basePoint, Action<Vector3?> onDeploy)
        {
            Hangar.OpenDeploy(canvasParent, basePoint, onDeploy);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
