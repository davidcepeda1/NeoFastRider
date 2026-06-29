using UnityEngine;

namespace NeoFastRider.Environment
{
    /// <summary>
    /// Añade automáticamente colliders a cualquier objeto hijo que tenga MeshRenderer.
    /// Funciona tanto en el Editor (al arrastrar objetos) como en Runtime.
    ///
    /// Uso: colocar en [Interactive_Threats] o en cada Threat_Zone.
    /// Al agregar cualquier objeto como hijo, este script le pone collider + capa.
    /// </summary>
    [ExecuteAlways]
    public sealed class ObstacleZone : MonoBehaviour
    {
        [Tooltip("Capa que se asigna a los obstáculos. Debe colisionar con Default (la moto).")]
        [SerializeField] private string _obstacleLayerName = "Obstacle";

        [Tooltip("Si true, usa MeshCollider (forma exacta). Si false, usa BoxCollider (caja simple).")]
        [SerializeField] private bool _useMeshCollider = true;

        private int _obstacleLayer = -1;

        private void OnEnable()
        {
            _obstacleLayer = LayerMask.NameToLayer(_obstacleLayerName);
            ProcessAllChildren();
        }

        /// <summary>
        /// Unity llama esto automáticamente cuando se añade o quita un hijo.
        /// Funciona en Editor y Runtime.
        /// </summary>
        private void OnTransformChildrenChanged()
        {
            ProcessAllChildren();
        }

        /// <summary>
        /// Escanea TODOS los hijos recursivamente y añade collider + capa
        /// a cualquier objeto que tenga MeshRenderer pero no tenga Collider.
        /// </summary>
        public void ProcessAllChildren()
        {
            if (_obstacleLayer < 0)
                _obstacleLayer = LayerMask.NameToLayer(_obstacleLayerName);

            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                // Siempre asignar la capa Obstacle (incluso si ya tiene collider)
                if (_obstacleLayer >= 0)
                    mr.gameObject.layer = _obstacleLayer;

                // Saltar adición de collider si ya tiene uno
                if (mr.GetComponent<Collider>() != null) continue;

                // Saltar el propio ObstacleZone GO
                if (mr.gameObject == gameObject) continue;

                var mf = mr.GetComponent<MeshFilter>();

                if (_useMeshCollider && mf != null && mf.sharedMesh != null)
                {
                    // MeshCollider para forma exacta del obstáculo
                    var mc = mr.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    // No necesita convex: la moto tiene BoxCollider (primitivo)
                    // que SÍ colisiona con MeshCollider no-convex
                }
                else
                {
                    // BoxCollider como fallback
                    mr.gameObject.AddComponent<BoxCollider>();
                }


            }

            // También procesar hijos que son contenedores (sin MeshRenderer)
            // para que OnTransformChildrenChanged se propague
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<ObstacleZone>() == null && child.childCount > 0)
                {
                    // Añadir ObstacleZone a sub-contenedores (Threat_Zone_A/B/C)
                    // para que detecten cuando SUS hijos cambian
                    var childZone = child.gameObject.GetComponent<ObstacleZone>();
                    if (childZone == null)
                    {
                        childZone = child.gameObject.AddComponent<ObstacleZone>();
                        childZone._obstacleLayerName = _obstacleLayerName;
                        childZone._useMeshCollider   = _useMeshCollider;
                    }
                }
            }
        }
    }
}
