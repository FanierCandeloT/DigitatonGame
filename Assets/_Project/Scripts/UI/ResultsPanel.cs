using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pantalla final de resultados (la abre el reto de los paneles solares al cerrar el diálogo final). Reúne el recuento de errores
/// de TODOS los retos (los mismos que usa el cuaderno: GameManager.RuleMistakes y los ":errors" de cada reto), calcula el nivel
/// con que el jugador maneja la voz pasiva y da una recomendación: qué reglas repasar (las que más falló, con su recordatorio)
/// y cómo seguir practicando. La interfaz se arma por código con PixelUi; el botón vuelve al menú principal.
/// </summary>
public class ResultsPanel : MonoBehaviour
{
    [Tooltip("Objeto de la pantalla provisional antigua (\"Results screen (pending)\"): se apaga, la nueva se arma por código.")]
    public GameObject root;
    public string mainMenuScene = "MainMenu";

    static readonly Color Terracotta = new Color(0.72f, 0.29f, 0.15f);
    static readonly Color Orange = new Color(0.96f, 0.52f, 0.12f);
    static readonly Color Cream = new Color(1f, 0.93f, 0.76f);
    static readonly Color DarkWarm = new Color(0.09f, 0.05f, 0.04f, 0.97f);

    /// <summary>Retos que cuentan en el resultado, en el orden de la historia: id y nombre.</summary>
    static readonly (string id, string title)[] Challenges =
    {
        ("q1_4", "Lucía's neurons"),
        ("r2_litter", "The bottles"),
        ("r3_exams", "The exams"),
        ("class1", "The last class"),
        ("r6_solar", "The blackout"),
    };

    /// <summary>Nivel con que el jugador maneja el tema, con su resumen y su consejo.</summary>
    public readonly struct LevelInfo
    {
        public readonly string Name, Summary, Advice;
        public readonly Color Color;
        public LevelInfo(string name, Color color, string summary, string advice)
        {
            Name = name; Color = color; Summary = summary; Advice = advice;
        }
    }

    static readonly LevelInfo Master = new LevelInfo("ADVANCED  (B2)", new Color(0.4f, 0.95f, 0.62f),
        "You handle the passive voice with confidence.",
        "Challenge yourself: rewrite news headlines in the passive, and practice other tenses (past perfect, future perfect).");
    static readonly LevelInfo Confident = new LevelInfo("CONFIDENT  (B1+)", new Color(0.55f, 0.85f, 0.4f),
        "You build correct passive sentences most of the time.",
        "Practice the rules you missed above, and try to name the doer with 'by' whenever it matters.");
    static readonly LevelInfo Developing = new LevelInfo("DEVELOPING  (B1)", new Color(1f, 0.78f, 0.34f),
        "You know the idea, but some details still slip.",
        "Review the rules above, then rewrite five active sentences from your day in the passive, checking 'be' and the participle.");
    static readonly LevelInfo Starting = new LevelInfo("GETTING STARTED  (A2+)", new Color(1f, 0.55f, 0.35f),
        "The passive voice is still new to you, and that is fine.",
        "Start with the pattern be + past participle (is made, was written). Replay the challenges and read the hints carefully.");

    /// <summary>Nivel de un jugador a partir del total de errores (público para poder probarlo).</summary>
    public static LevelInfo LevelFor(int totalMistakes)
    {
        if (totalMistakes <= 2) return Master;
        if (totalMistakes <= 7) return Confident;
        if (totalMistakes <= 14) return Developing;
        return Starting;
    }

    GameObject canvasObject;
    Button backButton;
    bool leaving;

    void Start()
    {
        if (root != null) root.SetActive(false);
    }

    void Update()
    {
        if (canvasObject == null || leaving) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            GoToMenu();
    }

    public void Show()
    {
        Hide();
        if (root != null) root.SetActive(false);
        leaving = false;
        Build(GameManager.Instance);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }

    public void Hide()
    {
        if (canvasObject != null) Destroy(canvasObject);
        canvasObject = null;
    }

    void GoToMenu()
    {
        if (leaving) return;
        leaving = true;
        Hide();
        GameManager.Instance.CloseResults();
        SceneTransition.Go(mainMenuScene, null);
    }

    // ───────────── Contenido ─────────────

    static int Mistakes(GameManager gm, string id) => gm.GetStat(id + ":errors");

    void Build(GameManager gm)
    {
        int total = Challenges.Sum(c => Mistakes(gm, c.id));
        var level = LevelFor(total);

        var canvas = PixelUi.NewCanvas("ResultsScreen", 600);
        canvasObject = canvas.gameObject;

        var dim = PixelUi.NewImage("Fondo", canvas.transform, new Color(0f, 0f, 0f, 0.78f));
        PixelUi.Stretch(dim.rectTransform);

        var border = PixelUi.NewPanel("Ventana", canvas.transform, Terracotta, DarkWarm, out _);
        PixelUi.Place(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 660f));
        var window = border.transform;

