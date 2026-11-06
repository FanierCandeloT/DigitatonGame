using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondo difuminado y oscurecido (tono cálido) del menú de pausa. Sirve en URP 2D y en WebGL, sin post-proceso:
/// al pausar se renderiza UNA vez la cámara principal a una textura de baja resolución (sin la interfaz: el HUD no sale),
/// se le pasa el desenfoque gaussiano del shader Hidden/DigitatonGame/MenuBlur y se muestra en un RawImage a pantalla completa.
/// Como el juego queda detenido en la pausa, no hace falta repetirlo cada frame.
/// </summary>
public class PauseBackdrop : MonoBehaviour
{
    public RawImage image;
    public Material blurMaterial;
    public int width = 320;
    [Range(0f, 3f)] public float spread = 1.5f;
    [Tooltip("Tono cálido que oscurece lo que hay detrás.")]
    public Color tint = new Color(0.55f, 0.40f, 0.32f, 1f);

    RenderTexture a, b;

    /// <summary>Foto de la escena actual, desenfocada. Llamar justo ANTES de mostrar el menú de pausa.</summary>
    public void Capture()
    {
        Release();
        image.color = tint;
        var cam = Camera.main;
        if (cam == null || blurMaterial == null) { image.texture = null; image.color = new Color(0.08f, 0.05f, 0.04f, 0.9f); return; }

        int height = Mathf.Max(90, Mathf.RoundToInt(width * (float)Screen.height / Mathf.Max(1, Screen.width)));
        a = Make(height, 16);
        b = Make(height, 0);

        var previous = cam.targetTexture;
        cam.targetTexture = a;
        cam.Render();                 // la escena tal como se ve ahora (sin canvases de pantalla)
        cam.targetTexture = previous;

        for (int i = 0; i < 2; i++)
        {
            blurMaterial.SetVector("_Dir", new Vector4(1f, 0f, spread, 0f));
            Graphics.Blit(a, b, blurMaterial);
            blurMaterial.SetVector("_Dir", new Vector4(0f, 1f, spread, 0f));
            Graphics.Blit(b, a, blurMaterial);
        }
        RenderTexture.active = null;   // el Blit deja la última textura activa; si no se suelta, Release() avisa
        image.texture = a;
    }

    RenderTexture Make(int height, int depth) => new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32)
    {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp,
        hideFlags = HideFlags.HideAndDontSave,
    };

    /// <summary>Libera las texturas (al terminar de cerrarse la pausa).</summary>
    public void Release()
    {
        if (RenderTexture.active == a || RenderTexture.active == b) RenderTexture.active = null;
        if (image != null) image.texture = null;
        if (a != null) { a.Release(); Destroy(a); a = null; }
        if (b != null) { b.Release(); Destroy(b); b = null; }
    }

    void OnDestroy() => Release();
}
