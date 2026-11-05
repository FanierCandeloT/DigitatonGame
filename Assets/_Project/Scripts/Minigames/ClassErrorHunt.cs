using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Un error del jugador en la lección: tipo de error gramatical, cómo falló y la frase.</summary>
public class HuntMistake
{
    public ErrorKind Kind;
    public MistakeType Type;
    public string Sentence;
}

/// <summary>Resultado de la lección: puntaje, si aprobó y el detalle de los errores del último intento.</summary>
public class HuntResult
{
    public bool Passed;
    public int Score, MaxScore, PassScore, Attempts;
    public readonly List<HuntMistake> Mistakes = new List<HuntMistake>();
}

/// <summary>
/// Minijuego "Atrapa el error": Andrés da clase y los estudiantes dicen frases en voz pasiva.
/// Cada frase tiene un tiempo límite: el jugador pulsa STOP si oye un error (o la deja pasar si está bien).
/// Si detiene una frase con error, el tiempo se congela y debe (1) tocar la pieza equivocada y (2) elegir la corrección.
/// Hay que llegar a un puntaje mínimo. Si no aprueba, recibe una guía por tipo de error (sin la respuesta) y puede
/// reintentar con frases nuevas que refuerzan lo que falló, con más tiempo en cada intento.
/// La interfaz se construye por código; los estudiantes y la pizarra son placeholders del greybox.
/// </summary>
public class ClassErrorHunt : MonoBehaviour
{
    // ───────────── Configuración ─────────────

    const int ErrorsPerRun = 5;
    const int CorrectPerRun = 3;
    const float ThinkSeconds = 8f;       // tiempo por frase en el primer intento
    const int PointsPerError = 3;        // detener + ubicar + corregir
    const int PointsPerCorrect = 1;      // dejar pasar una frase correcta

    static readonly string[] StudentNames = { "Valentina", "Santiago", "Camila", "Mateo", "Daniela" };
    static readonly Color[] ShirtColors =
    {
        new Color(0.9f, 0.45f, 0.5f), new Color(0.4f, 0.65f, 0.95f), new Color(0.95f, 0.75f, 0.35f),
        new Color(0.5f, 0.8f, 0.5f), new Color(0.7f, 0.55f, 0.9f),
    };
    static readonly Color[] SkinColors =
    {
        new Color(0.96f, 0.8f, 0.65f), new Color(0.8f, 0.6f, 0.45f), new Color(0.62f, 0.44f, 0.3f),
        new Color(0.9f, 0.72f, 0.55f), new Color(0.72f, 0.52f, 0.38f),
    };

    static readonly Color Background = new Color(0.05f, 0.09f, 0.11f);
    static readonly Color Frame = new Color(0.55f, 0.65f, 1f);
    static readonly Color Panel = new Color(0.07f, 0.09f, 0.17f);
    static readonly Color Good = new Color(0.4f, 0.95f, 0.62f);
    static readonly Color Bad = new Color(0.95f, 0.3f, 0.3f);
    static readonly Color Warn = new Color(1f, 0.78f, 0.34f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.85f);
    static readonly Color Cyan = new Color(0.4f, 0.85f, 1f);

    // ───────────── Estado ─────────────

    class TokenView
    {
        public RectTransform Rect;
        public Image Outline, Fill;
        public TextMeshProUGUI Label;
        public Button Button;
    }

    class Student
    {
        public RectTransform Root;
        public Image Head, Highlight;
        public TextMeshProUGUI Mood;
        public string Name;
        public Color Skin;
        public float Phase, Variation;
    }

    class Floater
    {
        public TextMeshProUGUI Label;
        public float Life, MaxLife;
    }

    readonly List<TokenView> tokens = new List<TokenView>();
    readonly List<Student> students = new List<Student>();
    readonly List<Floater> floaters = new List<Floater>();
    readonly List<Button> chipButtons = new List<Button>();
    readonly List<TextMeshProUGUI> chipLabels = new List<TextMeshProUGUI>();
    readonly HuntResult result = new HuntResult();

