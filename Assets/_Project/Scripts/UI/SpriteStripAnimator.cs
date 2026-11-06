using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reproduce una tira de sprites en un Image de la UI (fondo animado del menú). Usa el tiempo real (unscaled), así que
/// sigue animándose aunque el juego esté en pausa.
/// - forwardSeconds: lo que dura UNA pasada por todos los cuadros (el video original dura 4,0167 s).
/// - pingPong: al llegar al último cuadro regresa hacia atrás en vez de saltar al primero (se usa cuando el primer y el
///   último cuadro son muy distintos y el salto se notaría). La velocidad es la misma en los dos sentidos.
/// </summary>
[RequireComponent(typeof(Image))]
public class SpriteStripAnimator : MonoBehaviour
{
    public Sprite[] frames;
    [Min(0.1f)] public float forwardSeconds = 4.0167f;
    public bool pingPong = true;

    Image image;
    int last = -1;

    void Awake() => image = GetComponent<Image>();

    void OnEnable() => last = -1;

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int n = frames.Length;
        float frameTime = forwardSeconds / n;
        int step = Mathf.FloorToInt(Time.unscaledTime / frameTime);

        int index;
        if (!pingPong || n < 3) index = step % n;
        else
        {
            int period = 2 * n - 2;           // 0,1,...,n-1,n-2,...,1
            int k = step % period;
            index = k < n ? k : period - k;
        }

        if (index == last) return;
        last = index;
        image.sprite = frames[index];
    }
}
