using UnityEngine;

namespace NeoFastRider.Environment
{
    /// <summary>
    /// Generic collision trigger for Zone B Static_Debris_Wall.
    /// On player contact, tells TutorialCheckpointManager to execute a reset.
    /// Lives on the collider parent; mesh in child named 'Mesh'.
    /// </summary>
    public sealed class TutorialObstacleTrigger : MonoBehaviour
    {
        [SerializeField] private Tutorial.TutorialCheckpointManager _checkpointManager;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _checkpointManager?.TriggerReset();
        }
    }
}