    Action<HuntResult> onFinish;
    float passFraction;
    CanvasGroup group;
    RectTransform root, tokensRoot, timerFillRect, attentionFillRect;
    Image timerFill, attentionFill, bubbleFrame;
    TextMeshProUGUI scoreLabel, needLabel, promptLabel, feedbackLabel, speakerLabel, roundLabel, measure;
    GameObject buttonsRoot, chipsRoot, timerBar;

    List<HuntSentence> run;
    readonly List<ErrorKind> focus = new List<ErrorKind>();
    Student speaker;
    int lastSpeaker = -1;
    int attempt = 1;
    int score;
    float attention = 55f, shownAttention = 55f;

    bool acceptingDecision, acceptingToken, acceptingChip;
    bool stopRequested, okRequested;
    int tokenChosen = -1, chipChosen = -1, resultsChoice;
    float inputReadyAt;

    static ClassErrorHunt current;

    /// <summary>Abre la lección. onFinish recibe el resultado al aprobar y continuar, o al salir sin aprobar.</summary>
    public static ClassErrorHunt Open(Action<HuntResult> onFinish, float passFraction = 0.7f)
    {
        var canvas = PixelUi.NewCanvas("ClassErrorHunt", 500);
        var game = canvas.gameObject.AddComponent<ClassErrorHunt>();
        game.onFinish = onFinish;
        game.passFraction = passFraction;
        game.Build();
        current = game;
        GameManager.Instance.MinigameOpen = true;
        game.StartCoroutine(game.Lesson());
        return game;
    }

    void OnDestroy()
    {
        // Si la lección desaparece por un cambio de escena, el jugador no puede quedar bloqueado.
        if (current == this)
        {
            current = null;
            if (GameManager.Exists) GameManager.Instance.MinigameOpen = false;
        }
    }

    // ───────────── Construcción de la interfaz ─────────────

    void Build()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var backdrop = PixelUi.NewImage("Fondo", transform, Background);
        PixelUi.Stretch(backdrop.rectTransform);

        root = PixelUi.NewRect("Salon", transform);
        PixelUi.Stretch(root);

