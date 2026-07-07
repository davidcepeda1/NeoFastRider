using System.Collections;
using UnityEngine;

public class SupplyPowerUp : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Float")]
    [SerializeField] private float floatAmplitude = 0.3f;
    [SerializeField] private float floatFrequency = 1.5f;

    [Header("Material Pulse")]
    [SerializeField] private Material matBase;
    [SerializeField] private Material matEmission;
    [SerializeField] private float pulseInterval = 0.35f;

    [Header("Collect")]
    [SerializeField] private float popDuration    = 0.08f;
    [SerializeField] private float popScale       = 1.4f;
    [SerializeField] private float shrinkDuration = 0.28f;

    private bool _collected;
    private Vector3 _startLocalPos;
    private Vector3 _originalScale;
    private MeshRenderer _renderer;
    private float _pulseTimer;
    private bool _showingEmission;

    void Start()
    {
        _startLocalPos = transform.localPosition;
        _originalScale = transform.localScale;
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null)
            _renderer = GetComponentInChildren<MeshRenderer>();
    }

    void Update()
    {
        if (_collected) return;

        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.localPosition = _startLocalPos + new Vector3(0f, yOffset, 0f);

        if (_renderer != null && matBase != null && matEmission != null)
        {
            _pulseTimer += Time.deltaTime;
            if (_pulseTimer >= pulseInterval)
            {
                _pulseTimer = 0f;
                _showingEmission = !_showingEmission;
                _renderer.sharedMaterial = _showingEmission ? matEmission : matBase;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected || !other.CompareTag("Player")) return;
        _collected = true;

        // Recargar la barra de láser al 100 % en el arma de la moto
        var weapon = FindAnyObjectByType<NeoFastRider.Moto.PlayerPulseWeapon>();
        weapon?.SetLaserEnergy(1f);

        StartCoroutine(CollectRoutine(other.transform));
    }

    IEnumerator CollectRoutine(Transform collector)
    {
        // Pop
        float elapsed = 0f;
        Vector3 bigScale = _originalScale * popScale;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(_originalScale, bigScale, elapsed / popDuration);
            yield return null;
        }

        // Encoge y sube
        elapsed = 0f;
        Vector3 startPos  = transform.position;
        Vector3 targetPos = startPos + Vector3.up * 3f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t      = elapsed / shrinkDuration;
            float tEased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.Lerp(bigScale, Vector3.zero, tEased);
            transform.position   = Vector3.Lerp(startPos, targetPos, tEased);
            yield return null;
        }

        Destroy(gameObject);
    }
}
