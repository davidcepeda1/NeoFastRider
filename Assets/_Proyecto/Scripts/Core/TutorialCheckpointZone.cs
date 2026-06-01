using UnityEngine;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Box-trigger zone boundary marker. Each zone has one at its start Z.
    /// Requires the player to have tag "Player".
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialCheckpointZone : MonoBehaviour
    {
        [Tooltip("0 = Zone A, 1 = Zone B, 2 = Zone C")]
        [SerializeField] private int _zoneIndex = 0;

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            // TutorialManager tracks zone via Z position each frame.
            // This trigger can be used for zone-entry SFX or visual effects.
            Debug.Log($"[Tutorial] Entered Zone {(char)('A' + _zoneIndex)}");
        }
    }
}
