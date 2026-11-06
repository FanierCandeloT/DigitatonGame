using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Resumen del reto de exámenes: intentos, errores y en qué tipos de regla falló (para el cuaderno).</summary>
public class SurgeryResult
{
    public int Attempts, Errors;
    public readonly Dictionary<string, int> Weak = new Dictionary<string, int>();

    public void Note(string label)
    {
        Weak.TryGetValue(label, out int n);
        Weak[label] = n + 1;
    }
}

/// <summary>
/// "Cirugía de frases" (reto 3): sobre un examen escrito en voz ACTIVA, el jugador opera la frase como si fuera una pieza física:
///  1) toca el receptor y el hacedor para INTERCAMBIARLOS de lugar,
///  2) toca el verbo y elige su forma pasiva (be + participio),
///  3) elige un sello (by / to / from / of) y lo coloca en el hueco antes del hacedor.
/// La frase de abajo se va armando en vivo y la lee. Al pulsar CALIFICAR, cada error se explica por separado (orden, verbo, agente)
/// con una guía sin la respuesta. Después de cada frase hay una pregunta del profesor sobre POR QUÉ se usa la pasiva (conceptual).
/// Andrés queda quieto mientras dura el reto; Esc lo abandona.
/// </summary>
public class ExamSurgery : MonoBehaviour
{
    // ───────────── Contenido ─────────────

    class SurgeryExam
    {
        public string Topic, Doer, Verb, Receiver;
        public string[] VerbOptions; public ErrorKind[] VerbKinds; public int VerbOk;
        public string Question, QuestionHint, QuestionRule;
        public string[] Answers; public int AnswerOk;
    }

    static readonly SurgeryExam[] Exams =
    {
        new SurgeryExam   // presente simple
        {
            Topic = "SDG 8 - decent work", Doer = "The company", Verb = "pays", Receiver = "the workers",
            VerbOptions = new[] { "are paid", "is paid", "are paying", "paid" },
            VerbKinds = new[] { ErrorKind.None, ErrorKind.Agreement, ErrorKind.Participle, ErrorKind.MissingBe }, VerbOk = 0,
            Question = "Why does the passive sentence start with \"The workers\"?",
            Answers = new[] { "Because we focus on who RECEIVES the action.", "Because the workers are doing the action.", "Because 'pay' is an irregular verb." }, AnswerOk = 0,
            QuestionHint = "Look at the active sentence again. Who is the sentence about now?",
            QuestionRule = "In the passive, the receiver of the action becomes the subject, so it goes first.",
        },
        new SurgeryExam   // pasado simple
        {
            Topic = "SDG 12 - responsible consumption", Doer = "Students", Verb = "recycled", Receiver = "the bottles",
            VerbOptions = new[] { "were recycled", "are recycled", "was recycled", "were recycling" },
            VerbKinds = new[] { ErrorKind.None, ErrorKind.Tense, ErrorKind.Agreement, ErrorKind.Participle }, VerbOk = 0,
            Question = "When can we leave out \"by students\"?",
            Answers = new[] { "Never: 'by' is always required.", "When the doer is unknown or not important.", "Only in questions." }, AnswerOk = 1,
            QuestionHint = "Remember the accident: \"A scaffold was left unsecured.\" Was the doer named?",
            QuestionRule = "The agent can be omitted when it is unknown, obvious or unimportant. Leaving it out can also hide who is responsible!",
        },
        new SurgeryExam   // present perfect
        {
            Topic = "SDG 8 - decent work", Doer = "The company", Verb = "has reduced", Receiver = "the wages",
            VerbOptions = new[] { "have been reduced", "has been reduced", "have been reduce", "have reduced" },
            VerbKinds = new[] { ErrorKind.None, ErrorKind.Agreement, ErrorKind.Participle, ErrorKind.MissingBe }, VerbOk = 0,
            Question = "In the passive, which word comes after \"have / has\"?",
            Answers = new[] { "be", "been", "being" }, AnswerOk = 1,
            QuestionHint = "Present perfect = have / has + past participle. 'Be' has its own past participle.",
            QuestionRule = "Present perfect passive: have / has + BEEN + past participle.",
        },
        new SurgeryExam   // modal
        {
            Topic = "SDG 8 - safe work", Doer = "Workers", Verb = "must wear", Receiver = "helmets",
            VerbOptions = new[] { "must be worn", "must worn", "must be wear", "must are worn" },
            VerbKinds = new[] { ErrorKind.None, ErrorKind.MissingBe, ErrorKind.Participle, ErrorKind.Agreement }, VerbOk = 0,
            Question = "After a modal verb (must, can, should), the passive uses...",
            Answers = new[] { "be + past participle", "been + past participle", "is + infinitive" }, AnswerOk = 0,
            QuestionHint = "A modal is always followed by the base form of the next verb.",
            QuestionRule = "Modal passive: modal + BE + past participle (must be worn).",
        },
    };

