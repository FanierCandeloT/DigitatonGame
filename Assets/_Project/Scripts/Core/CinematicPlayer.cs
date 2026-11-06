using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Reproduce un CinematicAsset sobre el fundido a negro: imagen a pantalla completa con movimiento lento (zoom o paneo),
/// barras negras de cine y el texto con máquina de escribir (velocidad de Settings, con el sonido de diálogos).
/// Control: clic, E, Enter o Espacio completan el texto y luego pasan a la siguiente toma; Esc salta toda la cinemática.
/// Todo se arma por código, usa tiempo sin escala y se destruye al terminar.
/// </summary>
public static class CinematicPlayer
{
    static readonly Color Cream = new Color(1f, 0.93f, 0.76f);
    static readonly Color Terracotta = new Color(0.72f, 0.29f, 0.15f);

    const float Zoom = 1.12f;      // cuánto se agranda la imagen en ZoomIn/ZoomOut
    const float PanShare = 0.05f;  // fracción del ancho que se desplaza en los paneos
    const float MinSeconds = 0.4f; // antes de este tiempo no se puede saltar la toma (evita saltarse por accidente)

    sealed class Layer
    {
        public RectTransform rect;
        public Image image;
        public CanvasGroup group;
    }

    /// <summary>Reproduce la cinemática. 'font' es la fuente del texto (la de los captions del fundido).</summary>
    public static IEnumerator Play(CinematicAsset cinematic, TMP_FontAsset font, int sortingOrder)
    {
        if (cinematic == null || cinematic.shots == null || cinematic.shots.Length == 0) yield break;

        var canvas = PixelUi.NewCanvas("Cinematica", sortingOrder);
        var root = (RectTransform)canvas.transform;

        // Fondo negro y dos capas de imagen para fundir una toma con la siguiente
        PixelUi.Stretch(PixelUi.NewImage("Fondo", root, Color.black).rectTransform);
        var frame = PixelUi.NewRect("Marco", root);
        PixelUi.Stretch(frame);
        frame.gameObject.AddComponent<RectMask2D>();
        var layers = new[] { NewLayer(frame, "Toma A"), NewLayer(frame, "Toma B") };

        // Barras de cine arriba y abajo, y la caja del texto
        var topBar = PixelUi.NewImage("BarraSuperior", root, Color.black);
        PixelUi.Place(topBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2000f, 60f));
        var textBox = PixelUi.NewImage("CajaTexto", root, new Color(0.05f, 0.03f, 0.03f, 0.82f));
        textBox.rectTransform.anchorMin = new Vector2(0f, 0f);
        textBox.rectTransform.anchorMax = new Vector2(1f, 0f);
        textBox.rectTransform.pivot = new Vector2(0.5f, 0f);
        textBox.rectTransform.sizeDelta = new Vector2(0f, 150f);
        var line = PixelUi.NewImage("Linea", textBox.transform, Terracotta);
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(1f, 1f);
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.sizeDelta = new Vector2(0f, 3f);

        var text = PixelUi.NewLabel("Texto", textBox.transform, "", 34f, Vector2.zero, Vector2.zero,
            FontStyles.Normal, TextAlignmentOptions.Center);
        if (font != null) text.font = font;
        text.color = Cream;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(120f, 14f);
        text.rectTransform.offsetMax = new Vector2(-120f, -14f);

