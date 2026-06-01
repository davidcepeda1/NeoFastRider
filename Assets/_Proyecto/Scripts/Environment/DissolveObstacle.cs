using System.Collections;
using UnityEngine;

namespace NeoFastRider.Environment
{
    /// <summary>
    /// Zone C barrier — implements IDissolveTarget.
    /// TASK 3 rule: BoxCollider on root, MeshFilter+MeshRenderer on child named 'Mesh'.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DissolveObstacle : MonoBehaviour, NeoFastRider.Moto.IDissolveTarget
    {
        [SerializeField] private float _dissolveDuration = 0.45f;
        [SerializeField] private Color _dissolveColor = new Color(0f, 0.94f, 1f);
        [SerializeField] private Transform _meshChild;

        private BoxCollider _col;
        private bool _dissolved;

        private void Awake()
        {
            _col = GetComponent<BoxCollider>();
            if (_meshChild == null) { var c = transform.Find("Mesh"); if (c) _meshChild = c; }
        }

        public void Dissolve()
        {
            if (_dissolved) return;
            _dissolved = true;
            _col.enabled = false;
            StartCoroutine(DoDissolve());
        }

        private IEnumerator DoDissolve()
        {
            if (_meshChild == null) { gameObject.SetActive(false); yield break; }
            var startScale = _meshChild.localScale;
            var rend = _meshChild.GetComponent<Renderer>();
            if (rend != null) rend.material.color = _dissolveColor;

            float e = 0f;
            while (e < _dissolveDuration)
            {
                _meshChild.localScale = Vector3.Lerp(startScale, Vector3.zero,
                    Mathf.SmoothStep(0f, 1f, e / _dissolveDuration));
                e += Time.deltaTime;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}