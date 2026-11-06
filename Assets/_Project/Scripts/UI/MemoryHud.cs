using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Panel de progreso en la esquina superior derecha: "Memories: X/12", el estado del habla de Andrés
/// ("SPEECH 33% - confused") y el objetivo actual. Cuando el habla mejora de etapa avisa con un mensaje, cuando ya no hay
/// diálogos abiertos. Solo se ve durante el juego (cuando la escena tiene a Andrés): no aparece en el menú ni en el tutorial,
/// y se esconde mientras el juego está en pausa (el menú de pausa difumina todo lo que hay detrás).
/// Estilo del menú: borde terracota de 2 px, fondo oscuro cálido, Pixelify Sans, textos crema y naranja.
/// El tamaño del panel se calcula a partir del texto del objetivo, así que todo cabe dentro del recuadro.
/// </summary>
public class MemoryHud : MonoBehaviour
{
    public TMP_Text label;
    public GameObject root;
    [Tooltip("Fuente Pixelify Sans (Art/UI/Fonts). Vacío = la fuente que ya tenga el contador.")]
    public TMP_FontAsset pixelFont;

    static readonly string[] HiddenScenes = { "MainMenu", "Tutorial" };

    static readonly Color Terracotta = new Color(0.72f, 0.29f, 0.15f);
    static readonly Color Orange = new Color(0.96f, 0.52f, 0.12f);
    static readonly Color Cream = new Color(1f, 0.93f, 0.76f);
    static readonly Color DarkWarm = new Color(0.09f, 0.05f, 0.04f, 0.92f);

    const float PanelWidth = 380f;
    const float Pad = 14f;
    const float InnerWidth = PanelWidth - 2f * Pad;
    const float ObjectiveTop = 84f;      // distancia del borde superior hasta el objetivo (contador + habla + línea)

    TextMeshProUGUI speechLabel, objectiveLabel;
    RectTransform separator;
    int lastMemories;

    void OnEnable()
    {
        GameManager.MemoriesChanged += OnMemoriesChanged;
        GameManager.ProgressChanged += Refresh;
        GameManager.PauseChanged += OnPauseChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameManager.MemoriesChanged -= OnMemoriesChanged;
        GameManager.ProgressChanged -= Refresh;
        GameManager.PauseChanged -= OnPauseChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        BuildPanel();
        lastMemories = GameManager.Instance.MemoriesRecovered;
        Refresh();
    }

