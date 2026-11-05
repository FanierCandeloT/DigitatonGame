using UnityEngine;

/// <summary>Icono que flota sobre un NPC con un reto pendiente (el "!" sobre la cabeza). Solo se mueve; se apaga desde fuera.</summary>
public class QuestIcon : MonoBehaviour
{
    public float amplitude = 0.15f;
    public float speed = 3f;

    Vector3 baseLocal;

    void Awake() => baseLocal = transform.localPosition;

    void Update()
    {
        transform.localPosition = baseLocal + Vector3.up * (Mathf.Sin(Time.unscaledTime * speed) * amplitude);
    }
}
