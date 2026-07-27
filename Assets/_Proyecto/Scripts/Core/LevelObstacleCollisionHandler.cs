using UnityEngine;
using NeoFastRider.Moto;

namespace NeoFastRider.Core
{
    /// <summary>
    /// Exclusivo de niveles con meta (Scene_Level_01 y sucesores — el tutorial usa su propio
    /// TutorialObstacleCollisionRelay con fail-safe reset, no game over): al chocar físicamente
    /// con un obstáculo (tag "Obstacle"), lleva la vida a 0 de una — acá no hay inmortalidad ni
    /// reset de posición, el choque termina el intento (dispara PlayerHealth.OnPlayerDeath, que
    /// GameOverManager ya está escuchando).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LevelObstacleCollisionHandler : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private string _obstacleTag = "Obstacle";

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.collider.CompareTag(_obstacleTag)) return;
            if (_playerHealth != null) _playerHealth.Kill();
        }
    }
}