    void OnMemoriesChanged(int value)
    {
        string message = SpeechRecovery.ImprovementMessage(lastMemories, value);
        lastMemories = value;
        if (message != null) StartCoroutine(AnnounceWhenFree(message));
        Refresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();
    void OnPauseChanged(bool paused) => Refresh();

    /// <summary>Arma el panel: borde terracota, relleno oscuro, contador, habla, línea separadora y objetivo.</summary>
    void BuildPanel()
    {
        var font = pixelFont != null ? pixelFont : label.font;

        // Borde de 2 px (la raíz) y relleno oscuro cálido, sin bordes suaves
        var rootImage = root.GetComponent<Image>();
        rootImage.sprite = null;
        rootImage.type = Image.Type.Simple;
        rootImage.color = Terracotta;
        rootImage.raycastTarget = false;
        var existing = root.transform.Find("Relleno");
        if (existing != null) Destroy(existing.gameObject);
        var fill = NewImage("Relleno", root.transform, DarkWarm);
        fill.transform.SetAsFirstSibling();
        Stretch(fill.rectTransform, 2f);
        fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-20f, -20f);
        rootRect.sizeDelta = new Vector2(PanelWidth, ObjectiveTop);

        // Contador
        Style(label, font, 26f, Cream, TextAlignmentOptions.Left, FontStyles.Normal);
        PlaceTopLeft(label.rectTransform, new Vector2(Pad, -10f), new Vector2(InnerWidth, 32f));

        // Habla de Andrés
        speechLabel = NewLabel("Habla");
        Style(speechLabel, font, 18f, Orange, TextAlignmentOptions.Left, FontStyles.Normal);
        PlaceTopLeft(speechLabel.rectTransform, new Vector2(Pad, -44f), new Vector2(InnerWidth, 24f));

        // Línea separadora naranja
        var line = NewImage("Linea", root.transform, new Color(Orange.r, Orange.g, Orange.b, 0.85f));
        separator = line.rectTransform;
        PlaceTopLeft(separator, new Vector2(Pad, -74f), new Vector2(InnerWidth, 2f));

        // Objetivo actual
        objectiveLabel = NewLabel("Objetivo");
        Style(objectiveLabel, font, 18f, Cream, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        objectiveLabel.textWrappingMode = TextWrappingModes.Normal;
        objectiveLabel.richText = true;
        PlaceTopLeft(objectiveLabel.rectTransform, new Vector2(Pad, -ObjectiveTop + 4f), new Vector2(InnerWidth, 40f));

        // Contorno oscuro (el modo raster no lo trae)
        PixelTextOutline.Build(label, 1f);
        PixelTextOutline.Build(speechLabel, 1f);
        PixelTextOutline.Build(objectiveLabel, 1f);
    }

    static void Style(TMP_Text text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.fontStyle = style;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    TextMeshProUGUI NewLabel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void PlaceTopLeft(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    void Refresh()
    {
        var gm = GameManager.Instance;
        label.text = $"Memories: {gm.MemoriesRecovered}/{GameManager.MaxMemories}";

        if (speechLabel != null)
        {
            var stage = SpeechRecovery.Stage;
            speechLabel.text = $"SPEECH {Mathf.RoundToInt(SpeechRecovery.Progress * 100f)}% - {SpeechRecovery.StageName(stage)}";
            speechLabel.color = SpeechRecovery.StageColor(stage);
        }

        if (objectiveLabel != null)
        {
            string objective = StoryFlow.CurrentObjective(gm);
            string text = string.IsNullOrEmpty(objective) ? "" : $"<color=#F58A1F>OBJECTIVE</color>\n{objective}";
            if (gm.HasFlag(GameManager.FlagNotebookGiven))
                text += (text.Length > 0 ? "\n" : "") + "<color=#F58A1F>[C]</color> Notebook";
            if (text != objectiveLabel.text)
            {
                objectiveLabel.text = text;
                separator.gameObject.SetActive(text.Length > 0);

                // El texto se mide con la fuente real y el ancho interior: el recuadro crece lo justo y NADA se sale
                float textHeight = 0f;
                if (text.Length > 0)
                {
                    objectiveLabel.ForceMeshUpdate();
                    textHeight = Mathf.Ceil(objectiveLabel.GetPreferredValues(text, InnerWidth, 0f).y);
                    objectiveLabel.rectTransform.sizeDelta = new Vector2(InnerWidth, textHeight + 2f);
                }
                float height = text.Length == 0 ? 64f : ObjectiveTop + 4f + textHeight + Pad;
                ((RectTransform)root.transform).sizeDelta = new Vector2(PanelWidth, height);
            }
        }

        bool inGame = FindAnyObjectByType<PlayerController>() != null &&
                      System.Array.IndexOf(HiddenScenes, SceneManager.GetActiveScene().name) < 0;
        // En pausa se esconde: el menú de pausa difumina el juego y el panel no debe quedar encima
        root.SetActive(inGame && !gm.IsPaused);
    }

    /// <summary>Espera a que se cierren diálogos y minijuegos para que el aviso no tape ni sea tapado.</summary>
    IEnumerator AnnounceWhenFree(string message)
    {
        yield return null;
        while (GameManager.Instance.ModalOpen || GameManager.Instance.IsTransitioning)
            yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
        ToastMessage.Show(message, 3.5f);
    }
}
