using UnityEngine;

namespace NeoFastRider.Environment
{
    /// <summary>
    /// Invisible volume trigger at zone boundaries.
    /// Notifies TutorialCheckpointManager when the player enters a new zone.
    /// zoneIndex: 0=A, 1=B, 2=C.
    /// </summary>
    public sealed class TutorialZoneTrigger : MonoBehaviour
    {
        [SerializeField] private Tutorial.TutorialCheckpointManager _checkpointManager;
        [SerializeField] private int _zoneIndex;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _checkpointManager?.EnterZone(_zoneIndex);
        }
    }
}