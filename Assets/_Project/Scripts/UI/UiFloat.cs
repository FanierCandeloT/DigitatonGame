using UnityEngine;

/// <summary>Flota suavemente hacia arriba y hacia abajo (lento y sutil). Usa el tiempo real.</summary>
public class UiFloat : MonoBehaviour
{
    public float amplitude = 7f;
    public float period = 4.5f;

    RectTransform rt;
    Vector2 basePos;

    void Awake() { rt = (RectTransform)transform; basePos = rt.anchoredPosition; }
    void OnEnable() { if (rt != null) rt.anchoredPosition = basePos; }
    void Update() => rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period) * amplitude);
}
