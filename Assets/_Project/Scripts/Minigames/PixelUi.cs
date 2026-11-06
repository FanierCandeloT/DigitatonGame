using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utilidades para armar interfaz con estética pixel art desde código (cajas con esquinas escalonadas,
/// etiquetas y anclajes). Las usan el minijuego de memoria y el panel de diálogo.
/// </summary>
public static class PixelUi
{
    /// <summary>Cuántas unidades de canvas ocupa cada "pixel" de la caja.</summary>
    public const float PixelScale = 4f;

    static Sprite box;

    /// <summary>Caja 8x8 con las esquinas cortadas en escalera, lista para 9-slice con filtro Point.</summary>
    public static Sprite Box
    {
        get
        {
            if (box == null) box = MakeBox();
            return box;
        }
    }

    static Sprite MakeBox()
    {
        const int size = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Min(x, size - 1 - x);
                int dy = Mathf.Min(y, size - 1 - y);
                bool corner = dx + dy <= 1;   // quita 3 pixeles por esquina
                texture.SetPixel(x, y, corner ? Color.clear : Color.white);
            }
        }
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(3f, 3f, 3f, 3f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>Espera en tiempo real (no le afecta la pausa ni el timeScale). Para usar con StartCoroutine / yield return.</summary>
    public static System.Collections.IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    public static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image NewImage(string objectName, Transform parent, Color color)
    {
        var image = NewRect(objectName, parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    /// <summary>Imagen con la caja pixelada (9-slice).</summary>
    public static Image NewBox(string objectName, Transform parent, Color color)
    {
        var image = NewImage(objectName, parent, color);
        image.sprite = Box;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / PixelScale;
        return image;
    }

    /// <summary>Caja con borde de un pixel: devuelve el borde (raíz) y deja en fill el relleno interior.</summary>
    public static Image NewPanel(string objectName, Transform parent, Color outline, Color fillColor, out Image fill)
    {
        var border = NewBox(objectName, parent, outline);
        fill = NewBox("Relleno", border.transform, fillColor);
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(PixelScale, PixelScale);
        fill.rectTransform.offsetMax = new Vector2(-PixelScale, -PixelScale);
        return border;
    }

    /// <summary>
    /// Convierte una imagen existente (por ejemplo la caja gris de un HUD) en un panel pixelado con borde de color y relleno oscuro opaco,
    /// para que se lea bien sobre cualquier fondo. Devuelve el relleno; el contenido existente queda encima. Seguro si el padre usa Layout Group.
    /// </summary>
    public static Image Restyle(Image target, Color outline, Color fillColor)
    {
        target.sprite = Box;
        target.type = Image.Type.Sliced;
        target.pixelsPerUnitMultiplier = 1f / PixelScale;
        target.color = outline;

        // Idempotente: si ya tiene relleno (se llamó antes), solo se actualiza su color
        var existing = target.transform.Find("Relleno");
        if (existing != null && existing.TryGetComponent(out Image current))
        {
            current.color = fillColor;
            return current;
        }

        var fill = NewBox("Relleno", target.transform, fillColor);
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(PixelScale, PixelScale);
        fill.rectTransform.offsetMax = new Vector2(-PixelScale, -PixelScale);
        fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return fill;
    }

    public static TextMeshProUGUI NewLabel(string objectName, Transform parent, string text, float size, Vector2 position,
        Vector2 dimensions, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = NewRect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = dimensions;
        return label;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Ancla el rectángulo a un punto del padre (0..1) y lo coloca con pivote propio.</summary>
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>Crea un canvas de pantalla completa de 1280x720 de referencia.</summary>
    public static Canvas NewCanvas(string objectName, int sortingOrder)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }
}