        // Piso del salón (franja inferior) y pizarra
        var floor = PixelUi.NewImage("Piso", root, new Color(0.13f, 0.12f, 0.1f));
        floor.raycastTarget = false;
        PixelUi.Place(floor.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1280f, 300f));

        var board = PixelUi.NewPanel("Pizarra", root, new Color(0.5f, 0.33f, 0.16f), new Color(0.08f, 0.2f, 0.16f), out _);
        board.raycastTarget = false;
        PixelUi.Place(board.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 284f), new Vector2(760f, 112f));
        var chalk = new Color(0.92f, 0.96f, 0.92f);
        var title = PixelUi.NewLabel("Titulo", board.transform, "TODAY'S LESSON: THE PASSIVE VOICE", 32, new Vector2(0f, 18f), new Vector2(720f, 44f), FontStyles.Bold);
        title.color = chalk;
        var sub = PixelUi.NewLabel("Subtitulo", board.transform, "Stop every sentence that has a mistake!", 24, new Vector2(0f, -22f), new Vector2(720f, 34f), FontStyles.Italic);
        sub.color = Color.Lerp(chalk, Color.black, 0.15f);

        BuildHud();
        BuildBubble();
        BuildStudents();
        BuildButtons();
        BuildChips();

        measure = PixelUi.NewLabel("Medida", root, "", 30, Vector2.zero, new Vector2(900f, 60f));
        measure.alpha = 0f;

        promptLabel = PixelUi.NewLabel("Indicacion", root, "", 28, new Vector2(0f, -4f), new Vector2(1200f, 40f));
        promptLabel.color = Muted;
        feedbackLabel = PixelUi.NewLabel("Respuesta", root, "", 30, new Vector2(0f, -40f), new Vector2(1200f, 44f), FontStyles.Bold);
    }

    void BuildHud()
    {
        scoreLabel = PixelUi.NewLabel("Puntaje", root, "0", 64, new Vector2(-520f, 296f), new Vector2(220f, 70f), FontStyles.Bold);
        var scoreCaption = PixelUi.NewLabel("Puntaje titulo", root, "SCORE", 20, new Vector2(-520f, 336f), new Vector2(220f, 26f), FontStyles.Bold);
        scoreCaption.color = Muted;
        needLabel = PixelUi.NewLabel("Puntaje minimo", root, "", 20, new Vector2(-520f, 254f), new Vector2(240f, 26f));
        needLabel.color = Muted;

        var caption = PixelUi.NewLabel("Atencion titulo", root, "CLASS ATTENTION", 20, new Vector2(510f, 336f), new Vector2(240f, 26f), FontStyles.Bold);
        caption.color = Muted;
        var bar = PixelUi.NewPanel("Atencion", root, Frame, Panel, out _);
        bar.raycastTarget = false;
        PixelUi.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(510f, 302f), new Vector2(240f, 34f));
        attentionFill = PixelUi.NewImage("Relleno", bar.transform, Good);
        attentionFill.raycastTarget = false;
        attentionFillRect = attentionFill.rectTransform;
        attentionFillRect.anchorMin = Vector2.zero;
        attentionFillRect.anchorMax = new Vector2(0.5f, 1f);
        attentionFillRect.offsetMin = new Vector2(PixelUi.PixelScale, PixelUi.PixelScale);
        attentionFillRect.offsetMax = new Vector2(-PixelUi.PixelScale, -PixelUi.PixelScale);

        roundLabel = PixelUi.NewLabel("Ronda", root, "", 20, new Vector2(-470f, 38f), new Vector2(260f, 26f), FontStyles.Bold);
        roundLabel.color = Muted;
    }

    void BuildBubble()
    {
        bubbleFrame = PixelUi.NewPanel("Globo", root, Frame, Panel, out _);
        bubbleFrame.raycastTarget = false;
        PixelUi.Place(bubbleFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1160f, 150f));

        speakerLabel = PixelUi.NewLabel("Quien habla", bubbleFrame.transform, "", 22, Vector2.zero, new Vector2(500f, 28f), FontStyles.Bold, TextAlignmentOptions.Left);
        speakerLabel.color = Warn;
        PixelUi.Place(speakerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(500f, 28f));

        tokensRoot = PixelUi.NewRect("Piezas", bubbleFrame.transform);
        tokensRoot.anchoredPosition = new Vector2(0f, -8f);

        // Barra de tiempo bajo el globo
        var bar = PixelUi.NewPanel("Tiempo", root, Frame, Panel, out _);
        bar.raycastTarget = false;
        PixelUi.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(700f, 24f));
        timerBar = bar.gameObject;
        timerFill = PixelUi.NewImage("Relleno", bar.transform, Cyan);
        timerFill.raycastTarget = false;
        timerFillRect = timerFill.rectTransform;
        timerFillRect.anchorMin = Vector2.zero;
        timerFillRect.anchorMax = Vector2.one;
        timerFillRect.offsetMin = new Vector2(PixelUi.PixelScale, PixelUi.PixelScale);
        timerFillRect.offsetMax = new Vector2(-PixelUi.PixelScale, -PixelUi.PixelScale);
        timerBar.SetActive(false);
    }

    void BuildStudents()
    {
        for (int i = 0; i < StudentNames.Length; i++)
        {
            var pos = new Vector2(-480f + i * 240f, -150f);
            var rect = PixelUi.NewRect("Estudiante " + StudentNames[i], root);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(140f, 170f);

            var highlight = PixelUi.NewBox("Marco", rect, Color.clear);
            highlight.raycastTarget = false;
            PixelUi.Stretch(highlight.rectTransform);

            var body = PixelUi.NewBox("Cuerpo", rect, ShirtColors[i]);
            body.raycastTarget = false;
            body.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            body.rectTransform.sizeDelta = new Vector2(68f, 58f);

            var head = PixelUi.NewBox("Cabeza", rect, SkinColors[i]);
            head.raycastTarget = false;
            head.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            head.rectTransform.sizeDelta = new Vector2(54f, 48f);
            foreach (float x in new[] { -12f, 12f })
            {
                var eye = PixelUi.NewImage("Ojo", head.transform, new Color(0.1f, 0.08f, 0.08f));
                eye.raycastTarget = false;
                eye.rectTransform.anchoredPosition = new Vector2(x, 3f);
                eye.rectTransform.sizeDelta = new Vector2(8f, 10f);
            }

            var desk = PixelUi.NewBox("Pupitre", rect, new Color(0.45f, 0.3f, 0.18f));
            desk.raycastTarget = false;
            desk.rectTransform.anchoredPosition = new Vector2(0f, -50f);
            desk.rectTransform.sizeDelta = new Vector2(124f, 36f);

            var mood = PixelUi.NewLabel("Animo", rect, "", 30, new Vector2(0f, 88f), new Vector2(120f, 36f), FontStyles.Bold);
            var nameLabel = PixelUi.NewLabel("Nombre", rect, StudentNames[i], 19, new Vector2(0f, -80f), new Vector2(140f, 24f));
            nameLabel.color = Muted;

            students.Add(new Student
            {
                Root = rect, Head = head, Highlight = highlight, Mood = mood, Name = StudentNames[i], Skin = SkinColors[i],
                Phase = i * 1.3f, Variation = (i - 2) * 9f,
            });
        }
    }

    void BuildButtons()
    {
        buttonsRoot = PixelUi.NewRect("Botones", root).gameObject;

        var stop = MakeBigButton(buttonsRoot.transform, "STOP!", "Space", Bad, new Color(0.25f, 0.06f, 0.08f), new Vector2(-220f, -300f));
        stop.onClick.AddListener(() => { if (acceptingDecision) stopRequested = true; });

        var ok = MakeBigButton(buttonsRoot.transform, "SOUNDS RIGHT", "Enter", Good, new Color(0.06f, 0.2f, 0.12f), new Vector2(220f, -300f));
        ok.onClick.AddListener(() => { if (acceptingDecision) okRequested = true; });

        buttonsRoot.SetActive(false);
    }

    Button MakeBigButton(Transform parent, string text, string key, Color frame, Color fill, Vector2 pos)
    {
        var panel = PixelUi.NewPanel(text, parent, frame, fill, out _);
        PixelUi.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(340f, 86f));
        var label = PixelUi.NewLabel("Texto", panel.transform, text, 36, new Vector2(0f, 10f), new Vector2(320f, 44f), FontStyles.Bold);
        label.color = frame;
        var hint = PixelUi.NewLabel("Tecla", panel.transform, $"({key})", 20, new Vector2(0f, -22f), new Vector2(320f, 26f));
        hint.color = Muted;
        var button = panel.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        return button;
    }

    void BuildChips()
    {
        chipsRoot = PixelUi.NewRect("Opciones", root).gameObject;
        for (int i = 0; i < 3; i++)
        {
            var chip = PixelUi.NewPanel("Opcion " + (i + 1), chipsRoot.transform, Cyan, Panel, out _);
            PixelUi.Place(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f + i * 360f, -300f), new Vector2(330f, 80f));
            var label = PixelUi.NewLabel("Texto", chip.transform, "", 30, Vector2.zero, new Vector2(310f, 76f), FontStyles.Bold);
            var button = chip.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int index = i;
            button.onClick.AddListener(() => PickChip(index));
            chipButtons.Add(button);
            chipLabels.Add(label);
        }
        chipsRoot.SetActive(false);
    }

    // ───────────── Animación y entrada ─────────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        if (group.alpha < 1f) group.alpha = Mathf.Min(1f, group.alpha + dt * 3f);

        // Atención de la clase: barra suave y ánimo de cada estudiante
        shownAttention = Mathf.MoveTowards(shownAttention, attention, 40f * dt);
        attentionFillRect.anchorMax = new Vector2(shownAttention / 100f, 1f);
        attentionFill.color = shownAttention < 35f ? Bad : (shownAttention < 65f ? Warn : Good);

        foreach (var s in students)
        {
            bool speaking = s == speaker;
            float level = Mathf.Clamp(shownAttention + s.Variation, 0f, 100f);
            bool engaged = level >= 65f, bored = level < 35f;

            float bob = speaking ? Mathf.Abs(Mathf.Sin(now * 7f + s.Phase)) * 10f
                                 : (engaged ? Mathf.Sin(now * 3f + s.Phase) * 3f : 0f);
            float droop = !speaking && bored ? -8f : 0f;
            s.Head.rectTransform.anchoredPosition = new Vector2(0f, 38f + bob + droop);
            s.Head.color = !speaking && bored ? Color.Lerp(s.Skin, Color.black, 0.25f) : s.Skin;

            s.Mood.text = speaking ? "" : (engaged ? "!" : (bored ? "Zzz" : ""));
            s.Mood.color = engaged ? Good : Cyan;
            s.Highlight.color = speaking ? new Color(1f, 0.95f, 0.5f, 0.3f) : Color.clear;
        }

        for (int i = floaters.Count - 1; i >= 0; i--)
        {
            var f = floaters[i];
            f.Life -= dt;
            if (f.Life <= 0f)
            {
                Destroy(f.Label.gameObject);
                floaters.RemoveAt(i);
                continue;
            }
            f.Label.rectTransform.anchoredPosition += Vector2.up * 70f * dt;
            f.Label.alpha = f.Life / f.MaxLife;
        }

        var kb = Keyboard.current;
        if (kb == null || now < inputReadyAt) return;
        if (acceptingDecision)
        {
            if (kb.spaceKey.wasPressedThisFrame) stopRequested = true;
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) okRequested = true;
        }
        if (acceptingChip)
        {
            if (kb.digit1Key.wasPressedThisFrame) PickChip(0);
            else if (kb.digit2Key.wasPressedThisFrame) PickChip(1);
            else if (kb.digit3Key.wasPressedThisFrame) PickChip(2);
        }
    }

    void PickToken(int index)
    {
        if (acceptingToken) tokenChosen = index;
    }

    void PickChip(int index)
    {
        if (acceptingChip) chipChosen = index;
    }

    // ───────────── La lección ─────────────

    IEnumerator Lesson()
    {
        yield return Wait(0.6f);
        while (true)
        {
            BeginAttempt();
            buttonsRoot.SetActive(true);
            feedbackLabel.text = "";
            promptLabel.color = Muted;
            promptLabel.text = attempt == 1
                ? "Class is starting! Listen to each student."
                : $"Attempt {attempt}: new sentences. Take your time.";
            yield return Wait(2.2f);

            for (int i = 0; i < run.Count; i++) yield return PlayRound(run[i], i, run.Count);

            result.Passed = score >= result.PassScore;
            result.Score = score;
            result.Attempts = attempt;
            yield return ShowResults();

            if (resultsChoice == 1)   // reintentar
            {
                attempt++;
                continue;
            }
            break;
        }

        result.Passed &= resultsChoice == 2;   // salir sin aprobar nunca cuenta como aprobado
        GameManager.Instance.MinigameOpen = false;
        var callback = onFinish;
        Destroy(gameObject);
        callback?.Invoke(result);
    }

    void BeginAttempt()
    {
        run = ErrorHuntContent.BuildRun(ErrorsPerRun, CorrectPerRun, focus);
        result.Mistakes.Clear();
        result.MaxScore = run.Sum(s => s.HasError ? PointsPerError : PointsPerCorrect);
        result.PassScore = Mathf.CeilToInt(result.MaxScore * passFraction);
        score = 0;
        attention = 55f;
        RefreshScore();
    }

    float TimeScale() => Mathf.Min(1.5f, 1f + 0.25f * (attempt - 1));   // más tiempo en cada reintento

    IEnumerator PlayRound(HuntSentence s, int index, int total)
    {
        speaker = NextSpeaker();
        roundLabel.text = $"SENTENCE {index + 1} / {total}";
        speakerLabel.text = $"{speaker.Name} says:";
        feedbackLabel.text = "";
        bubbleFrame.color = Frame;
        BuildTokens(s);
        buttonsRoot.SetActive(true);
        chipsRoot.SetActive(false);
        yield return RevealTokens();

        // 1) Decisión con tiempo: STOP o dejarla pasar
        promptLabel.color = Muted;
        promptLabel.text = "Is it correct? Press STOP if you hear a mistake.";
        stopRequested = okRequested = false;
        inputReadyAt = Time.unscaledTime + 0.3f;
        acceptingDecision = true;
        timerBar.SetActive(true);

        float limit = ThinkSeconds * TimeScale();
        float elapsed = 0f;
        while (!stopRequested && !okRequested && elapsed < limit)
        {
            elapsed += Time.unscaledDeltaTime;
            float left = Mathf.Clamp01(1f - elapsed / limit);
            timerFillRect.anchorMax = new Vector2(left, 1f);
            timerFill.color = Color.Lerp(Bad, Cyan, left);
            yield return null;
        }
        acceptingDecision = false;
        timerBar.SetActive(false);
        buttonsRoot.SetActive(false);
        bool silent = !stopRequested && !okRequested;

        if (!s.HasError)
        {
            if (stopRequested)
            {
                Mistake(s, MistakeType.FalseAlarm);
                Award(-1, "That sentence was correct!", Bad, -12f);
                bubbleFrame.color = Bad;
            }
            else if (okRequested)
            {
                Award(PointsPerCorrect, "Good ear! That one was right.", Good, 10f);
                bubbleFrame.color = Good;
            }
            else
            {
                Award(0, "That one was fine.", Muted, 3f);
            }
            yield return Wait(1.5f);
            speaker = null;
            yield break;
        }

        // La frase tenía un error
        if (!stopRequested)
        {
            Mistake(s, MistakeType.Missed);
            Award(0, silent ? "Time is up! That sentence had a mistake." : "Oops, that sentence had a mistake.", Bad, -15f);
            bubbleFrame.color = Bad;
            yield return Wait(1.8f);
            speaker = null;
            yield break;
        }

        Award(1, "Good catch! There is a mistake.", Good, 5f);

        // 2) Ubicar la pieza equivocada
        promptLabel.color = Warn;
        promptLabel.text = "Which part is wrong? Tap it.";
        SetTokensInteractable(true);
        tokenChosen = -1;
        inputReadyAt = Time.unscaledTime + 0.15f;
        acceptingToken = true;
        while (tokenChosen < 0) yield return null;
        acceptingToken = false;
        SetTokensInteractable(false);

        if (tokenChosen != s.WrongIndex)
        {
            Mistake(s, MistakeType.WrongWord);
            ColorToken(tokens[tokenChosen], Bad);
            promptLabel.text = "";
            Award(0, "That part is fine. The mistake is somewhere else.", Bad, -8f);
            yield return Wait(2f);
            speaker = null;
            yield break;
        }

        ColorToken(tokens[tokenChosen], Warn);
        Award(1, "Yes, that part is the problem.", Good, 3f);

        // 3) Elegir la corrección
        promptLabel.text = "How do you fix it? Choose the correct form.";
        var options = new List<string>(s.Distractors) { s.Fix };
        ErrorHuntContent.Shuffle(options);
        for (int i = 0; i < chipButtons.Count; i++) chipLabels[i].text = $"{i + 1}.  {options[i]}";
        chipsRoot.SetActive(true);
        chipChosen = -1;
        inputReadyAt = Time.unscaledTime + 0.15f;
        acceptingChip = true;
        while (chipChosen < 0) yield return null;
        acceptingChip = false;
        chipsRoot.SetActive(false);
        promptLabel.text = "";

        if (options[chipChosen] == s.Fix)
        {
            tokens[tokenChosen].Label.text = s.Fix;
            ColorToken(tokens[tokenChosen], Good);
            bubbleFrame.color = Good;
            Award(1, "Perfect! The sentence is correct now.", Good, 12f);
        }
        else
        {
            Mistake(s, MistakeType.WrongFix);
            tokens[tokenChosen].Label.text = options[chipChosen];
            ColorToken(tokens[tokenChosen], Bad);
            Award(0, "That does not fix it.", Bad, -5f);
        }
        yield return Wait(1.9f);
        speaker = null;
    }

    Student NextSpeaker()
    {
        int next;
        do { next = UnityEngine.Random.Range(0, students.Count); } while (next == lastSpeaker);
        lastSpeaker = next;
        return students[next];
    }

    void Mistake(HuntSentence s, MistakeType type)
    {
        result.Mistakes.Add(new HuntMistake { Kind = s.Kind, Type = type, Sentence = s.FullText });
    }

    void Award(int points, string message, Color color, float attentionDelta)
    {
        if (points != 0)
        {
            score = Mathf.Max(0, score + points);
            Popup(points > 0 ? $"+{points}" : points.ToString(), color, new Vector2(-520f, 210f));
        }
        attention = Mathf.Clamp(attention + attentionDelta, 0f, 100f);
        feedbackLabel.text = message;
        feedbackLabel.color = color;
        RefreshScore();
    }

    void RefreshScore()
    {
        scoreLabel.text = score.ToString();
        scoreLabel.color = score >= result.PassScore ? Good : Color.white;
        needLabel.text = $"Pass: {result.PassScore} of {result.MaxScore}";
    }

    void Popup(string text, Color color, Vector2 position)
    {
        var label = PixelUi.NewLabel("Puntos", root, text, 44, position, new Vector2(160f, 54f), FontStyles.Bold);
        label.color = color;
        floaters.Add(new Floater { Label = label, Life = 1.2f, MaxLife = 1.2f });
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // ───────────── Piezas de la frase ─────────────

    void BuildTokens(HuntSentence s)
    {
        foreach (var t in tokens) Destroy(t.Rect.gameObject);
        tokens.Clear();

        const float pad = 44f, gap = 14f, height = 66f;
        var widths = new float[s.Segments.Length];
        float total = gap * (s.Segments.Length - 1);
        for (int i = 0; i < widths.Length; i++)
        {
            widths[i] = Mathf.Max(80f, measure.GetPreferredValues(s.Segments[i]).x + pad);
            total += widths[i];
        }

        float x = -total * 0.5f;
        for (int i = 0; i < widths.Length; i++)
        {
            var outline = PixelUi.NewPanel("Pieza", tokensRoot, Frame, Panel, out var fill);
            outline.rectTransform.anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
            outline.rectTransform.sizeDelta = new Vector2(widths[i], height);
            x += widths[i] + gap;

            var label = PixelUi.NewLabel("Texto", outline.transform, s.Segments[i], 30, Vector2.zero, new Vector2(widths[i], height));
            var button = outline.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            int index = i;
            button.onClick.AddListener(() => PickToken(index));

            outline.gameObject.SetActive(false);   // aparecen una por una
            tokens.Add(new TokenView { Rect = outline.rectTransform, Outline = outline, Fill = fill, Label = label, Button = button });
        }
    }

    IEnumerator RevealTokens()
    {
        foreach (var t in tokens)
        {
            t.Rect.gameObject.SetActive(true);
            yield return Wait(0.18f);
        }
    }

    void SetTokensInteractable(bool value)
    {
        foreach (var t in tokens) t.Button.interactable = value;
    }

    static void ColorToken(TokenView t, Color color)
    {
        t.Outline.color = color;
        t.Fill.color = Color.Lerp(Panel, color, 0.3f);
    }

    // ───────────── Resultados y guía ─────────────

    IEnumerator ShowResults()
    {
        buttonsRoot.SetActive(false);
        chipsRoot.SetActive(false);
        promptLabel.text = "";
        feedbackLabel.text = "";
        resultsChoice = 0;

        bool passed = result.Passed;
        var panel = PixelUi.NewPanel("Resultados", root, passed ? Good : Bad, new Color(0.05f, 0.07f, 0.14f), out _);
        PixelUi.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1100f, 620f));

        var header = PixelUi.NewLabel("Titulo", panel.transform, passed ? "LESSON PASSED!" : "NOT YET... LET'S REVIEW", 46, new Vector2(0f, 262f), new Vector2(1000f, 60f), FontStyles.Bold);
        header.color = passed ? Good : Bad;
        var line = PixelUi.NewLabel("Puntaje", panel.transform, $"Score: {score} / {result.MaxScore}   (you needed {result.PassScore})", 30, new Vector2(0f, 212f), new Vector2(1000f, 40f));

        // Errores agrupados por tipo: los más frecuentes primero
        var groups = result.Mistakes
            .GroupBy(m => m.Type == MistakeType.FalseAlarm ? ErrorKind.None : m.Kind)
            .OrderByDescending(g => g.Count())
            .Take(passed ? 2 : 3)
            .ToList();

        int hintLevel = Mathf.Min(1, attempt - 1);
        if (groups.Count == 0)
        {
            var perfect = PixelUi.NewLabel("Sin errores", panel.transform, "No mistakes at all. Perfect class!", 32, new Vector2(0f, 60f), new Vector2(1000f, 50f));
            perfect.color = Good;
        }
        else
        {
            var notes = PixelUi.NewLabel("Guia titulo", panel.transform, passed ? "Keep an eye on:" : "Teacher's notes:", 26, new Vector2(-300f, 158f), new Vector2(400f, 34f), FontStyles.Bold, TextAlignmentOptions.Left);
            notes.color = Warn;
            PixelUi.Place(notes.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-500f, 158f), new Vector2(600f, 34f));

            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var first = g.First();
                int n = g.Count();
                string head = $"<color=#FFC857><b>{ErrorHuntContent.Label(g.Key)}</b></color>  -  {n} mistake{(n > 1 ? "s" : "")}";
                string hint = ErrorHuntContent.Hint(g.Key, hintLevel);
                string quote = $"<i><color=#9AA6D6>{ErrorHuntContent.Describe(first.Type)} \"{first.Sentence}\"</color></i>";

                var text = PixelUi.NewLabel("Guia", panel.transform, $"{head}\n{hint}\n{quote}", 23, Vector2.zero, new Vector2(1020f, 110f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
                text.textWrappingMode = TextWrappingModes.Normal;
                PixelUi.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 82f - i * 118f), new Vector2(1020f, 110f));
            }
        }

        // Para el siguiente intento se refuerzan los tipos de error que fallaste
        focus.Clear();
        focus.AddRange(result.Mistakes.Where(m => m.Type != MistakeType.FalseAlarm).Select(m => m.Kind).Distinct());

        if (passed)
        {
            MakePanelButton(panel.transform, "CONTINUE", Good, new Vector2(0f, -258f), 2);
        }
        else
        {
            MakePanelButton(panel.transform, "TRY AGAIN", Good, new Vector2(-190f, -258f), 1);
            MakePanelButton(panel.transform, "LEAVE", Muted, new Vector2(190f, -258f), 3);
        }

        while (resultsChoice == 0) yield return null;
        Destroy(panel.gameObject);
    }

    GameObject MakePanelButton(Transform parent, string text, Color color, Vector2 pos, int choice)
    {
        var button = PixelUi.NewPanel(text, parent, color, new Color(0.06f, 0.1f, 0.14f), out _);
        PixelUi.Place(button.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(320f, 74f));
        var label = PixelUi.NewLabel("Texto", button.transform, text, 32, Vector2.zero, new Vector2(300f, 70f), FontStyles.Bold);
        label.color = color;
        var b = button.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => resultsChoice = choice);
        return button.gameObject;
    }
}
