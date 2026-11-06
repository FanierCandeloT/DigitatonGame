using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Libro del tutorial: panel centrado con el sprite del libro abierto y el texto sobre las dos páginas.
/// - Botón derecho / flecha derecha: página siguiente. Botón izquierdo / flecha izquierda: la anterior (con la animación de pasar hoja).
/// - Botón X o Esc: cierra. El botón izquierdo se oculta en la primera hoja y el derecho en la última.
/// - Sonidos: OpenBook al abrir, cerrar y pasar página; el Click de los botones lo pone el AudioManager solo.
/// - Andrés queda quieto mientras está abierto. El contenido vive en un TutorialBookAsset (ScriptableObject).
/// - Pixel perfect: el libro se dibuja con filtro Point a una escala en pasos de 1/6 (cada "pixel" del arte, de ~6 px,
///   ocupa un número entero de pixeles de pantalla) y con tamaños y posiciones redondeados.
/// </summary>
public class TutorialBookPanel : MonoBehaviour
{
    /// <summary>Se dispara cuando el libro termina de cerrarse.</summary>
    public static event Action Closed;
    public static bool IsOpen { get; private set; }

    // Medidas del arte original (LibroTuto1: 1698 x 926 px)
    const float ArtW = 1698f, ArtH = 926f;
    // Zona de texto de cada página, en pixeles del arte (origen arriba a la izquierda)
    static readonly Rect LeftText = new Rect(335f, 175f, 455f, 535f);
    static readonly Rect RightText = new Rect(910f, 175f, 455f, 535f);
    const float TurnSeconds = 0.16f;
    const float BaseFont = 38f;
    static readonly Color Ink = new Color(0.17f, 0.09f, 0.07f);

    TutorialBookAsset asset;
    GameManager.ModalLock modalLock;
    int page;
    float turnLeft;
    int pendingPage;
    bool closeRequested;
    int startFrame;
    float lastScreenH = -1f;

    RectTransform bookRect;
    Image bookImage;
    TextMeshProUGUI leftText, rightText;
    Button prevButton, nextButton, closeButton;
    RectTransform prevRect, nextRect, closeRect;
    CanvasScaler scaler;

    public static TutorialBookPanel Open(TutorialBookAsset asset)
    {
        if (asset == null || asset.PageCount == 0 || IsOpen || !GameManager.Exists) return null;

        var canvas = PixelUi.NewCanvas("TutorialBook", 450);
        var panel = canvas.gameObject.AddComponent<TutorialBookPanel>();
        panel.modalLock = GameManager.Instance.LockInput();   // antes de armar la UI: si algo falla, OnDestroy lo suelta
        IsOpen = true;
        panel.asset = asset;
        panel.startFrame = Time.frameCount;
        panel.Build(canvas);
        panel.ShowPage(0);
        AudioManager.PlayOpenBook();
        return panel;
    }

    void OnDestroy()
    {
        IsOpen = false;
        modalLock?.Release();
    }

    void Build(Canvas canvas)
    {
        // 1 unidad de canvas = 1 pixel de pantalla: así todo se puede alinear a pixeles enteros
        scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        var dim = PixelUi.NewImage("Oscurecer", transform, new Color(0f, 0f, 0.03f, 0.62f));
        PixelUi.Stretch(dim.rectTransform);

        bookImage = PixelUi.NewImage("Libro", transform, Color.white);
        bookImage.sprite = asset.book;
        bookImage.raycastTarget = false;
        bookRect = bookImage.rectTransform;

        leftText = NewPageText("Pagina izquierda", bookRect);
        rightText = NewPageText("Pagina derecha", bookRect);

        prevButton = NewButton("Boton anterior", asset.previousButton, out prevRect);
        nextButton = NewButton("Boton siguiente", asset.nextButton, out nextRect);
        closeButton = NewButton("Boton cerrar", asset.closeButton, out closeRect);
        prevButton.onClick.AddListener(() => Turn(-1));
        nextButton.onClick.AddListener(() => Turn(+1));
        closeButton.onClick.AddListener(RequestClose);

        Layout();
    }