        Text(window, "Results", 40f, new Vector2(0f, 300f), new Vector2(880f, 50f), Cream, FontStyles.Bold);

        // Nivel
        Text(window, "YOUR PASSIVE VOICE LEVEL", 18f, new Vector2(0f, 258f), new Vector2(880f, 26f), Orange, FontStyles.Normal);
        Text(window, level.Name, 36f, new Vector2(0f, 222f), new Vector2(880f, 46f), level.Color, FontStyles.Bold);
        Text(window, level.Summary, 20f, new Vector2(0f, 186f), new Vector2(880f, 28f), Cream, FontStyles.Normal);

        Line(window, 160f);

        // Errores por reto (izquierda)
        Text(window, "MISTAKES PER CHALLENGE", 18f, new Vector2(-230f, 140f), new Vector2(420f, 26f), Orange, FontStyles.Normal, TextAlignmentOptions.Left);
        var rows = new StringBuilder();
        foreach (var c in Challenges)
        {
            if (!gm.IsChallengeDone(c.id)) { rows.Append($"{c.title}: <color=#C7B899>not completed</color>\n"); continue; }
            int n = Mistakes(gm, c.id);
            string mark = n == 0 ? "<color=#66F29E>no mistakes</color>" : $"<color=#FFC757>{n} mistake{(n == 1 ? "" : "s")}</color>";
            rows.Append($"{c.title}: {mark}\n");
        }
        rows.Append($"\n<color=#F58A1F>Total:</color> {total}   <color=#F58A1F>Memories:</color> {gm.MemoriesRecovered}/{GameManager.MaxMemories}");
        Text(window, rows.ToString(), 20f, new Vector2(-230f, 6f), new Vector2(420f, 220f), Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft, rich: true);

        // Reglas a repasar (derecha)
        Text(window, "RULES TO REVIEW", 18f, new Vector2(230f, 140f), new Vector2(420f, 26f), Orange, FontStyles.Normal, TextAlignmentOptions.Left);
        Text(window, ReviewText(gm), 17f, new Vector2(230f, 6f), new Vector2(420f, 220f), Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft, rich: true);

        Line(window, -96f);

        // Recomendación
        Text(window, "WHAT TO DO NEXT", 18f, new Vector2(0f, -116f), new Vector2(880f, 26f), Orange, FontStyles.Normal);
        Text(window, level.Advice, 20f, new Vector2(0f, -170f), new Vector2(860f, 74f), Cream, FontStyles.Normal, TextAlignmentOptions.Top);

        // Botón
        var button = PixelUi.NewPanel("Boton", window, Terracotta, new Color(0.2f, 0.09f, 0.05f), out _);
        PixelUi.Place(button.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 56f));
        backButton = button.gameObject.AddComponent<Button>();
        backButton.targetGraphic = button;
        backButton.onClick.AddListener(GoToMenu);
        Text(button.transform, "Main Menu", 24f, Vector2.zero, new Vector2(280f, 40f), Cream, FontStyles.Bold);
    }

    /// <summary>Las reglas que más se fallaron (hasta tres), cada una con su recordatorio; o una felicitación si no hubo errores.</summary>
    static string ReviewText(GameManager gm)
    {
        var top = gm.RuleMistakes.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(3).ToList();
        if (top.Count == 0)
            return "<color=#66F29E>Nothing to review!</color>\nEvery rule was applied correctly: receiver first, passive verb (be + participle) and 'by + doer'.";

        var sb = new StringBuilder();
        foreach (var pair in top)
            sb.Append($"<color=#FFC757>{pair.Key}</color> (x{pair.Value})\n{ErrorHuntContent.Reminder(pair.Key)}\n\n");
        return sb.ToString().TrimEnd();
    }

    static TextMeshProUGUI Text(Transform parent, string text, float size, Vector2 position, Vector2 dimensions, Color color,
        FontStyles style, TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool rich = false)
    {
        var label = PixelUi.NewLabel("Texto", parent, text, size, position, dimensions, style, alignment);
        PixelUi.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, dimensions);
        label.color = color;
        label.richText = rich || text.Contains("<color");
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    static void Line(Transform parent, float y)
    {
        var line = PixelUi.NewImage("Linea", parent, new Color(Orange.r, Orange.g, Orange.b, 0.85f));
        PixelUi.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(860f, 2f));
        line.raycastTarget = false;
    }
}
