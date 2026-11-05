using UnityEngine;

/// <summary>
/// Peatón de ambiente: camina de ida y vuelta por una ruta (puntos del mundo) con una pausa en cada extremo.
/// No usa física ni Update propio: su posición es una función del tiempo (Evaluate), así AmbientCrowd puede
/// apagarlo cuando está fuera de la cámara y, al volver a encenderlo, aparece exactamente donde "estaría".
/// </summary>
public class AmbientWalker : MonoBehaviour
{
    [Tooltip("Puntos de la ruta en coordenadas del mundo. Camina del primero al último y regresa.")]
    public Vector2[] path = new Vector2[0];

    [Min(0.1f)] public float speed = 1.6f;
    [Min(0f)] public float pauseSeconds = 1.5f;

    [Tooltip("Desfase para que varios peatones en la misma ruta no vayan juntos.")]
    public float timeOffset;

    float length, moveTime, cycle;
    float[] cumulative;
    bool ready;

    /// <summary>Calcula el largo de la ruta. Lo llama AmbientCrowd al iniciar.</summary>
    public void Init()
    {
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

    /// <summary>Posición del peatón en el instante time y hacia dónde camina (+1 derecha, -1 izquierda, 0 parado).</summary>
    public Vector2 Evaluate(float time, out float facing)
    {
        facing = 0f;
        if (!ready) return path != null && path.Length > 0 ? path[0] : (Vector2)transform.position;

        float t = Mathf.Repeat(time + timeOffset, cycle);
        float distance;
        bool forward;

        if (t < moveTime) { distance = t * speed; forward = true; }
        else if (t < moveTime + pauseSeconds) { distance = length; forward = true; facing = 0f; return Sample(distance); }
        else if (t < 2f * moveTime + pauseSeconds) { distance = length - (t - moveTime - pauseSeconds) * speed; forward = false; }
        else { return Sample(0f); }

        var pos = Sample(distance);
        var ahead = Sample(Mathf.Clamp(distance + (forward ? 0.2f : -0.2f), 0f, length));
        facing = Mathf.Sign(ahead.x - pos.x);
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

    /// <summary>Coloca al peatón donde le toca en este instante, con un pequeño balanceo al caminar.</summary>
    public void Place(float time)
    {
        var pos = Evaluate(time, out float facing);
        float bob = facing != 0f ? Mathf.Abs(Mathf.Sin(time * 9f)) * 0.06f : 0f;
        transform.position = new Vector3(pos.x, pos.y + bob, transform.position.z);
    }
}