    TextMeshProUGUI NewPageText(string objectName, Transform parent)
    {
        var label = PixelUi.NewLabel(objectName, parent, "", BaseFont, Vector2.zero, Vector2.zero, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        label.color = Ink;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Truncate;
        label.enableAutoSizing = true;   // si el texto no cabe en la página, se achica en vez de salirse
        label.fontSizeMin = 18f;
        label.fontSizeMax = BaseFont;
        label.margin = Vector4.zero;
        return label;
    }

    Button NewButton(string objectName, Sprite sprite, out RectTransform rect)
    {
        var image = PixelUi.NewImage(objectName, transform, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        rect = image.rectTransform;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.86f, 0.86f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };   // las flechas del teclado pasan página, no mueven la selección
        return button;
    }

    /// <summary>Calcula la escala pixel perfect según el alto de pantalla y coloca libro, textos y botones.</summary>
    void Layout()
    {
        lastScreenH = Screen.height;

        // El libro más los botones de abajo ocupan ~1080 unidades del arte de alto; se deja un margen
        float fit = Screen.height * 0.92f / 1080f;
        float s = Mathf.Max(1f / 6f, Mathf.Floor(fit * 6f) / 6f);   // pasos de 1/6: cada pixel del arte (~6 px) = n pixeles de pantalla

        SetRect(bookRect, 0f, Mathf.Round(40f * s), ArtW * s, ArtH * s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        PlaceText(leftText, LeftText, s);
        PlaceText(rightText, RightText, s);
        leftText.fontSizeMax = Mathf.Round(BaseFont * s);
        rightText.fontSizeMax = leftText.fontSizeMax;
        leftText.fontSizeMin = Mathf.Max(8f, Mathf.Round(18f * s));
        rightText.fontSizeMin = leftText.fontSizeMin;

        // Botones: debajo del libro (anterior a la izquierda, siguiente a la derecha) y cerrar arriba a la derecha
        float below = -ArtH * 0.5f * s + Mathf.Round(40f * s);   // borde inferior del libro (en coordenadas de pantalla desde el centro)
        PlaceButton(prevRect, asset.previousButton, new Vector2(-ArtW * 0.17f * s, below - 70f * s), 150f * s);
        PlaceButton(nextRect, asset.nextButton, new Vector2(ArtW * 0.17f * s, below - 70f * s), 150f * s);
        PlaceButton(closeRect, asset.closeButton, new Vector2(ArtW * 0.5f * s - 70f * s, ArtH * 0.5f * s + Mathf.Round(40f * s) - 60f * s), 130f * s);
    }

    static void SetRect(RectTransform r, float x, float y, float w, float h, Vector2 anchor, Vector2 pivot)
    {
        PixelUi.Place(r, anchor, pivot, new Vector2(Mathf.Round(x), Mathf.Round(y)), new Vector2(Mathf.Round(w), Mathf.Round(h)));
    }

    /// <summary>Coloca el texto sobre la página (rect en pixeles del arte, origen arriba a la izquierda).</summary>
    static void PlaceText(TextMeshProUGUI label, Rect art, float s)
    {
        var r = label.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(Mathf.Round(art.x * s), -Mathf.Round(art.y * s));
        r.sizeDelta = new Vector2(Mathf.Round(art.width * s), Mathf.Round(art.height * s));
    }

    void PlaceButton(RectTransform r, Sprite sprite, Vector2 center, float height)
    {
        if (sprite == null) return;
        float aspect = sprite.rect.width / sprite.rect.height;
        // tamaño entero y par para que el centro caiga sobre un pixel
        float h = Mathf.Round(height / 2f) * 2f;
        float w = Mathf.Round(h * aspect / 2f) * 2f;
        SetRect(r, center.x, center.y, w, h, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
    }

    void ShowPage(int index)
    {
        page = Mathf.Clamp(index, 0, asset.PageCount - 1);
        var p = asset.pages[page];
        leftText.text = Compose(p.leftTitle, p.leftBody);
        rightText.text = Compose(p.rightTitle, p.rightBody);
        leftText.gameObject.SetActive(true);
        rightText.gameObject.SetActive(true);
        bookImage.sprite = asset.book;

        // El botón izquierdo se oculta en la primera hoja y el derecho en la última
        prevButton.gameObject.SetActive(page > 0);
        nextButton.gameObject.SetActive(page < asset.PageCount - 1);
    }

    static string Compose(string title, string body)
    {
        if (string.IsNullOrEmpty(title)) return body;
        return $"<size=118%><b>{title}</b></size>\n\n{body}";
    }

    void Turn(int direction)
    {
        if (turnLeft > 0f || closeRequested) return;
        int target = page + direction;
        if (target < 0 || target >= asset.PageCount) return;

        pendingPage = target;
        turnLeft = TurnSeconds;
        AudioManager.PlayOpenBook();

        // Mientras la hoja se levanta se ve la animación y el texto se esconde
        bookImage.sprite = direction > 0 ? asset.turnNext : asset.turnPrevious;
        leftText.gameObject.SetActive(false);
        rightText.gameObject.SetActive(false);
        prevButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
    }

    void RequestClose()
    {
        if (closeRequested) return;
        closeRequested = true;
        AudioManager.PlayOpenBook();
    }

    void Update()
    {
        if (closeRequested) return;

        if (!Mathf.Approximately(lastScreenH, Screen.height)) Layout();   // cambió el tamaño de la ventana

        if (turnLeft > 0f)
        {
            turnLeft -= Time.unscaledDeltaTime;
            if (turnLeft <= 0f) { turnLeft = 0f; ShowPage(pendingPage); }
            return;
        }

        if (Time.frameCount <= startFrame + 1) return;   // ignora la pulsación que abrió el libro

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) RequestClose();
        else if (kb.rightArrowKey.wasPressedThisFrame) Turn(+1);
        else if (kb.leftArrowKey.wasPressedThisFrame) Turn(-1);
    }

    // El cierre se hace al final del frame: así el Esc que cierra el libro no llega, en este mismo frame, al menú de pausa
    // (que solo ignora Esc mientras hay un bloqueo abierto).
    void LateUpdate()
    {
        if (!closeRequested) return;
        IsOpen = false;
        modalLock?.Release();
        modalLock = null;
        Destroy(gameObject);
        Closed?.Invoke();
    }
}
