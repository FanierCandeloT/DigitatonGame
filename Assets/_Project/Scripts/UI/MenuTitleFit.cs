using UnityEngine;

/// <summary>
/// Da al título un tamaño PIXEL PERFECT: el arte original se ve como bloques de ~6 px, así que se escala en pasos de n/6
/// (cada bloque del arte ocupa un número ENTERO n de pixeles de pantalla) y se elige n para que ocupe cerca de la mitad
/// del ancho de la pantalla. Se recalcula si cambia la resolución.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuTitleFit : MonoBehaviour
{
    public Vector2 spriteSize = new Vector2(1480f, 320f);   // tamaño del sprite recortado, en pixeles del arte
    public float blockPixels = 6f;
    [Range(0.3f, 0.7f)] public float widthFraction = 0.5f;

    int lastW, lastH;

    void OnEnable() => Fit();
    void Update() { if (Screen.width != lastW || Screen.height != lastH) Fit(); }

    void Fit()
    {
        lastW = Screen.width; lastH = Screen.height;
        var canvas = GetComponentInParent<Canvas>();
        float k = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;   // pixeles de pantalla por unidad de canvas
        float nativeW = spriteSize.x / blockPixels;                                         // ancho en "pixeles" del arte
        int n = Mathf.Max(1, Mathf.RoundToInt(Screen.width * widthFraction / nativeW));
        float w = spriteSize.x * n / blockPixels, h = spriteSize.y * n / blockPixels;       // en pixeles de pantalla
        ((RectTransform)transform).sizeDelta = new Vector2(w / k, h / k);
    }
}
