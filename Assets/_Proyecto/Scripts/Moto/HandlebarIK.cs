using UnityEngine;

namespace NeoFastRider.Moto
{
    /// <summary>
    /// Ancla las manos del Runner al manubrio de la moto en LateUpdate.
    ///
    /// LateUpdate es obligatorio: el Animator mueve los huesos en Update.
    /// Si se sobreescribe en Update, el Animator lo cancela.
    /// En LateUpdate el override es definitivo cada frame.
    ///
    /// Uso:
    ///   1. Añadir este script al GameObject Runner.
    ///   2. Asignar Handle_Left y Handle_Right (hijos vacíos del manubrio).
    ///   3. Los huesos LeftHand y RightHand se buscan automáticamente en el Armature.
    /// </summary>
    public sealed class HandlebarIK : MonoBehaviour
    {
        [Header("Puntos del manubrio")]
        [Tooltip("GameObject vacío hijo de SuperMotors_5_017 — posición mano izquierda.")]
        [SerializeField] private Transform _handleLeft;

        [Tooltip("GameObject vacío hijo de SuperMotors_5_017 — posición mano derecha.")]
        [SerializeField] private Transform _handleRight;

        [Header("Fuerza del IK (0 = libre, 1 = pegado al manubrio)")]
        [SerializeField, Range(0f, 1f)] private float _ikWeight = 1f;

        [Header("Offset de rotación de manos")]
        [SerializeField] private Vector3 _leftHandRotOffset  = Vector3.zero;
        [SerializeField] private Vector3 _rightHandRotOffset = Vector3.zero;

        // ── Huesos — se buscan automáticamente ───────────────────────────────
        private Transform _boneLeftHand;
        private Transform _boneRightHand;

        private void Awake()
        {
            // Buscar huesos en el Armature del Runner por nombre
            _boneLeftHand  = FindBoneByName(transform, "LeftHand");
            _boneRightHand = FindBoneByName(transform, "RightHand");

            if (_boneLeftHand  == null) Debug.LogWarning("[HandlebarIK] Hueso 'LeftHand' no encontrado.");
            if (_boneRightHand == null) Debug.LogWarning("[HandlebarIK] Hueso 'RightHand' no encontrado.");
        }

        private void LateUpdate()
        {
            if (_ikWeight <= 0f) return;

            ApplyIK(_boneLeftHand,  _handleLeft,  _leftHandRotOffset);
            ApplyIK(_boneRightHand, _handleRight, _rightHandRotOffset);
        }

        private void ApplyIK(Transform bone, Transform target, Vector3 rotOffset)
        {
            if (bone == null || target == null) return;

            // Posición — lerp para suavizar si _ikWeight < 1
            bone.position = Vector3.Lerp(
                bone.position, target.position, _ikWeight);

            // Rotación — aplicar offset encima de la rotación del target
            Quaternion targetRot = target.rotation *
                                   Quaternion.Euler(rotOffset);
            bone.rotation = Quaternion.Slerp(
                bone.rotation, targetRot, _ikWeight);
        }

        // ── Utilidad ──────────────────────────────────────────────────────────
        private static Transform FindBoneByName(Transform root, string boneName)
        {
            if (root.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var result = FindBoneByName(root.GetChild(i), boneName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
