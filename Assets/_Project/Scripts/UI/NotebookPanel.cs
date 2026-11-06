using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cuaderno del profe: se abre y cierra con C. Lucía se lo entrega a Andrés al terminar el primer reto
/// (hasta entonces C no hace nada, salvo en el tutorial y en modo desarrollo). Tiene la lista de objetivos
/// de la historia y, al hacer clic en un reto completado, un panel con su retroalimentación.
/// La interfaz se arma por código al abrirlo; mientras está abierto Andrés no se mueve.
/// </summary>
public class NotebookPanel : MonoBehaviour
{
    public GameObject panel;

    public static event Action Opened;
    public static event Action Closed;

    static readonly Color Ink = new Color(0.16f, 0.11f, 0.07f);
    static readonly Color Paper = new Color(0.95f, 0.90f, 0.76f);
    static readonly Color Frame = new Color(0.30f, 0.18f, 0.08f);
    static readonly Color RowIdle = new Color(0.88f, 0.80f, 0.62f);
    static readonly Color RowDone = new Color(0.66f, 0.82f, 0.58f);
    static readonly Color RowCurrent = new Color(1f, 0.85f, 0.35f);
    static readonly Color RowSelectedOutline = new Color(0.12f, 0.35f, 0.75f);

    RectTransform content;
    TextMeshProUGUI detailTitle, detailBody, hint;
    string selectedId;
    bool firstOpenHint;

    public bool IsOpen => panel != null && panel.activeSelf;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameManager.ProgressChanged += OnProgressChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameManager.ProgressChanged -= OnProgressChanged;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Close();

    void OnProgressChanged()
    {
        if (IsOpen) Rebuild();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.cKey.wasPressedThisFrame) return;

