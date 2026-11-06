using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Reto 3 (ODS 8 y 12): los exámenes de los estudiantes. Andrés encuentra una pila de exámenes escritos en voz ACTIVA
/// y, como en este país todo es pasivo, los "opera" con la mecánica de cirugía de frases (ExamSurgery): intercambia el
/// receptor y el hacedor, elige el verbo pasivo y sella el agente con "by". Después de cada frase, el profesor hace una
/// pregunta sobre POR QUÉ y CUÁNDO se usa la pasiva (variedad de preguntas, no solo transformar).
/// Arriba se ve el "libro de consulta": al principio solo se lee la primera regla y las demás están borrosas; cada examen
/// aprobado aclara una página. Los errores se explican uno por uno (orden, verbo, agente) y el resumen queda en el cuaderno.
/// Andrés queda quieto mientras dura el reto (bloqueo de ExamSurgery).
/// </summary>
public class ExamChallenge : Interactable
{
    [Tooltip("Id del reto (marca 'done:r3_exams' al completarlo).")]
    public string challengeId = "r3_exams";

    [Min(0)] public int memoryReward = 2;

    [Tooltip("Icono '!' sobre la pila de exámenes (se apaga al completar el reto).")]
    public GameObject icon;

    /// <summary>Páginas del libro: la primera se lee desde el inicio; cada examen aprobado aclara la siguiente.</summary>
    static readonly string[] BookRules =
    {
        "1. Passive = be + past participle (is / are / was / were + paid).",
        "2. The RECEIVER of the action goes first. The doer goes after 'by' (and can be left out if unknown).",
        "3. 'Be' matches the new subject and the tense: have / has + BEEN, modal + BE.",
    };

    static readonly Color Frame = new Color(1f, 0.82f, 0.45f);
    static readonly Color Good = new Color(0.4f, 0.95f, 0.62f);

    // ───────────── Estado ─────────────

    PlayerInteractor lastInteractor;
    bool running;
    int cleared = 1;              // páginas del libro legibles
    TextMeshProUGUI[] bookLines;
    GameObject bookCanvas;

    void Start()
    {
        if (GameManager.Instance.IsChallengeDone(challengeId)) ApplyDoneLook();
    }

    void OnDestroy()
    {
        if (bookCanvas != null) Destroy(bookCanvas);
    }

    // ───────────── Diálogos ─────────────

    static DialogueLine[] IntroLines() => new[]
    {
        new DialogueLine("Andrés",
            "These are my students' exams. Their sentences are all in the active voice...",
            "{These are my students' exams}. Todas sus frases están en {active voice}...",
            "Estos son los exámenes de mis estudiantes. Todas sus frases están en voz activa..."),
        new DialogueLine("Andrés",
            "In this country, they must be passive. Let me fix them. My book will help me... if I can read it.",
            "En este país todo debe ser {passive}. Voy a {fix them}. Mi {book} me va a ayudar... si puedo leerlo.",
            "En este país deben ser pasivas. Voy a corregirlas. Mi libro me ayudará... si puedo leerlo."),
    };

    static DialogueLine[] ClosingLines() => new[]
    {
        new DialogueLine("Andrés",
            "I taught them this. Decent work. Responsible production. I remember why.",
            "Yo les enseñé esto: {decent work}, {responsible production}. Ya recuerdo por qué.",
            "Yo les enseñé esto. Trabajo decente. Producción responsable. Ya recuerdo por qué."),
        new DialogueLine("Andrés",
            "In every sentence, somebody does something. I only need to remember who...",
            "En cada frase, {somebody does something}. Solo necesito recordar {who}...",
            "En cada frase, alguien hace algo. Solo necesito recordar quién..."),
    };

