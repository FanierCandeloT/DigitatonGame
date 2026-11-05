using UnityEngine;

/// <summary>
/// Límites del mapa de la escena. La cámara (Confiner 2D) no sale de este rectángulo.
/// Ajusta el tamaño con el BoxCollider2D.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MapBounds : MonoBehaviour
{
    public Collider2D Shape => GetComponent<Collider2D>();

    void Reset()
    {
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(30f, 20f);
    }

    void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider2D box)) return;
        Gizmos.color = new Color(1f, 0.3f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.TransformPoint(box.offset), Vector3.Scale(box.size, transform.lossyScale));
    }
}
