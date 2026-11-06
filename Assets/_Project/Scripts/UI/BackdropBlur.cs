using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Difumina el fondo animado mientras el panel Settings está abierto. Funciona en URP 2D y en WebGL sin post-proceso:
/// cada frame copia el cuadro actual del fondo a una textura de BAJA resolución (160x90) y le pasa un desenfoque gaussiano
/// separable (shader Hidden/DigitatonGame/MenuBlur, 2 pasadas x 2 iteraciones). El resultado se estira con filtro bilineal en
/// un RawImage con tono cálido y oscuro. Mientras no se usa no gasta nada (se apaga y libera las texturas).
/// </summary>
public class BackdropBlur : MonoBehaviour
{
    public Image source;          // el fondo animado (sus cuadros cambian solos)
    public RawImage target;       // dónde se muestra el desenfoque
    public Material blurMaterial;
    public CanvasGroup group;
    public int width = 160, height = 90;
    [Range(0f, 3f)] public float spread = 1.6f;
    public float fadeSeconds = 0.25f;

    RenderTexture a, b;
    float amount;
    bool want;

    public bool Visible => want;
    public void Show(bool on) => want = on;

    void Awake()
    {
        group.alpha = 0f;
        target.gameObject.SetActive(false);
    }

    void OnDestroy() => Release();

    void Release()
    {
        if (a != null) { a.Release(); Destroy(a); a = null; }
        if (b != null) { b.Release(); Destroy(b); b = null; }
    }

    void Update()
    {
        amount = Mathf.MoveTowards(amount, want ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        if (amount <= 0.001f)
        {
            if (target.gameObject.activeSelf) { target.gameObject.SetActive(false); Release(); }
            return;
        }
        if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);
        group.alpha = amount * amount * (3f - 2f * amount);   // smoothstep
        Render();
    }

    RenderTexture Make() => new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
    {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp,
        hideFlags = HideFlags.HideAndDontSave,
    };

    void Render()
    {
        var sprite = source.sprite;
        if (sprite == null || blurMaterial == null) return;
        if (a == null) { a = Make(); b = Make(); target.texture = a; }

        var tex = sprite.texture;
        var r = sprite.textureRect;   // el cuadro actual dentro de la tira
        Graphics.Blit(tex, a, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));

        for (int i = 0; i < 2; i++)
        {
            blurMaterial.SetVector("_Dir", new Vector4(1f, 0f, spread, 0f));
            Graphics.Blit(a, b, blurMaterial);
            blurMaterial.SetVector("_Dir", new Vector4(0f, 1f, spread, 0f));
            Graphics.Blit(b, a, blurMaterial);
        }
    }
}