        var hint = PixelUi.NewLabel("Pista", root, "Click / Enter: next   ·   Esc: skip", 18f, Vector2.zero, Vector2.zero,
            FontStyles.Normal, TextAlignmentOptions.BottomRight);
        if (font != null) hint.font = font;
        hint.color = new Color(1f, 0.93f, 0.76f, 0.55f);
        PixelUi.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 8f), new Vector2(520f, 28f));

        bool skipAll = false;
        int current = 0;
        foreach (var shot in cinematic.shots)
        {
            if (shot == null || shot.image == null) continue;
            var layer = layers[current];
            var other = layers[1 - current];
            current = 1 - current;
            layer.rect.SetAsLastSibling();
            ShowShot(layer, shot, canvas);

            // Fundido cruzado: la toma nueva aparece encima de la anterior
            float fade = Mathf.Max(0.01f, cinematic.crossFadeSeconds);
            var animate = Animate(layer, shot, canvas);
            var typed = TypeText(text, shot.caption);
            float elapsed = 0f;
            bool textDone = string.IsNullOrEmpty(shot.caption);
            float holdLeft = shot.holdSeconds;
            bool skipShot = false;
            bool typing = !textDone;

            while (!skipShot)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                layer.group.alpha = Mathf.Clamp01(elapsed / fade);
                animate.MoveNext();
                if (typing && !typed.MoveNext()) { typing = false; textDone = true; }
                if (textDone) holdLeft -= dt;

                if (Escape()) { skipAll = true; break; }
                if (elapsed > MinSeconds && Advance())
                {
                    AudioManager.PlayTextAdvance();
                    if (typing) { text.maxVisibleCharacters = int.MaxValue; typing = false; textDone = true; AudioManager.StopTyping(); }
                    else skipShot = true;
                }
                if (textDone && holdLeft <= 0f) skipShot = true;
                yield return null;
            }

            AudioManager.StopTyping();
            other.group.alpha = 0f;   // la toma anterior ya quedó tapada
            layer.group.alpha = 1f;
            if (skipAll) break;
        }

        AudioManager.StopTyping();
        Object.Destroy(canvas.gameObject);
    }

    static Layer NewLayer(RectTransform parent, string layerName)
    {
        var rect = PixelUi.NewRect(layerName, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = false;
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return new Layer { rect = rect, image = image, group = group };
    }

    static void ShowShot(Layer layer, CinematicAsset.Shot shot, Canvas canvas)
    {
        layer.image.sprite = shot.image;
        layer.image.color = shot.tint;
        layer.group.alpha = 0f;
        layer.rect.anchorMin = layer.rect.anchorMax = new Vector2(0.5f, 0.5f);
        layer.rect.pivot = new Vector2(0.5f, 0.5f);
        layer.rect.localScale = Vector3.one;
        layer.rect.anchoredPosition = Vector2.zero;
        layer.rect.sizeDelta = CoverSize(canvas, shot.image);
    }

    /// <summary>Tamaño que cubre toda la pantalla sin deformar la imagen (recorta lo que sobre).</summary>
    static Vector2 CoverSize(Canvas canvas, Sprite sprite)
    {
        var screen = ((RectTransform)canvas.transform).rect.size;
        float scale = Mathf.Max(screen.x / sprite.rect.width, screen.y / sprite.rect.height);
        return new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
    }

    /// <summary>Movimiento lento de la toma: se avanza una vez por cuadro y dura lo que dure la toma.</summary>
    static IEnumerator Animate(Layer layer, CinematicAsset.Shot shot, Canvas canvas)
    {
        float duration = Mathf.Max(3f, shot.holdSeconds + (string.IsNullOrEmpty(shot.caption) ? 0f : shot.caption.Length / 30f));
        float width = ((RectTransform)canvas.transform).rect.width;
        // Hay un margen extra para que el paneo nunca deje ver el borde de la imagen
        bool pan = shot.move == CinematicAsset.Move.PanLeft || shot.move == CinematicAsset.Move.PanRight;
        float baseScale = pan ? 1f + PanShare * 2.2f : 1f;
        for (float t = 0f; ; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            float scale = baseScale;
            float x = 0f;
            switch (shot.move)
            {
                case CinematicAsset.Move.ZoomIn: scale = Mathf.Lerp(1f, Zoom, k); break;
                case CinematicAsset.Move.ZoomOut: scale = Mathf.Lerp(Zoom, 1f, k); break;
                case CinematicAsset.Move.PanLeft: x = Mathf.Lerp(width * PanShare, -width * PanShare, k); break;
                case CinematicAsset.Move.PanRight: x = Mathf.Lerp(-width * PanShare, width * PanShare, k); break;
            }
            layer.rect.localScale = new Vector3(scale, scale, 1f);
            layer.rect.anchoredPosition = new Vector2(x, 0f);
            yield return null;
        }
    }

    /// <summary>Escribe el texto letra por letra; termina (MoveNext = false) cuando el último caracter ya se ve.</summary>
    static IEnumerator TypeText(TMP_Text label, string caption)
    {
        label.text = caption ?? "";
        label.maxVisibleCharacters = 0;
        if (string.IsNullOrEmpty(caption)) yield break;

        label.ForceMeshUpdate();
        int total = label.textInfo.characterCount;
        AudioManager.StartTyping();
        float shown = 0f;
        while (label.maxVisibleCharacters < total)
        {
            shown += SettingsData.CharsPerSecond * Time.unscaledDeltaTime;
            label.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }
        label.maxVisibleCharacters = int.MaxValue;
        AudioManager.StopTyping();   // en el instante en que termina de aparecer
    }

    static bool Advance()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        var pointer = Pointer.current;
        return pointer != null && pointer.press.wasPressedThisFrame;
    }

    static bool Escape()
    {
        var kb = Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
    }
}
