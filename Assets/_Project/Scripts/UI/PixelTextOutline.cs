using TMPro;
using UnityEngine;

/// <summary>
/// Contorno oscuro para texto de TextMeshPro en modo raster (la fuente pixel art sin suavizado no tiene contorno propio):
/// cuatro copias del texto desplazadas en diagonal van DETRÁS del texto real. Las copias se crean en el editor
/// (<see cref="Build"/>) y quedan guardadas en la escena; en juego este componente solo las mantiene iguales al original.
/// </summary>
[DisallowMultipleComponent]
public class PixelTextOutline : MonoBehaviour
{
    public TMP_Text label;
    public TMP_Text[] copies;
    public float offset = 2f;
    public Color outlineColor = new Color(0.08f, 0.03f, 0.02f, 1f);

    string lastText;
    Color lastColor;
    float lastSize = -1f;
    bool lastActive = true;

    void LateUpdate()
    {
        if (label == null || copies == null) return;
        bool active = label.isActiveAndEnabled;
        if (active != lastActive)
        {
            lastActive = active;
            foreach (var c in copies) if (c != null) c.gameObject.SetActive(active);
        }
        if (!active) return;
        if (label.text == lastText && label.color == lastColor && Mathf.Approximately(label.fontSize, lastSize)) return;
        lastText = label.text; lastColor = label.color; lastSize = label.fontSize;
        foreach (var c in copies)
        {
            if (c == null) continue;
            c.text = lastText;
            c.fontSize = lastSize;
            var oc = outlineColor; oc.a = lastColor.a;
            c.color = oc;
        }
    }

    /// <summary>Crea las 4 copias detrás de label (mismos anclajes y tamaño) y deja el componente configurado.</summary>
    public static PixelTextOutline Build(TMP_Text label, float offset = 2f)
    {
        var existing = label.GetComponent<PixelTextOutline>();
        if (existing != null)
        {
            if (existing.copies != null) foreach (var c in existing.copies) if (c != null) DestroyImmediate(c.gameObject);
            DestroyImmediate(existing);
        }
        var comp = label.gameObject.AddComponent<PixelTextOutline>();
        comp.label = label;
        comp.offset = offset;
        comp.copies = new TMP_Text[4];
        var dirs = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
        int at = label.transform.GetSiblingIndex();
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject(label.name + " (contorno " + i + ")", typeof(RectTransform));
            go.transform.SetParent(label.transform.parent, false);
            go.transform.SetSiblingIndex(at);   // antes que el texto: se dibuja detrás
            at++;
            var rt = (RectTransform)go.transform;
            var src = label.rectTransform;
            rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            rt.anchoredPosition = src.anchoredPosition + dirs[i] * offset;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = label.font; t.fontSharedMaterial = label.fontSharedMaterial;
            t.fontSize = label.fontSize; t.fontStyle = label.fontStyle;
            t.alignment = label.alignment; t.textWrappingMode = label.textWrappingMode;
            t.overflowMode = label.overflowMode; t.margin = label.margin;
            t.richText = label.richText; t.raycastTarget = false;
            t.text = label.text; t.color = comp.outlineColor;
            comp.copies[i] = t;
        }
        return comp;
    }
}