        if (IsOpen) Close();
        else if (CanOpen()) Open();
    }

    /// <summary>True si Andrés ya tiene el cuaderno (o si es el tutorial o modo desarrollo).</summary>
    public static bool Unlocked()
    {
        var gm = GameManager.Instance;
        return gm.HasFlag(GameManager.FlagNotebookGiven) || gm.HasFlag(GameManager.FlagDevOpen) ||
               SceneManager.GetActiveScene().name == "Tutorial";
    }

    static bool CanOpen()
    {
        var gm = GameManager.Instance;
        // Solo dentro del juego: hace falta que haya jugador, y no en pausa ni en un fundido.
        return Unlocked() && !gm.InputLocked && FindAnyObjectByType<PlayerController>() != null;
    }

    /// <summary>Lo llama Lucía al entregarlo: marca el cuaderno como obtenido y lo abre con el mini tutorial.</summary>
    public static void GiveToPlayer()
    {
        GameManager.Instance.SetFlag(GameManager.FlagNotebookGiven);
        var notebook = FindAnyObjectByType<NotebookPanel>(FindObjectsInactive.Include);
        if (notebook == null) return;
        notebook.firstOpenHint = true;
        notebook.selectedId = StoryFlow.Steps[0].Id;
        notebook.Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        panel.SetActive(true);
        GameManager.Instance.NotebookOpen = true;
        Rebuild();
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        firstOpenHint = false;
        GameManager.Instance.NotebookOpen = false;
        Closed?.Invoke();
    }

    // ───────────── Interfaz ─────────────

    void Rebuild()
    {
        // Se descarta el contenido viejo del prefab ("Book": solo el título) y el anterior de esta clase
        foreach (Transform child in panel.transform)
            child.gameObject.SetActive(false);
        if (content != null) Destroy(content.gameObject);

        var border = PixelUi.NewPanel("Cuaderno", panel.transform, Frame, Paper, out var fill);
        content = border.rectTransform;
        PixelUi.Place(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 620f));

        var title = PixelUi.NewLabel("Titulo", fill.transform, "PROFESSOR'S NOTEBOOK", 40f, Vector2.zero,
            new Vector2(900f, 56f), FontStyles.Bold);
        title.color = Ink;
        PixelUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(900f, 56f));

        // Lista de objetivos a la izquierda
        var gm = GameManager.Instance;
        string currentId = null;
        float y = -84f;
        foreach (var step in StoryFlow.Steps)
        {
            if (!step.Built) continue;
            bool done = gm.IsChallengeDone(step.Id);
            if (!done && currentId == null) currentId = step.Id;
            bool isCurrent = currentId == step.Id;
            if (selectedId == null && isCurrent) selectedId = step.Id;

            BuildRow(fill.transform, step, done, isCurrent, y);
            y -= 84f;
        }
        if (selectedId == null) selectedId = StoryFlow.Steps[0].Id;

        // Detalle a la derecha
        var detail = PixelUi.NewPanel("Detalle", fill.transform, Frame, new Color(1f, 0.98f, 0.9f), out var detailFill);
        PixelUi.Place(detail.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -84f), new Vector2(570f, 450f));
        detailTitle = PixelUi.NewLabel("DetalleTitulo", detailFill.transform, "", 30f, Vector2.zero, Vector2.zero,
            FontStyles.Bold, TextAlignmentOptions.TopLeft);
        detailTitle.color = Ink;
        PixelUi.Place(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(530f, 44f));
        detailBody = PixelUi.NewLabel("DetalleTexto", detailFill.transform, "", 24f, Vector2.zero, Vector2.zero,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        detailBody.color = Ink;
        detailBody.textWrappingMode = TextWrappingModes.Normal;
        PixelUi.Place(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -62f), new Vector2(530f, 370f));

        hint = PixelUi.NewLabel("Pista", fill.transform, "", 22f, Vector2.zero, Vector2.zero, FontStyles.Bold, TextAlignmentOptions.Center);
        hint.color = new Color(0.12f, 0.35f, 0.75f);
        PixelUi.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(980f, 34f));
        hint.text = firstOpenHint
            ? "Lucía: write down what you remember. C opens / closes this notebook."
            : "Click a task to see it.   C or Esc: close";

        ShowDetail(selectedId);
    }

    void BuildRow(Transform parent, StoryFlow.Step step, bool done, bool current, float y)
    {
        var color = done ? RowDone : current ? RowCurrent : RowIdle;
        var row = PixelUi.NewPanel("Fila_" + step.Id, parent, selectedId == step.Id ? RowSelectedOutline : Frame, color, out var rowFill);
        PixelUi.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(390f, 72f));

        string mark = done ? "[x]" : current ? "[>]" : "[ ]";
        var label = PixelUi.NewLabel("Texto", rowFill.transform, $"{mark} {step.Title}", 26f, Vector2.zero, Vector2.zero,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        label.color = Ink;
        PixelUi.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(14f, 0f);

        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        string id = step.Id;
        button.onClick.AddListener(() =>
        {
            selectedId = id;
            Rebuild();
        });
    }

    void ShowDetail(string id)
    {
        StoryFlow.Step step = null;
        foreach (var s in StoryFlow.Steps)
            if (s.Id == id) step = s;
        if (step == null) return;

        var gm = GameManager.Instance;
        detailTitle.text = step.Title;
        if (gm.IsChallengeDone(id))
            detailBody.text = $"<b>Completed.</b>\n\n{FeedbackFor(gm, id)}";
        else
            detailBody.text = $"<b>Objective:</b> {step.Objective}\n\nComplete this task to unlock your feedback here.";
    }

    /// <summary>Feedback guardado por el reto; si no guardó uno, un resumen con sus cifras.</summary>
    static string FeedbackFor(GameManager gm, string id)
    {
        string saved = gm.GetFeedback(id);
        if (!string.IsNullOrEmpty(saved)) return saved;

        var sb = new StringBuilder();
        if (gm.HasStat(id + ":score")) sb.AppendLine($"Score: {gm.GetStat(id + ":score")}/{gm.GetStat(id + ":max")}");
        if (gm.HasStat(id + ":attempts")) sb.AppendLine($"Attempts: {gm.GetStat(id + ":attempts")}");
        if (gm.HasStat(id + ":errors"))
        {
            int errors = gm.GetStat(id + ":errors");
            sb.AppendLine(errors == 0 ? "No mistakes. Great work!" : $"Mistakes along the way: {errors}. Each one was a chance to review the rule: be + past participle, and by + agent.");
        }
        return sb.Length > 0 ? sb.ToString() : "Well done. The passive voice was used correctly here.";
    }
}
