using UnityEngine;
using UnityEngine.EventSystems;

namespace Scifi_UI_Icons
{
public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float scaleUp = 1.2f;
    public float speed = 10f;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        // Smooth scale animation
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * scaleUp;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }
}
}
