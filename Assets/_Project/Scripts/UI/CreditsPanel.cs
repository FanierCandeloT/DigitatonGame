using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panel de créditos del menú principal. El texto sale de un CreditsAsset (Assets/_Project/Data/Credits.asset) y se arma
/// al abrir el panel: títulos de sección en naranja, nombres en crema y códigos en un crema más tenue, centrado y con
/// espacio parejo. Si no cabe en la ventana se desplaza con la rueda del mouse y con las flechas (arriba/abajo, Re Pág / Av Pág).
/// Aparece y se cierra con SettingsPanelAnimator (fundido + crecimiento). Back o Esc lo cierran.
/// </summary>
public class CreditsPanel : MonoBehaviour
{
    public CreditsAsset asset;
    public TMP_FontAsset font;
    public TMP_Text titleLabel;
    public ScrollRect scroll;
    public RectTransform content;
    public Button backButton;

    public UnityEvent onClosing;
    public UnityEvent onBack;

    static readonly Color Orange = new Color(0.96f, 0.52f, 0.12f);
    static readonly Color Cream = new Color(1f, 0.93f, 0.76f);
    static readonly Color CreamDim = new Color(0.72f, 0.66f, 0.54f);

    const float LineWidth = 600f;
    const float ScrollSpeed = 380f;   // unidades de canvas por segundo con las flechas
    const float BlockGap = 20f;

    SettingsPanelAnimator animator;
    bool wired;

    void Wire()
    {
        if (wired) return;
        wired = true;
        animator = GetComponent<SettingsPanelAnimator>();
        backButton.onClick.AddListener(Back);
        backButton.navigation = new Navigation { mode = Navigation.Mode.None };   // las flechas desplazan el texto, no mueven la selección
    }

    void OnEnable()
    {
        Wire();
        Populate();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        float range = content.rect.height - ((RectTransform)scroll.viewport).rect.height;
        if (range <= 1f) return;   // todo cabe: nada que desplazar

        float step = 0f;
        if (kb.downArrowKey.isPressed) step = -ScrollSpeed * Time.unscaledDeltaTime;
        if (kb.upArrowKey.isPressed) step = ScrollSpeed * Time.unscaledDeltaTime;
        if (kb.pageDownKey.wasPressedThisFrame) step = -((RectTransform)scroll.viewport).rect.height * 0.9f;
        if (kb.pageUpKey.wasPressedThisFrame) step = ((RectTransform)scroll.viewport).rect.height * 0.9f;
        if (step != 0f) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + step / range);
    }

    /// <summary>Cierra con la animación (fundido + crecimiento al revés).</summary>
    public void Back()
    {
        if (animator == null) animator = GetComponent<SettingsPanelAnimator>();
        if (animator != null && animator.isActiveAndEnabled)
        {
            if (animator.Closing) return;
            onClosing?.Invoke();
            animator.PlayClose(Finish);
            return;
        }
        onClosing?.Invoke();
        Finish();
    }

    void Finish()
    {
        gameObject.SetActive(false);
        onBack?.Invoke();
    }

    // ───────────── Contenido ─────────────

    void Populate()
    {
        if (asset == null) return;
        if (titleLabel != null) titleLabel.text = asset.title;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        bool first = true;
        foreach (var block in asset.blocks)
        {
            if (!first) Spacer(BlockGap);
            first = false;
            if (!string.IsNullOrEmpty(block.heading)) AddLine(block.heading, 24f, Orange, Spacing: 6f);
            if (block.entries == null) continue;
            foreach (var entry in block.entries)
            {
                string text = entry.text;
                if (!string.IsNullOrEmpty(entry.detail))
                    text += " <color=#" + ColorUtility.ToHtmlStringRGB(CreamDim) + ">- " + entry.detail + "</color>";
                AddLine(text, entry.big ? 30f : 22f, Cream, Spacing: 2f);
            }
        }
        scroll.verticalNormalizedPosition = 1f;   // siempre empieza arriba
    }

    void Spacer(float height)
    {
        var go = new GameObject("Espacio", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
    }

    void AddLine(string text, float size, Color color, float Spacing)
    {
        var row = new GameObject("Linea", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(content, false);

        var labelGo = new GameObject("Texto", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        label.raycastTarget = false;
        label.text = text;
        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // la altura sale de medir el texto con la fuente real: nada se corta ni se empalma
        float height = Mathf.Ceil(label.GetPreferredValues(text, LineWidth, 0f).y) + Spacing;
        row.GetComponent<LayoutElement>().preferredHeight = height;

        PixelTextOutline.Build(label, 1.5f);
    }
}
