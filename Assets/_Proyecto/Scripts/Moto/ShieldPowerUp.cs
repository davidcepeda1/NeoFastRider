using System.Collections;
using UnityEngine;

/// <summary>
/// HexaCube de Escudo — concede una carga de escudo al jugador al ser recogido.
/// Script autocontenido: incluye animación propia (rotación, flotación, pulso de material).
/// Arrástrate a cualquier HexaCube para que actúe como caja de escudo.
/// </summary>
public sealed class ShieldPowerUp : MonoBehaviour
{
    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Flotación")]
    [SerializeField] private float floatAmplitude = 0.3f;
    [SerializeField] private float floatFrequency = 1.5f;

    [Header("Pulso de Material")]
    [SerializeField] private Material matBase;
    [SerializeField] private Material matEmission;
    [SerializeField] private float pulseInterval = 0.35f;

    [Header("Animación de Recogida")]
    [SerializeField] private float popDuration    = 0.08f;
    [SerializeField] private float popScale       = 1.4f;
    [SerializeField] private float shrinkDuration = 0.28f;

    private bool         _collected;
    private Vector3      _startLocalPos;
    private Vector3      _originalScale;
    private MeshRenderer _renderer;
    private float        _pulseTimer;
    private bool         _showingEmission;

    private void Start()
    {
        _startLocalPos = transform.localPosition;
        _originalScale = transform.localScale;
        _renderer      = GetComponent<MeshRenderer>()
                      ?? GetComponentInChildren<MeshRenderer>();
    }

    private void Update()
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
                _pulseTimer      = 0f;
                _showingEmission = !_showingEmission;
                _renderer.sharedMaterial = _showingEmission ? matEmission : matBase;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected || !other.CompareTag("Player")) return;
        _collected = true;

        // Conceder una carga de escudo al PlayerShieldController del jugador
        var shield = other.GetComponentInParent<NeoFastRider.Moto.PlayerShieldController>()
                  ?? FindAnyObjectByType<NeoFastRider.Moto.PlayerShieldController>();

        if (shield != null)
            shield.GrantShield();
        else
            Debug.LogWarning("[ShieldPowerUp] PlayerShieldController no encontrado en escena.");

        var visor = FindAnyObjectByType<NeoFastRider.UI.HelmetVisorController>();
        visor?.ShowTutorialPrompt("¡Escudo obtenido! Presiona 'E' para activarlo.", 4f);

        StartCoroutine(CollectRoutine());
    }

    private IEnumerator CollectRoutine()
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
