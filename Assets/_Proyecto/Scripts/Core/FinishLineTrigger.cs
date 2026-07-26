using UnityEngine;

namespace NeoFastRider.Core
{
    /// <summary>Trigger colocado al final de la pista generada. Avisa a LevelGoalManager al cruzarlo.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FinishLineTrigger : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            LevelGoalManager.Instance?.RegisterFinish();
        }
    }
}
