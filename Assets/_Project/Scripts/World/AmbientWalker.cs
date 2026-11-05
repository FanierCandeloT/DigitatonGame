using UnityEngine;

/// <summary>
/// Peatón de ambiente: camina de ida y vuelta por una ruta (puntos del mundo) con una pausa en cada extremo.
/// No usa física ni Update propio: su posición es una función del tiempo (Evaluate), así AmbientCrowd puede
/// apagarlo cuando está fuera de la cámara y, al volver a encenderlo, aparece exactamente donde "estaría".
/// Si tiene los 12 cuadros de una hoja de personaje (frames), también elige el cuadro según hacia dónde camina.
/// </summary>
public class AmbientWalker : MonoBehaviour
{
    [Tooltip("Puntos de la ruta en coordenadas del mundo. Camina del primero al último y regresa.")]
    public Vector2[] path = new Vector2[0];

    [Min(0.1f)] public float speed = 1.6f;
    [Min(0f)] public float pauseSeconds = 1.5f;

    [Tooltip("Desfase para que varios peatones en la misma ruta no vayan juntos.")]
    public float timeOffset;

    [Tooltip("Los 12 cuadros de la hoja (fila*3 + columna): filas abajo, izquierda, derecha, arriba; columnas paso, quieto, paso.")]
    public Sprite[] frames = new Sprite[0];
    [Min(1f)] public float walkFps = 7f;

    static readonly int[] WalkColumns = { 1, 0, 1, 2 };   // quieto, paso, quieto, paso

    float length, moveTime, cycle;
    float[] cumulative;
    bool ready;
    SpriteRenderer sr;
    int row;   // fila de la última dirección (0 abajo, 1 izquierda, 2 derecha, 3 arriba)

    /// <summary>Calcula el largo de la ruta. Lo llama AmbientCrowd al iniciar.</summary>
    public void Init()
    {
        sr = GetComponent<SpriteRenderer>();
        if (path == null || path.Length < 2)
        {
            ready = false;
            return;
        }

        cumulative = new float[path.Length];
        for (int i = 1; i < path.Length; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(path[i - 1], path[i]);
        length = cumulative[path.Length - 1];
        moveTime = length / speed;
        cycle = 2f * (moveTime + pauseSeconds);
        ready = length > 0.01f;
    }

    /// <summary>Posición del peatón en el instante time y su dirección de marcha (cero si está parado).</summary>
    public Vector2 Evaluate(float time, out Vector2 direction)
    {
        direction = Vector2.zero;
        if (!ready) return path != null && path.Length > 0 ? path[0] : (Vector2)transform.position;

        float t = Mathf.Repeat(time + timeOffset, cycle);
        float distance;
        bool forward;

        if (t < moveTime) { distance = t * speed; forward = true; }
        else if (t < moveTime + pauseSeconds) { return Sample(length); }
        else if (t < 2f * moveTime + pauseSeconds) { distance = length - (t - moveTime - pauseSeconds) * speed; forward = false; }
        else { return Sample(0f); }

        var pos = Sample(distance);
        var ahead = Sample(Mathf.Clamp(distance + (forward ? 0.2f : -0.2f), 0f, length));
        direction = (ahead - pos).normalized;
        return pos;
    }

    Vector2 Sample(float distance)
    {
        for (int i = 1; i < path.Length; i++)
        {
            if (distance <= cumulative[i])
            {
                float segment = cumulative[i] - cumulative[i - 1];
                float k = segment <= 0.0001f ? 0f : (distance - cumulative[i - 1]) / segment;
                return Vector2.Lerp(path[i - 1], path[i], k);
            }
        }
        return path[path.Length - 1];
    }

    /// <summary>Coloca al peatón donde le toca en este instante y le pone el cuadro de animación que corresponde.</summary>
    public void Place(float time)
    {
        var pos = Evaluate(time, out Vector2 direction);
        bool moving = direction != Vector2.zero;
        bool animated = frames != null && frames.Length >= 12 && sr != null;

        // Con sprites el balanceo lo da la animación; con cajas de color se simula con un pequeño salto.
        float bob = moving && !animated ? Mathf.Abs(Mathf.Sin(time * 9f)) * 0.06f : 0f;
        transform.position = new Vector3(pos.x, pos.y + bob, transform.position.z);

        if (!animated) return;
        if (moving)
        {
            if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x)) row = direction.y > 0f ? 3 : 0;
            else row = direction.x < 0f ? 1 : 2;
        }
        int column = moving ? WalkColumns[Mathf.FloorToInt(time * walkFps) % WalkColumns.Length] : 1;
        sr.sprite = frames[row * 3 + column];
    }
}
