using UnityEngine;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Exclusivo de Scene_TutorialLevel: al chocar físicamente con un obstáculo
    /// (tag "Obstacle" — los props reales bajo "Obstacles" usan tag, no layer),
    /// en vez de quedar bloqueada por la física, la moto dispara el fail-safe
    /// reset de TutorialManager (fade + vuelta al punto inicial del nivel).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TutorialObstacleCollisionRelay : MonoBehaviour
    {
        [SerializeField] private string _obstacleTag = "Obstacle";

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.collider.CompareTag(_obstacleTag)) return;
            TutorialManager.Instance?.RegisterCollision();
        }
    }
}