    public static int ExamCount => Exams.Length;

    static readonly string[] Stamps = { "by", "to", "from", "of" };

    // ───────────── Estilo ─────────────

    static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.09f, 0.88f);
    static readonly Color PaperEdge = new Color(1f, 0.82f, 0.45f);
    static readonly Color PaperFill = new Color(0.10f, 0.09f, 0.14f, 0.97f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.85f);
    static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
    static readonly Color Good = new Color(0.4f, 0.95f, 0.62f);
    static readonly Color ChipFill = new Color(0.08f, 0.1f, 0.19f);
    static readonly Color Selected = new Color(1f, 1f, 1f);

    enum Role { Doer, Verb, Receiver, By }

    static Color RoleColor(Role role)
    {
        switch (role)
        {
            case Role.Doer: return new Color(1f, 0.78f, 0.35f);
            case Role.Verb: return new Color(0.4f, 0.85f, 1f);
            case Role.Receiver: return new Color(0.95f, 0.55f, 0.85f);
            default: return new Color(0.55f, 0.95f, 0.65f);
        }
    }

    class Chip
    {
        public Role Role;
        public string Text;
        public Image Outline;
    }

    // ───────────── Estado ─────────────

    Action<int> onPassed;
    Action<SurgeryResult> onDone;
    Action onLeave;
    readonly SurgeryResult result = new SurgeryResult();
    readonly List<Chip> line = new List<Chip>();
    GameManager.ModalLock modalLock;

    int index;
    int fails;                 // intentos fallidos en la frase actual (sube el nivel de la guía)
    int questionFails;
    int verbIndex = -1;
    Chip selectedChip;
    string selectedStamp;
    bool verbMenuOpen;
    bool busy;
    bool questionStage;
    float inputReadyAt;

    RectTransform paperRect, workRoot, lineRoot, extraRoot, questionRoot;
    TextMeshProUGUI examLabel, activeLabel, previewLabel, feedbackLabel, questionLabel;
    Button gradeButton;

    /// <summary>Abre el reto. onPassed(i) avisa que se aprobó la frase i (con su pregunta); onDone al aprobar todas.</summary>
    public static ExamSurgery Open(Action<int> onPassed, Action<SurgeryResult> onDone, Action onLeave)
    {
        var canvas = PixelUi.NewCanvas("ExamSurgery", 450);
        var game = canvas.gameObject.AddComponent<ExamSurgery>();
        game.modalLock = GameManager.Instance.LockInput();   // antes de armar la UI: si algo falla, OnDestroy lo suelta
        game.onPassed = onPassed;
        game.onDone = onDone;
        game.onLeave = onLeave;
        game.Build();
        game.LoadExam();
        game.inputReadyAt = Time.unscaledTime + 0.35f;   // ignora la pulsación que cerró el diálogo anterior
        return game;
    }

    void OnDestroy() => modalLock?.Release();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.unscaledTime > inputReadyAt) Leave();
    }

    void Leave()
    {
        var callback = onLeave;
        Destroy(gameObject);
        callback?.Invoke();
    }

    // ───────────── Interfaz ─────────────

    void Build()
    {
        var back = PixelUi.NewImage("Fondo", transform, Backdrop);
        PixelUi.Stretch(back.rectTransform);

        var paper = PixelUi.NewPanel("Examen", transform, PaperEdge, PaperFill, out _);
        paperRect = paper.rectTransform;
        PixelUi.Place(paperRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1180f, 500f));

        examLabel = PixelUi.NewLabel("Examen", paper.transform, "", 24, Vector2.zero, new Vector2(1100f, 30f), FontStyles.Bold);
        examLabel.color = PaperEdge;
        PixelUi.Place(examLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1100f, 30f));

        // Zona de trabajo (cirugía) y zona de pregunta: solo una se ve a la vez
        workRoot = PixelUi.NewRect("Cirugia", paper.transform);
        PixelUi.Stretch(workRoot);
        questionRoot = PixelUi.NewRect("Pregunta", paper.transform);
        PixelUi.Stretch(questionRoot);

        var instruction = PixelUi.NewLabel("Indicacion", workRoot, "Operate on the sentence: SWAP the two noun chips, pick the verb's passive form, and stamp the doer.", 20, Vector2.zero, new Vector2(1120f, 28f), FontStyles.Italic);
        instruction.color = Muted;
        PixelUi.Place(instruction.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(1120f, 28f));

        activeLabel = PixelUi.NewLabel("Activa", workRoot, "", 30, Vector2.zero, new Vector2(1120f, 40f), FontStyles.Bold);
        PixelUi.Place(activeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1120f, 40f));

        lineRoot = PixelUi.NewRect("Linea", workRoot);
        PixelUi.Place(lineRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(1120f, 60f));

        previewLabel = PixelUi.NewLabel("Vista", workRoot, "", 26, Vector2.zero, new Vector2(1120f, 36f), FontStyles.Italic);
        previewLabel.color = Muted;
        PixelUi.Place(previewLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -202f), new Vector2(1120f, 36f));

        extraRoot = PixelUi.NewRect("Opciones", workRoot);   // menú del verbo
        PixelUi.Place(extraRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(1120f, 50f));

        // Sellos y botón de calificar
        var stampCaption = PixelUi.NewLabel("SellosTitulo", workRoot, "STAMPS", 18, Vector2.zero, new Vector2(110f, 40f), FontStyles.Bold);
        stampCaption.color = RoleColor(Role.By);
        PixelUi.Place(stampCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -316f), new Vector2(110f, 40f));
        for (int i = 0; i < Stamps.Length; i++)
        {
            string stamp = Stamps[i];
            var chip = NewChip(workRoot, stamp, RoleColor(Role.By), 110f, 46f, 26);
            PixelUi.Place(chip.Outline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f + i * 126f, -312f), new Vector2(110f, 46f));
            chip.Outline.gameObject.AddComponent<Button>().onClick.AddListener(() => PickStamp(stamp));
            stampChips.Add(stamp, chip.Outline);
        }

        gradeButton = NewButton(workRoot, "GRADE", Good, new Vector2(970f, -310f), new Vector2(190f, 52f));
        gradeButton.onClick.AddListener(Grade);

        feedbackLabel = PixelUi.NewLabel("Feedback", paper.transform, "", 22, Vector2.zero, new Vector2(1120f, 96f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        feedbackLabel.textWrappingMode = TextWrappingModes.Normal;
        feedbackLabel.fontSizeMax = 22f;
        feedbackLabel.fontSizeMin = 15f;
        feedbackLabel.enableAutoSizing = true;   // varios errores a la vez deben caber en el recuadro
        PixelUi.Place(feedbackLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(1120f, 92f));

        var leave = PixelUi.NewLabel("Salir", paper.transform, "Esc: leave", 16, Vector2.zero, new Vector2(200f, 24f), FontStyles.Normal, TextAlignmentOptions.BottomRight);
        leave.color = Muted;
        PixelUi.Place(leave.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 10f), new Vector2(200f, 24f));

        // Pregunta del profesor
        questionLabel = PixelUi.NewLabel("Pregunta", questionRoot, "", 32, Vector2.zero, new Vector2(1080f, 90f), FontStyles.Bold);
        questionLabel.textWrappingMode = TextWrappingModes.Normal;
        PixelUi.Place(questionLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(1080f, 90f));
    }

    readonly Dictionary<string, Image> stampChips = new Dictionary<string, Image>();
    readonly List<GameObject> questionButtons = new List<GameObject>();

    static Chip NewChip(Transform parent, string text, Color color, float width, float height, float fontSize)
    {
        var outline = PixelUi.NewPanel("Ficha", parent, color, ChipFill, out _);
        outline.rectTransform.sizeDelta = new Vector2(width, height);
        var label = PixelUi.NewLabel("Texto", outline.transform, text, fontSize, Vector2.zero, Vector2.zero, FontStyles.Bold);
        PixelUi.Stretch(label.rectTransform);
        return new Chip { Outline = outline, Text = text };
    }

    static Button NewButton(Transform parent, string text, Color color, Vector2 topLeftPosition, Vector2 size)
    {
        var outline = PixelUi.NewPanel("Boton", parent, color, ChipFill, out _);
        PixelUi.Place(outline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeftPosition, size);
        var label = PixelUi.NewLabel("Texto", outline.transform, text, 26, Vector2.zero, Vector2.zero, FontStyles.Bold);
        PixelUi.Stretch(label.rectTransform);
        label.color = color;
        var button = outline.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;   // igual que los demás botones pixelados (sin tinte)
        return button;
    }

    // ───────────── Cargar una frase ─────────────

    void LoadExam()
    {
        var exam = Exams[index];
        questionStage = false;
        busy = false;
        fails = 0;
        questionFails = 0;
        verbIndex = -1;
        selectedChip = null;
        selectedStamp = null;
        verbMenuOpen = false;

        workRoot.gameObject.SetActive(true);
        questionRoot.gameObject.SetActive(false);
        examLabel.text = $"EXAM {index + 1} / {Exams.Length}  -  {exam.Topic}";
        activeLabel.text = $"ACTIVE:  {exam.Doer} {exam.Verb} {exam.Receiver}.";
        SetFeedback("Rewrite it in the passive voice. Tap two chips to swap them.", Muted);

        line.Clear();
        line.Add(new Chip { Role = Role.Doer, Text = exam.Doer });
        line.Add(new Chip { Role = Role.Verb, Text = exam.Verb });
        line.Add(new Chip { Role = Role.Receiver, Text = exam.Receiver });
        Redraw();
    }

    void SetFeedback(string text, Color color)
    {
        if (color == Good) AudioManager.PlayCorrect();        // el sonido sale en el mismo instante que el mensaje
        else if (color == Bad) AudioManager.PlayIncorrect();
        feedbackLabel.text = text;
        feedbackLabel.color = color;
    }

    // ───────────── Dibujo de la frase ─────────────

    string Display(int i)
    {
        var chip = line[i];
        if (chip.Role == Role.Doer || chip.Role == Role.Receiver)
        {
            if (string.IsNullOrEmpty(chip.Text)) return "";
            char first = i == 0 ? char.ToUpper(chip.Text[0]) : char.ToLower(chip.Text[0]);
            return first + chip.Text.Substring(1);
        }
        return chip.Text;
    }

    static float WidthFor(string text) => 44f + text.Length * 18f;

    void Redraw()
    {
        for (int i = lineRoot.childCount - 1; i >= 0; i--) Destroy(lineRoot.GetChild(i).gameObject);
        for (int i = extraRoot.childCount - 1; i >= 0; i--) Destroy(extraRoot.GetChild(i).gameObject);

        bool placing = selectedStamp != null;
        float gap = placing ? 64f : 16f;
        float total = -gap;
        for (int i = 0; i < line.Count; i++) total += WidthFor(Display(i)) + gap;

        float x = -total * 0.5f;
        var gapX = new List<float>();   // centro de cada hueco (0..n)
        gapX.Add(x - gap * 0.5f);
        for (int i = 0; i < line.Count; i++)
        {
            string text = Display(i);
            float width = WidthFor(text);
            var chip = line[i];
            var made = NewChip(lineRoot, text, RoleColor(chip.Role), width, 54f, 30);
            chip.Outline = made.Outline;
            if (chip == selectedChip) chip.Outline.color = Selected;
            PixelUi.Place(chip.Outline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, 54f));
            var captured = chip;
            chip.Outline.gameObject.AddComponent<Button>().onClick.AddListener(() => ClickChip(captured));
            x += width;
            gapX.Add(x + gap * 0.5f);
            x += gap;
        }

        if (placing)
        {
            for (int g = 0; g < gapX.Count; g++)
            {
                int position = g;
                var plus = NewButton(lineRoot, "+", RoleColor(Role.By), Vector2.zero, new Vector2(46f, 46f));
                PixelUi.Place(((RectTransform)plus.transform), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(gapX[g], 0f), new Vector2(46f, 46f));
                plus.onClick.AddListener(() => PlaceStamp(position));
            }
        }

        foreach (var pair in stampChips)
            pair.Value.color = pair.Key == selectedStamp ? Selected : RoleColor(Role.By);

        if (verbMenuOpen) DrawVerbMenu();

        var sb = new StringBuilder();
        for (int i = 0; i < line.Count; i++) sb.Append(i > 0 ? " " : "").Append(Display(i));
        previewLabel.text = $"\"{sb}.\"";
    }

    void DrawVerbMenu()
    {
        var exam = Exams[index];
        float width = 0f;
        foreach (var option in exam.VerbOptions) width += WidthFor(option) * 0.8f + 12f;
        float x = -width * 0.5f;
        for (int i = 0; i < exam.VerbOptions.Length; i++)
        {
            int choice = i;
            float w = WidthFor(exam.VerbOptions[i]) * 0.8f;
            var chip = NewChip(extraRoot, exam.VerbOptions[i], RoleColor(Role.Verb), w, 44f, 24);
            PixelUi.Place(chip.Outline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w, 44f));
            if (verbIndex == i) chip.Outline.color = Selected;
            chip.Outline.gameObject.AddComponent<Button>().onClick.AddListener(() => ChooseVerb(choice));
            x += w + 12f;
        }
    }

    // ───────────── Acciones del jugador ─────────────

    bool CanAct => !busy && !questionStage && Time.unscaledTime > inputReadyAt;

    void ClickChip(Chip chip)
    {
        if (!CanAct) return;
        verbMenuOpen = false;

        if (chip.Role == Role.Verb)
        {
            selectedChip = null;
            selectedStamp = null;
            verbMenuOpen = true;
        }
        else if (chip.Role == Role.By)
        {
            line.Remove(chip);   // se quita el sello colocado
            selectedChip = null;
        }
        else if (selectedChip == null)
        {
            selectedChip = chip;
        }
        else if (selectedChip == chip)
        {
            selectedChip = null;
        }
        else
        {
            // Intercambia el hacedor y el receptor de lugar
            int a = line.IndexOf(selectedChip);
            int b = line.IndexOf(chip);
            line[a] = chip;
            line[b] = selectedChip;
            selectedChip = null;
            SetFeedback("Swapped! Read the new sentence below. Does the verb still fit?", Muted);
        }
        selectedStamp = null;
        Redraw();
    }

    void ChooseVerb(int choice)
    {
        if (!CanAct) return;
        verbIndex = choice;
        foreach (var chip in line)
            if (chip.Role == Role.Verb) chip.Text = Exams[index].VerbOptions[choice];
        verbMenuOpen = false;
        Redraw();
    }

    void PickStamp(string stamp)
    {
        if (!CanAct) return;
        selectedStamp = selectedStamp == stamp ? null : stamp;
        selectedChip = null;
        verbMenuOpen = false;
        if (selectedStamp != null) SetFeedback("Now tap a + to place the stamp.", Muted);
        Redraw();
    }

    void PlaceStamp(int position)
    {
        if (!CanAct || selectedStamp == null) return;
        line.RemoveAll(c => c.Role == Role.By);   // un solo sello a la vez
        position = Mathf.Min(position, line.Count);
        line.Insert(position, new Chip { Role = Role.By, Text = selectedStamp });
        selectedStamp = null;
        Redraw();
    }

    // ───────────── Calificar ─────────────

    void Grade()
    {
        if (!CanAct) return;
        var exam = Exams[index];
        result.Attempts++;
        int level = fails >= 2 ? 1 : 0;   // desde el 3.er intento la guía explica la regla

        var problems = new List<string>();
        var wrong = new List<Chip>();
        Chip Find(Role role) => line.Find(c => c.Role == role);
        var receiver = Find(Role.Receiver);
        var doer = Find(Role.Doer);
        var verb = Find(Role.Verb);
        var by = Find(Role.By);

        // 1) Orden: el receptor va primero
        if (line[0].Role != Role.Receiver)
        {
            problems.Add("Word order: the RECEIVER of the action must come first. " +
                         (level == 0 ? "Who or what receives the action in the active sentence?"
                                     : "Active: doer + verb + receiver. Passive: receiver + be + participle + by + doer."));
            wrong.Add(line[0]);
            result.Note("Word order");
        }

        // 2) Verbo
        if (verbIndex < 0)
        {
            problems.Add("Verb: it is still in the active voice. Tap the verb and choose a passive form (be + participle).");
            wrong.Add(verb);
            result.Note("The verb 'to be'");
        }
        else if (exam.VerbKinds[verbIndex] != ErrorKind.None)
        {
            var kind = exam.VerbKinds[verbIndex];
            problems.Add("Verb: " + ErrorHuntContent.Hint(kind, level));
            wrong.Add(verb);
            result.Note(ErrorHuntContent.Label(kind));
        }

        // 3) Agente: sello correcto y justo antes del hacedor
        if (by == null)
        {
            problems.Add("Doer: " + ErrorHuntContent.Hint(ErrorKind.Agent, level) + " Place a stamp before the doer.");
            wrong.Add(doer);
            result.Note(ErrorHuntContent.Label(ErrorKind.Agent));
        }
        else if (by.Text != "by")
        {
            problems.Add("Doer: the stamp \"" + by.Text + "\" does not introduce the doer. " + ErrorHuntContent.Hint(ErrorKind.Agent, level));
            wrong.Add(by);
            result.Note(ErrorHuntContent.Label(ErrorKind.Agent));
        }
        else if (line.IndexOf(by) != line.IndexOf(doer) - 1)
        {
            problems.Add("Doer: the stamp is in the wrong place. It goes right before the doer.");
            wrong.Add(by);
            result.Note("Word order");
        }

        if (problems.Count > 0)
        {
            result.Errors++;
            fails++;
            StartCoroutine(ShowProblems(problems, wrong));
            return;
        }

        StartCoroutine(PassedSurgery());
    }

    IEnumerator ShowProblems(List<string> problems, List<Chip> wrong)
    {
        var sb = new StringBuilder("X Not graded yet:");
        foreach (var p in problems) sb.Append("\n- ").Append(p);
        SetFeedback(sb.ToString(), Bad);

        foreach (var chip in wrong) if (chip != null && chip.Outline != null) chip.Outline.color = Bad;
        busy = true;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            paperRect.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * 6f * Mathf.Clamp01(1f - t / 0.3f), -110f);
            yield return null;
        }
        paperRect.anchoredPosition = new Vector2(0f, -110f);
        busy = false;
    }

    IEnumerator PassedSurgery()
    {
        busy = true;
        SetFeedback("Correct! The sentence is passive now. One last question...", Good);
        foreach (var chip in line) if (chip.Outline != null) chip.Outline.color = Good;
        yield return PixelUi.Wait(1.0f);
        OpenQuestion();
    }

    // ───────────── Pregunta del profesor ─────────────

    void OpenQuestion()
    {
        var exam = Exams[index];
        questionStage = true;
        busy = false;
        workRoot.gameObject.SetActive(false);
        questionRoot.gameObject.SetActive(true);
        questionLabel.text = "TEACHER'S QUESTION\n" + exam.Question;
        SetFeedback("Choose the best answer.", Muted);

        foreach (var old in questionButtons) Destroy(old);
        questionButtons.Clear();
        for (int i = 0; i < exam.Answers.Length; i++)
        {
            int choice = i;
            var button = NewButton(questionRoot, exam.Answers[i], RoleColor(Role.Verb), new Vector2(100f, -166f - i * 62f), new Vector2(980f, 52f));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = Color.white;
            label.fontSize = 24;
            button.onClick.AddListener(() => Answer(choice));
            questionButtons.Add(button.gameObject);
        }
    }

    void Answer(int choice)
    {
        if (busy || !questionStage || Time.unscaledTime < inputReadyAt) return;
        var exam = Exams[index];
        if (choice == exam.AnswerOk)
        {
            StartCoroutine(NextExam());
            return;
        }

        result.Errors++;
        result.Note("Why and when to use the passive");
        questionFails++;
        SetFeedback("X Not quite. " + (questionFails >= 2 ? exam.QuestionRule : exam.QuestionHint), Bad);
    }

    IEnumerator NextExam()
    {
        busy = true;
        SetFeedback("Graded! Well done.", Good);
        onPassed?.Invoke(index);
        yield return PixelUi.Wait(1.2f);

        if (index >= Exams.Length - 1)
        {
            var callback = onDone;
            Destroy(gameObject);
            callback?.Invoke(result);
            yield break;
        }

        index++;
        LoadExam();
    }

}
