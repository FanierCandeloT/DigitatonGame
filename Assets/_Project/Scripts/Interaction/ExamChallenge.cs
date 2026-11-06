using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Reto 3 (ODS 8 y 12): los exámenes de los estudiantes. Andrés encuentra una pila de exámenes escritos en voz ACTIVA
/// y, como en este país todo es pasivo, debe reescribirlos en pasiva con bloques (sujeto + verbo + agente).
/// Arriba se ve el "libro de consulta" del profesor: al principio solo se lee la primera regla y las demás están
/// borrosas; cada examen aprobado aclara una página, así que el libro se va completando a medida que Andrés recuerda.
/// Si se equivoca, el panel dice qué pasó, da una guía (sin la respuesta; desde el tercer intento explica la regla)
/// y deja reintentar con las mismas elecciones. Andrés queda quieto mientras dura el reto (bloqueo del SentenceBuilder).
/// </summary>
public class ExamChallenge : Interactable
{
    [Tooltip("Id del reto (marca 'done:r3_exams' al completarlo).")]
    public string challengeId = "r3_exams";

    [Min(0)] public int memoryReward = 2;

    [Tooltip("Icono '!' sobre la pila de exámenes (se apaga al completar el reto).")]
    public GameObject icon;

    // ───────────── Contenido ─────────────

    /// <summary>Un examen: la frase activa y, por fila, las opciones con su error (None = correcta).</summary>
    class Exam
    {
        public string Active;
        public string Topic;                      // ODS al que apunta la frase
        public string[] Subject; public int SubjectOk;
        public string[] Verb; public ErrorKind[] VerbKind; public int VerbOk;
        public string[] Agent; public ErrorKind[] AgentKind; public int AgentOk;
    }

    static readonly Exam[] Exams =
    {
        new Exam
        {
            Active = "The company pays the workers.", Topic = "SDG 8 - decent work",
            Subject = new[] { "The workers", "The company", "The salary", "The exam" }, SubjectOk = 0,
            Verb = new[] { "are paid", "is paid", "are paying", "paid" },
            VerbKind = new[] { ErrorKind.None, ErrorKind.Agreement, ErrorKind.Participle, ErrorKind.MissingBe }, VerbOk = 0,
            Agent = new[] { "by the company", "by the workers", "for the company" },
            AgentKind = new[] { ErrorKind.None, ErrorKind.None, ErrorKind.Agent }, AgentOk = 0,
        },
        new Exam
        {
            Active = "Students recycled the bottles.", Topic = "SDG 12 - responsible consumption",
            Subject = new[] { "The bottles", "Students", "The recycling", "The bin" }, SubjectOk = 0,
            Verb = new[] { "were recycled", "are recycled", "was recycled", "were recycling" },
            VerbKind = new[] { ErrorKind.None, ErrorKind.Tense, ErrorKind.Agreement, ErrorKind.Participle }, VerbOk = 0,
            Agent = new[] { "by students", "by the bottles", "to students" },
            AgentKind = new[] { ErrorKind.None, ErrorKind.None, ErrorKind.Agent }, AgentOk = 0,
        },
        new Exam
        {
            Active = "The factory wastes energy.", Topic = "SDG 12 - responsible production",
            Subject = new[] { "Energy", "The factory", "The waste", "The workers" }, SubjectOk = 0,
            Verb = new[] { "is wasted", "are wasted", "was wasted", "is wasting" },
            VerbKind = new[] { ErrorKind.None, ErrorKind.Agreement, ErrorKind.Tense, ErrorKind.Participle }, VerbOk = 0,
            Agent = new[] { "by the factory", "by energy", "from the factory" },
            AgentKind = new[] { ErrorKind.None, ErrorKind.None, ErrorKind.Agent }, AgentOk = 0,
        },
    };

    /// <summary>Páginas del libro: la primera se lee desde el inicio; cada examen aprobado aclara la siguiente.</summary>
    static readonly string[] BookRules =
    {
        "1. Passive = be + past participle (is / are / was / were + paid).",
        "2. The new subject is the one that RECEIVES the action. The doer goes after 'by'.",
        "3. 'Be' must match the new subject (singular / plural) and the time of the original verb.",
    };

    static readonly Color Warn = new Color(1f, 0.78f, 0.34f);
    static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
    static readonly Color Good = new Color(0.4f, 0.95f, 0.62f);
    static readonly Color Frame = new Color(1f, 0.82f, 0.45f);

    // ───────────── Estado ─────────────

