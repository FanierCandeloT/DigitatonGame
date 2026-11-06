using UnityEngine;

/// <summary>
/// Gestor de los peatones de ambiente del campus. Para no gastar recursos, solo están ACTIVOS los que caen dentro de la
/// vista de la cámara (más un margen): cada pocos décimas de segundo revisa cuáles se ven, apaga el resto por completo
/// (sin dibujo, sin Update) y mueve solo los que están a la vista. Con 10 o 20 peatones, normalmente se ven 1 o 2.
/// </summary>
public class AmbientCrowd : MonoBehaviour
{
    public AmbientWalker[] walkers = new AmbientWalker[0];

    [Tooltip("Cuánto fuera de la pantalla (en unidades) un peatón sigue activo, para que no aparezca de golpe en el borde.")]
    [Min(0f)] public float margin = 2.5f;

    [Tooltip("Cada cuántos segundos se revisa quién está en cámara.")]
    [Min(0.05f)] public float checkInterval = 0.25f;

    Camera cam;
    float nextCheck;

    /// <summary>Cuántos peatones están activos ahora mismo (para comprobar la optimización).</summary>
    public int ActiveCount { get; private set; }

    void Awake()
    {
        foreach (var w in walkers) if (w != null) w.Init();
    }

    void Start() => Refresh(Time.time);

    void Update()
    {
        float now = Time.time;
        if (now >= nextCheck)
        {
            nextCheck = now + checkInterval;
            Refresh(now);
        }

        // Solo se mueven los que están activos (a la vista)
        for (int i = 0; i < walkers.Length; i++)
        {
            var w = walkers[i];
            if (w != null && w.gameObject.activeSelf) w.Place(now);
        }
    }

    void Refresh(float now)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var c = cam.transform.position;
        float halfH = cam.orthographicSize + margin;
        float halfW = cam.orthographicSize * cam.aspect + margin;

        int active = 0;
        foreach (var w in walkers)
        {
            if (w == null) continue;
            var pos = w.Evaluate(now, out _);
            bool inView = Mathf.Abs(pos.x - c.x) <= halfW && Mathf.Abs(pos.y - c.y) <= halfH;

            if (inView != w.gameObject.activeSelf)
            {
                w.gameObject.SetActive(inView);
                if (inView) w.Place(now);
            }
            if (inView) active++;
        }
        ActiveCount = active;
    }
}