    // ───────────── Flujo ─────────────

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;
        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show("The exams are graded. Every sentence has a doer!", 2.5f);
            return;
        }
        if (running) return;

        running = true;
        lastInteractor = interactor;
        cleared = 1;
        DialoguePanel.Show(IntroLines(), OpenSurgery);
    }

    void OpenSurgery()
    {
        BuildBook();
        ExamSurgery.Open(OnExamPassed, Finish, OnLeave);
    }

    void OnLeave()
    {
        running = false;
        if (bookCanvas != null) Destroy(bookCanvas);
    }

    void OnExamPassed(int index)
    {
        bool last = index >= ExamSurgery.ExamCount - 1;
        cleared = last ? BookRules.Length : Mathf.Min(BookRules.Length, cleared + 1);
        RefreshBook(true);
        ToastMessage.Show(last ? "Graded! The whole book is readable now." : "Graded! A page of the book became clear.", 2f);
    }

    void Finish(SurgeryResult result)
    {
        if (bookCanvas != null) Destroy(bookCanvas);

        var gm = GameManager.Instance;
        gm.CompleteChallenge(challengeId, memoryReward);
        gm.SetStat(challengeId + ":attempts", result.Attempts);
        gm.SetStat(challengeId + ":errors", result.Errors);
        gm.SetFeedback(challengeId, BuildFeedback(result));
        ApplyDoneLook();
        base.Interact(lastInteractor);
        running = false;
        DialoguePanel.Show(ClosingLines(), null);
    }

    /// <summary>Texto del cuaderno: qué reglas costaron más (cada una con su recordatorio) o felicitación si no hubo fallos.</summary>
    static string BuildFeedback(SurgeryResult result)
    {
        if (result.Errors == 0)
            return $"You rewrote all {ExamSurgery.ExamCount} exams without a mistake: receiver first, passive verb (be + participle) and 'by + doer'.";

        var sb = new StringBuilder($"{ExamSurgery.ExamCount} exams graded in {result.Attempts} tries, with {result.Errors} mistake(s). Rules to review:\n");
        foreach (var pair in result.Weak)
            sb.Append("\n- ").Append(pair.Key).Append(" (x").Append(pair.Value).Append("): ").Append(Reminder(pair.Key));
        return sb.ToString();
    }

    static string Reminder(string label)
    {
        if (label == ErrorHuntContent.Label(ErrorKind.Agreement)) return "'be' must match the new subject (is / are, was / were).";
        if (label == ErrorHuntContent.Label(ErrorKind.Tense)) return "keep the tense of the active verb (present, past, present perfect).";
        if (label == ErrorHuntContent.Label(ErrorKind.Participle)) return "after 'be' use the past participle (written, taught, worn).";
        if (label == ErrorHuntContent.Label(ErrorKind.Agent)) return "'by' introduces the doer.";
        if (label == ErrorHuntContent.Label(ErrorKind.MissingBe)) return "do not forget 'be' (modal + be, have + been).";
        if (label == "Word order") return "the receiver goes first and 'by + doer' goes last.";
        return "the passive focuses on the receiver; the doer can be left out when it is unknown or unimportant.";
    }

    void ApplyDoneLook()
    {
        if (icon != null) icon.SetActive(false);
        if (TryGetComponent(out SpriteRenderer sr)) sr.color = new Color(0.55f, 0.6f, 0.6f);
    }

    // ───────────── Libro de consulta (arriba a la izquierda) ─────────────

    void BuildBook()
    {
        if (bookCanvas != null) Destroy(bookCanvas);

        var canvas = PixelUi.NewCanvas("ExamBook", 451);   // sobre el fondo del examen
        bookCanvas = canvas.gameObject;

        var panel = PixelUi.NewPanel("Libro", canvas.transform, Frame, new Color(0.12f, 0.09f, 0.05f, 0.97f), out _);
        PixelUi.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(600f, 216f));

        var title = PixelUi.NewLabel("Titulo", panel.transform, "TEACHER'S BOOK", 22, new Vector2(0f, 88f), new Vector2(560f, 30f), FontStyles.Bold);
        title.color = Frame;

        bookLines = new TextMeshProUGUI[BookRules.Length];
        for (int i = 0; i < BookRules.Length; i++)
        {
            var line = PixelUi.NewLabel("Regla", panel.transform, "", 19, new Vector2(0f, 38f - i * 56f), new Vector2(560f, 52f), FontStyles.Normal);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.alignment = TextAlignmentOptions.Left;
            bookLines[i] = line;
        }
        RefreshBook(false);
    }

    void RefreshBook(bool flashNewest)
    {
        if (bookLines == null) return;
        for (int i = 0; i < bookLines.Length; i++)
        {
            if (bookLines[i] == null) continue;
            bool readable = i < cleared;
            bookLines[i].text = readable ? BookRules[i] : Blur(BookRules[i]);
            bookLines[i].color = readable ? (flashNewest && i == cleared - 1 && cleared > 1 ? Good : Color.white)
                                          : new Color(0.55f, 0.5f, 0.42f);
        }
    }

    /// <summary>Texto "borroso": cada letra pasa a un bloque (se conserva la forma de las palabras).</summary>
    static string Blur(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text) sb.Append(char.IsLetterOrDigit(c) ? '•' : c);
        return sb.ToString();
    }
}