    SentenceBuilder builder;
    PlayerInteractor lastInteractor;
    bool running;
    int current;                  // examen actual
    int failsOnCurrent;           // intentos fallidos en el examen actual
    int attempts, errors;
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
            "Estos son los {exams} de mis {students}. Todas sus frases están en {active voice}...",
            "Estos son los exámenes de mis estudiantes. Todas sus frases están en voz activa..."),
        new DialogueLine("Andrés",
            "In this country, they must be passive. Let me fix them. My book will help me... if I can read it.",
            "En este país deben ser {passive}. Voy a {fix} las frases. Mi {book} me ayudará... si puedo leerlo.",
            "En este país deben ser pasivas. Voy a corregirlas. Mi libro me ayudará... si puedo leerlo."),
    };

    static DialogueLine[] ClosingLines() => new[]
    {
        new DialogueLine("Andrés",
            "I taught them this. Decent work. Responsible production. I remember why.",
            "Yo les {taught} esto. Trabajo {decent}. Producción {responsible}. Ya recuerdo por qué.",
            "Yo les enseñé esto. Trabajo decente. Producción responsable. Ya recuerdo por qué."),
        new DialogueLine("Andrés",
            "In every sentence, somebody does something. I only need to remember who...",
            "En cada {sentence}, alguien {does} algo. Solo necesito recordar {who}...",
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
        current = 0;
        failsOnCurrent = 0;
        cleared = 1;
        DialoguePanel.Show(IntroLines(), OpenExam);
    }

    void OpenExam()
    {
        BuildBook();
        var exam = Exams[current];
        builder = SentenceBuilder.Open(Instruction(), SlotsFor(exam), OnSubmit, OnLeave);
        builder.SetFeedback($"Exam {current + 1} of {Exams.Length} - {exam.Topic}", new Color(0.62f, 0.68f, 0.85f));
    }

    void OnLeave()
    {
        running = false;
        if (bookCanvas != null) Destroy(bookCanvas);
    }

    string Instruction() => $"Exam {current + 1}/{Exams.Length}.  Rewrite in the passive voice:  \"{Exams[current].Active}\"";

    static BuilderSlot[] SlotsFor(Exam e) => new[]
    {
        new BuilderSlot("Subject", e.Subject),
        new BuilderSlot("Passive\nverb", e.Verb),
        new BuilderSlot("Doer\n(by...)", e.Agent),
    };

    void OnSubmit(string[] values)
    {
        attempts++;
        var exam = Exams[current];
        int level = failsOnCurrent >= 2 ? 1 : 0;   // desde el tercer intento la guía explica la regla

        int subject = System.Array.IndexOf(exam.Subject, values[0]);
        int verb = System.Array.IndexOf(exam.Verb, values[1]);
        int agent = System.Array.IndexOf(exam.Agent, values[2]);

        string problem = null;
        if (subject != exam.SubjectOk)
        {
            problem = "X The subject is not right. In the passive voice, the receiver of the action goes first.\n"
                    + (level == 0 ? "Who or what RECEIVES the action in the active sentence?"
                                  : "Active: doer + verb + receiver. Passive: receiver + be + participle + by + doer.");
        }
        else if (verb != exam.VerbOk)
        {
            problem = "X The verb is not right. " + ErrorHuntContent.Hint(exam.VerbKind[verb], level);
        }
        else if (agent != exam.AgentOk)
        {
            problem = exam.AgentKind[agent] == ErrorKind.Agent
                ? "X The doer is not introduced correctly. " + ErrorHuntContent.Hint(ErrorKind.Agent, level)
                : "X The doer is not right. " + (level == 0 ? "Who performs the action in the active sentence?"
                                                              : "The doer is the subject of the ACTIVE sentence. Read it again.");
        }

        if (problem != null)
        {
            errors++;
            failsOnCurrent++;
            builder.SetFeedback(problem, Bad);   // visible al instante; el panel sigue abierto con las mismas elecciones
            return;
        }

        StartCoroutine(Passed());
    }

    IEnumerator Passed()
    {
        builder.Hide();
        bool last = current >= Exams.Length - 1;

        // Una página del libro se aclara
        if (!last) cleared = Mathf.Min(BookRules.Length, cleared + 1);
        else cleared = BookRules.Length;
        RefreshBook(true);
        ToastMessage.Show(last ? "Graded! The whole book is readable now." : "Graded! A page of the book became clear.", 2f);
        yield return Wait(1.6f);

        if (last)
        {
            Finish();
            yield break;
        }

        current++;
        failsOnCurrent = 0;
        var exam = Exams[current];
        builder.Reload(Instruction(), SlotsFor(exam));
        builder.SetFeedback($"Exam {current + 1} of {Exams.Length} - {exam.Topic}", new Color(0.62f, 0.68f, 0.85f));
        builder.Show();
    }

    void Finish()
    {
        builder.Close();
        if (bookCanvas != null) Destroy(bookCanvas);

        var gm = GameManager.Instance;
        gm.CompleteChallenge(challengeId, memoryReward);
        gm.SetStat(challengeId + ":attempts", attempts);
        gm.SetStat(challengeId + ":errors", errors);
        ApplyDoneLook();
        base.Interact(lastInteractor);
        running = false;
        DialoguePanel.Show(ClosingLines(), null);
    }

    void ApplyDoneLook()
    {
        if (icon != null) icon.SetActive(false);
        if (TryGetComponent(out SpriteRenderer sr)) sr.color = new Color(0.55f, 0.6f, 0.6f);
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // ───────────── Libro de consulta (arriba a la izquierda) ─────────────

    void BuildBook()
    {
        if (bookCanvas != null) Destroy(bookCanvas);

        var canvas = PixelUi.NewCanvas("ExamBook", 449);   // justo debajo del constructor
        bookCanvas = canvas.gameObject;

        var panel = PixelUi.NewPanel("Libro", canvas.transform, Frame, new Color(0.12f, 0.09f, 0.05f, 0.94f), out _);
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
