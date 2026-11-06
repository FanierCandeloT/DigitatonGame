using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Un intento equivocado: lo que armó el jugador, por qué falla y la frase correcta.</summary>
public class MemoryMistake
{
    public string Attempt, Explanation, Correct;
}

/// <summary>Resultado del minijuego: cuántos errores hubo y el detalle de cada intento equivocado.</summary>
public class MemoryResult
{
    public int Errors;
    public readonly List<MemoryMistake> Mistakes = new List<MemoryMistake>();
}

/// <summary>
/// Minijuego de reconstrucción de memoria: el jugador tiende cables entre neuronas
/// (sujeto → verbo en pasiva → complemento) para formar frases correctas en Passive Voice.
/// Ambientación: un escaneo cerebral en pixel art; los cables llevan impulsos eléctricos y cada recuerdo
/// recuperado "enciende" sus neuronas. Toda la interfaz se construye por código (placeholders del greybox).
/// </summary>
public class MemoryReconstructionGame : MonoBehaviour
{
    // ───────────── Contenido ─────────────

    readonly struct Sentence
    {
        public readonly string Subject, Verb, Complement;

        /// <summary>Pregunta guía: sirve de pista cuando la frase es correcta gramaticalmente pero no tiene sentido.</summary>
        public readonly string Clue;

        public readonly bool Plural;

        public Sentence(string subject, string verb, string complement, bool plural, string clue)
        {
            Subject = subject;
            Verb = verb;
            Complement = complement;
            Plural = plural;
            Clue = clue;
        }
    }

    static readonly Sentence[] Sentences =
    {
        new Sentence("My English class", "was taught", "in Room 204.", false, "Where was the class taught?"),   // la vida del profesor
        new Sentence("Fair salaries", "were paid", "to all the workers.", true, "Who received the fair salaries?"),  // ODS 8
        new Sentence("Plastic bottles", "are recycled", "into new products.", true, "What happens to the plastic bottles?"), // ODS 12
    };

    // Verbo sobrante: obliga a fijarse en la concordancia entre sujeto y auxiliar.
    const string DistractorVerb = "was paid";
    const bool DistractorPlural = false;

    static readonly string[] ColumnNames = { "SUBJECT", "PASSIVE VERB", "COMPLEMENT" };

    // Un color por rol gramatical (ayuda a leer el tablero, pero no revela qué piezas combinan).
    static readonly Color[] RoleColors =
    {
        new Color(1f, 0.78f, 0.35f),
        new Color(0.4f, 0.85f, 1f),
        new Color(0.95f, 0.55f, 0.85f),
    };

    static readonly Color Background = new Color(0.03f, 0.04f, 0.09f);
    static readonly Color NodeFill = new Color(0.07f, 0.09f, 0.17f);
    static readonly Color NodeDoneFill = new Color(0.07f, 0.26f, 0.18f);
    static readonly Color WireNeutral = new Color(0.72f, 0.76f, 1f);
    static readonly Color WireWrong = new Color(0.95f, 0.3f, 0.3f);
    static readonly Color WireDone = new Color(0.4f, 0.95f, 0.62f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.85f);

    const float ColumnGap = 420f;
    const float RowGap = 88f;
    const float BoardCenterY = 45f;
    const float WireThickness = 8f;
    const float WrongSeconds = 1.2f;
    const int DustCount = 36;

    // ───────────── Estado ─────────────

    class Wire
    {
        public MemoryNeuron From, To;   // From siempre está en la columna de la izquierda
        public Image Image, Pulse;
        public float Length, Phase;
        public bool Done, Wrong;
    }

    class Dust
    {
        public RectTransform Rect;
        public float BaseX, Speed, Phase;
    }

    class Port
    {
        public Image Image;
        public Color Color;
        public float Phase;
    }

    class Spark
    {
        public Image Image;
        public Vector2 Velocity;
        public float Life, MaxLife;
    }

    readonly List<MemoryNeuron> neurons = new List<MemoryNeuron>();
    readonly List<Wire> wires = new List<Wire>();
    readonly List<Dust> dust = new List<Dust>();
    readonly List<Port> ports = new List<Port>();
    readonly List<Spark> sparks = new List<Spark>();
    readonly List<Image> progress = new List<Image>();

    CanvasGroup group;
    RectTransform board, wireLayer, scanLine;
    TextMeshProUGUI feedback, recovered;
    GameObject errorPanel;
    TextMeshProUGUI errorTitle, errorBody;
    GameObject continueButton;
    Image preview;

    MemoryNeuron dragStart;
    bool busy;
    int solved;
    Action<MemoryResult> onComplete;
    readonly MemoryResult result = new MemoryResult();

    GameManager.ModalLock modalLock;

    /// <summary>Errores cometidos hasta ahora (servirá para la puntuación de precisión).</summary>
    public int Errors => result.Errors;

    /// <summary>Abre el minijuego. Al terminar llama a onComplete con los errores y su detalle.</summary>
    public static MemoryReconstructionGame Open(Action<MemoryResult> onComplete)
    {
        var canvas = PixelUi.NewCanvas("MemoryReconstructionGame", 500);
        var game = canvas.gameObject.AddComponent<MemoryReconstructionGame>();
        game.onComplete = onComplete;
        game.Build();
        game.modalLock = GameManager.Instance.LockInput();   // Andrés queda quieto mientras dura el reto
        return game;
    }

    void OnDestroy()
    {
        // Si el minijuego desaparece por un cambio de escena, el jugador no puede quedar bloqueado (Release es idempotente).
        modalLock?.Release();
    }

    // ───────────── Construcción de la interfaz ─────────────

    void Build()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var backdrop = PixelUi.NewImage("Fondo", transform, Background);
        PixelUi.Stretch(backdrop.rectTransform);

        var ambient = PixelUi.NewRect("Ambiente", transform);
        PixelUi.Stretch(ambient);
        BuildAmbient(ambient);

        board = PixelUi.NewRect("Tablero", transform);
        PixelUi.Stretch(board);

        wireLayer = PixelUi.NewRect("Cables", board);
        PixelUi.Stretch(wireLayer);

        PixelUi.NewLabel("Titulo", board, "RECONSTRUCT YOUR MEMORY", 40, new Vector2(0f, 322f), new Vector2(1000f, 56f), FontStyles.Bold);
        var quote = PixelUi.NewLabel("Enfermera", board, "Lucía: \"The pieces must be connected, one by one.\"", 24,
            new Vector2(0f, 276f), new Vector2(1000f, 36f), FontStyles.Italic);
        quote.color = Muted;

        BuildProgress();

        var subjects = new List<(string text, bool plural)>();
        var verbs = new List<(string text, bool plural)>();
        var complements = new List<(string text, bool plural)>();
        foreach (var s in Sentences)
        {
            subjects.Add((s.Subject, s.Plural));
            verbs.Add((s.Verb, s.Plural));
            complements.Add((s.Complement, s.Plural));
        }
        verbs.Add((DistractorVerb, DistractorPlural));

        BuildColumn(subjects, 0);
        BuildColumn(verbs, 1);
        BuildColumn(complements, 2);

        for (int column = 0; column < 3; column++)
        {
            var caption = PixelUi.NewLabel("Columna " + ColumnNames[column], board, ColumnNames[column], 20,
                new Vector2((column - 1) * ColumnGap, 248f), new Vector2(300f, 28f), FontStyles.Bold);
            caption.color = RoleColors[column];
        }

        feedback = PixelUi.NewLabel("Respuesta", board, "Drag a wire from one neuron to the next one to rebuild a sentence.", 25,
            new Vector2(0f, -160f), new Vector2(1200f, 40f));
        feedback.color = Muted;

        recovered = PixelUi.NewLabel("Recuerdos", board, "", 26, new Vector2(0f, -258f), new Vector2(900f, 120f));
        recovered.color = WireDone;

        BuildErrorPanel();

        continueButton = BuildContinueButton();
        continueButton.SetActive(false);

        preview = NewWireImage("Cable en curso", WireNeutral);
        preview.gameObject.SetActive(false);
    }

    /// <summary>Partículas que flotan y una línea de escaneo que sube por la pantalla.</summary>
    void BuildAmbient(RectTransform parent)
    {
        for (int i = 0; i < DustCount; i++)
        {
            float size = UnityEngine.Random.Range(5f, 10f);
            var tint = i % 3 == 0 ? RoleColors[1] : (i % 3 == 1 ? new Color(0.55f, 0.5f, 1f) : Muted);
            var image = PixelUi.NewImage("Particula", parent, new Color(tint.r, tint.g, tint.b, UnityEngine.Random.Range(0.12f, 0.35f)));
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
            var d = new Dust
            {
                Rect = rect,
                BaseX = UnityEngine.Random.Range(-640f, 640f),
                Speed = UnityEngine.Random.Range(8f, 28f),
                Phase = UnityEngine.Random.Range(0f, 6.28f),
            };
            rect.anchoredPosition = new Vector2(d.BaseX, UnityEngine.Random.Range(-360f, 360f));
            dust.Add(d);
        }

        var line = PixelUi.NewImage("Linea de escaneo", parent, new Color(RoleColors[1].r, RoleColors[1].g, RoleColors[1].b, 0.07f));
        line.raycastTarget = false;
        scanLine = line.rectTransform;
        scanLine.anchorMin = new Vector2(0f, 0.5f);
        scanLine.anchorMax = new Vector2(1f, 0.5f);
        scanLine.sizeDelta = new Vector2(0f, 10f);
    }

    /// <summary>Tres casillas arriba a la derecha: una por cada recuerdo recuperado.</summary>
    void BuildProgress()
    {
        var label = PixelUi.NewLabel("Progreso", board, "MEMORIES", 18, new Vector2(500f, 338f), new Vector2(200f, 24f), FontStyles.Bold);
        label.color = Muted;
        for (int i = 0; i < Sentences.Length; i++)
        {
            var slot = PixelUi.NewPanel("Casilla", board, Muted, NodeFill, out var fill);
            slot.raycastTarget = false;
            PixelUi.Place(slot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(440f + i * 60f, 304f), new Vector2(44f, 28f));
            progress.Add(fill);
        }
    }

    void BuildColumn(List<(string text, bool plural)> items, int column)
    {
        ErrorHuntContent.Shuffle(items);
        float top = BoardCenterY + (items.Count - 1) * RowGap * 0.5f;
        for (int i = 0; i < items.Count; i++)
        {
            var position = new Vector2((column - 1) * ColumnGap, top - i * RowGap);
            neurons.Add(BuildNeuron(items[i].text, column, items[i].plural, position));
        }
    }

    MemoryNeuron BuildNeuron(string text, int column, bool plural, Vector2 position)
    {
        var role = RoleColors[column];
        var outline = PixelUi.NewPanel("Neurona: " + text, board, role, NodeFill, out var fill);
        var rect = outline.rectTransform;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(MemoryNeuron.Width, MemoryNeuron.Height);

        var label = PixelUi.NewLabel("Texto", rect, text, 26, Vector2.zero, new Vector2(MemoryNeuron.Width - 24f, MemoryNeuron.Height));

        var neuron = outline.gameObject.AddComponent<MemoryNeuron>();
        neuron.Setup(this, text, column, plural, outline, fill, label, role);

        if (column > 0) AddPort(rect, false, role);
        if (column < 2) AddPort(rect, true, role);
        return neuron;
    }

    void AddPort(RectTransform parent, bool right, Color color)
    {
        var image = PixelUi.NewImage("Conexion", parent, color);
        image.raycastTarget = false;
        image.rectTransform.anchoredPosition = new Vector2(right ? MemoryNeuron.Width * 0.5f : -MemoryNeuron.Width * 0.5f, 0f);
        image.rectTransform.sizeDelta = new Vector2(18f, 18f);
        ports.Add(new Port { Image = image, Color = color, Phase = UnityEngine.Random.Range(0f, 6.28f) });
    }

    /// <summary>Panel rojo que explica el error en el momento en que ocurre. Queda visible hasta el siguiente intento.</summary>
    void BuildErrorPanel()
    {
        var panel = PixelUi.NewPanel("Feedback del error", board, WireWrong, new Color(0.14f, 0.05f, 0.08f), out _);
        panel.raycastTarget = false;
        PixelUi.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f), new Vector2(1120f, 160f));

        errorTitle = PixelUi.NewLabel("Titulo", panel.transform, "", 26, Vector2.zero, new Vector2(1070f, 34f), FontStyles.Bold, TextAlignmentOptions.Left);
        errorTitle.color = WireWrong;
        PixelUi.Place(errorTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(1070f, 34f));

        errorBody = PixelUi.NewLabel("Texto", panel.transform, "", 25, Vector2.zero, new Vector2(1070f, 100f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        errorBody.textWrappingMode = TextWrappingModes.Normal;
        PixelUi.Place(errorBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -50f), new Vector2(1070f, 100f));

        errorPanel = panel.gameObject;
        errorPanel.SetActive(false);
    }

    void ShowError(string title, string body)
    {
        errorTitle.text = title;
        errorBody.text = body;
        errorPanel.SetActive(true);
        recovered.gameObject.SetActive(false);   // ocupa el mismo espacio; vuelve al cerrar el panel
        feedback.text = "";
    }

    void HideError()
    {
        if (errorPanel == null || !errorPanel.activeSelf) return;
        errorPanel.SetActive(false);
        recovered.gameObject.SetActive(true);
        feedback.color = Muted;
        feedback.text = "Try again: connect the pieces so the sentence makes sense.";
    }

    GameObject BuildContinueButton()
    {
        var button = PixelUi.NewPanel("Continuar", board, WireDone, NodeDoneFill, out _);
        PixelUi.Place(button.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500f, -300f), new Vector2(200f, 66f));
        PixelUi.NewLabel("Texto", button.rectTransform, "CONTINUE", 26, Vector2.zero, new Vector2(200f, 66f), FontStyles.Bold);
        button.gameObject.AddComponent<Button>().onClick.AddListener(Finish);
        return button.gameObject;
    }

    // ───────────── Animación ─────────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        if (group.alpha < 1f) group.alpha = Mathf.Min(1f, group.alpha + dt * 3f);

        foreach (var d in dust)
        {
            var p = d.Rect.anchoredPosition;
            p.y += d.Speed * dt;
            if (p.y > 380f) p.y = -380f;
            p.x = d.BaseX + Mathf.Sin(now * 0.5f + d.Phase) * 18f;
            d.Rect.anchoredPosition = p;
        }

        scanLine.anchoredPosition = new Vector2(0f, Mathf.Repeat(now * 90f, 780f) - 390f);

        foreach (var port in ports)
        {
            float a = 0.55f + 0.45f * Mathf.Sin(now * 2.4f + port.Phase);
            port.Image.color = new Color(port.Color.r, port.Color.g, port.Color.b, a);
        }

        // Impulso que recorre cada cable: lento mientras se piensa, rápido cuando la frase ya es correcta.
        foreach (var wire in wires)
        {
            if (wire.Wrong) continue;
            float t = Mathf.Repeat(now * (wire.Done ? 1.5f : 0.8f) + wire.Phase, 1f);
            wire.Pulse.rectTransform.anchoredPosition = new Vector2(t * wire.Length, 0f);
        }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var spark = sparks[i];
            spark.Life -= dt;
            if (spark.Life <= 0f)
            {
                Destroy(spark.Image.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            spark.Velocity *= 0.94f;
            spark.Image.rectTransform.anchoredPosition += spark.Velocity * dt;
            var c = spark.Image.color;
            spark.Image.color = new Color(c.r, c.g, c.b, spark.Life / spark.MaxLife);
        }
    }

    void Burst(Vector2 position, Color color)
    {
        for (int i = 0; i < 10; i++)
        {
            var image = PixelUi.NewImage("Chispa", board, color);
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(10f, 10f);
            image.rectTransform.anchoredPosition = position;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(120f, 320f);
            float life = UnityEngine.Random.Range(0.4f, 0.8f);
            sparks.Add(new Spark
            {
                Image = image,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Life = life,
                MaxLife = life,
            });
        }
    }

    IEnumerator Pop(RectTransform rect)
    {
        const float duration = 0.3f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            rect.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(t / duration * Mathf.PI));
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    // ───────────── Arrastre de cables ─────────────

    public void BeginWire(MemoryNeuron neuron, PointerEventData eventData)
    {
        if (busy || neuron.Locked) return;
        HideError();   // el jugador ya va a intentarlo de nuevo
        dragStart = neuron;
        preview.gameObject.SetActive(true);
        DragWire(eventData);
    }

    public void DragWire(PointerEventData eventData)
    {
        if (dragStart == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, eventData.position, null, out var pointer);
        bool towardRight = pointer.x >= dragStart.Rect.anchoredPosition.x;
        PlaceWire(preview, dragStart.Port(towardRight), pointer);
    }

    public void EndWire()
    {
        dragStart = null;
        preview.gameObject.SetActive(false);
    }

    public void DropWire(MemoryNeuron target)
    {
        if (dragStart != null) TryConnect(dragStart, target);
    }

    /// <summary>Conecta dos neuronas de columnas vecinas y revisa si ya se formó una frase.</summary>
    public bool TryConnect(MemoryNeuron a, MemoryNeuron b)
    {
        if (busy || a == null || b == null || a.Locked || b.Locked) return false;
        if (Mathf.Abs(a.Column - b.Column) != 1) return false;
        HideError();

        var from = a.Column < b.Column ? a : b;
        var to = a.Column < b.Column ? b : a;

        // Cada neurona admite un solo cable por lado: el cable nuevo reemplaza al anterior.
        for (int i = wires.Count - 1; i >= 0; i--)
        {
            if (wires[i].From == from || wires[i].To == to) RemoveWire(wires[i]);
        }

        var wire = new Wire { From = from, To = to, Image = NewWireImage("Cable", WireNeutral), Phase = UnityEngine.Random.value };
        wire.Pulse = PixelUi.NewImage("Impulso", wire.Image.transform, Color.white);
        wire.Pulse.raycastTarget = false;
        wire.Pulse.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        wire.Pulse.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        wire.Pulse.rectTransform.sizeDelta = new Vector2(16f, 16f);
        wire.Length = PlaceWire(wire.Image, from.Port(true), to.Port(false));
        wires.Add(wire);
        CheckSentence(from.Column == 0 ? to : from);
        return true;
    }

    void RemoveWire(Wire wire)
    {
        wires.Remove(wire);
        Destroy(wire.Image.gameObject);   // el impulso es hijo del cable
    }

    Wire FindWire(Func<Wire, bool> match)
    {
        foreach (var wire in wires) if (match(wire)) return wire;
        return null;
    }

    // ───────────── Revisión de las frases ─────────────

    /// <summary>Una pieza equivocada: cuál es, qué cable la une mal y por qué (texto plano, sin etiquetas).</summary>
    class Issue
    {
        public MemoryNeuron Piece;
        public Wire Wire;
        public string Text;
    }

    void CheckSentence(MemoryNeuron verb)
    {
        var left = FindWire(w => w.To == verb);
        var right = FindWire(w => w.From == verb);
        if (left == null) return;   // sin sujeto todavía no se puede juzgar el verbo

        // Con solo el primer enlace (sujeto → verbo) ya se avisa de lo que falla, sin esperar al final
        if (right == null)
        {
            var early = Diagnose(left, null);
            if (early.Count > 0) StartCoroutine(RejectSentence(left, null, early));
            return;
        }

        // Una sola fuente de verdad: la frase es correcta cuando el diagnóstico no encuentra nada
        var issues = Diagnose(left, right);
        if (issues.Count == 0) AcceptSentence(left, right);
        else StartCoroutine(RejectSentence(left, right, issues));
    }

    /// <summary>
    /// Revisa cada enlace por separado y devuelve TODO lo que falla (concordancia, verbo que no cuadra con el sujeto,
    /// final que no cuadra), en vez de culpar siempre al complemento. right es null si solo hay un enlace.
    /// </summary>
    List<Issue> Diagnose(Wire left, Wire right)
    {
        var issues = new List<Issue>();
        var subject = left.From;
        var verb = left.To;
        var complement = right?.To;

        bool found = TryFindSentence(subject.Text, out var own);

        // El verbo y el final forman una frase de otro sujeto: lo que no encaja es el sujeto
        if (complement != null)
        {
            foreach (var s in Sentences)
            {
                if (s.Verb == verb.Text && s.Complement == complement.Text && s.Subject != subject.Text)
                {
                    issues.Add(new Issue
                    {
                        Piece = subject, Wire = left,
                        Text = $"The verb and the ending belong together, but \"{subject.Text}\" does not fit with them. Who or what receives that action?",
                    });
                    return issues;
                }
            }
        }

        if (subject.IsPlural != verb.IsPlural)
        {
            string number = subject.IsPlural ? "plural" : "singular";
            string forms = subject.IsPlural ? "were / are" : "was / is";
            issues.Add(new Issue
            {
                Piece = verb, Wire = left,
                Text = $"Agreement: \"{subject.Text}\" is {number}, so the passive needs {forms}.",
            });
        }
        else if (found && verb.Text != own.Verb)
        {
            issues.Add(new Issue
            {
                Piece = verb, Wire = left,
                Text = $"Meaning: \"{verb.Text}\" does not fit \"{subject.Text}\". Think about what really happens to it.",
            });
        }

        if (complement != null && found && complement.Text != own.Complement)
        {
            string tip = Errors >= 2 ? " Tip: read the whole sentence aloud and ask yourself if it could be true." : "";
            issues.Add(new Issue
            {
                Piece = complement, Wire = right,
                Text = $"Meaning: the ending does not fit the idea. Think: {own.Clue}{tip}",
            });
        }
        return issues;
    }

    static bool TryFindSentence(string subject, out Sentence found)
    {
        foreach (var s in Sentences)
        {
            if (s.Subject == subject)
            {
                found = s;
                return true;
            }
        }
        found = default;
        return false;
    }

    void AcceptSentence(Wire left, Wire right)
    {
        AudioManager.PlayCorrect();   // acierto: el mismo instante en que aparece el feedback
        HideError();
        foreach (var wire in new[] { left, right })
        {
            wire.Done = true;
            wire.Image.color = WireDone;
            wire.From.Locked = true;
            wire.To.Locked = true;
        }

        foreach (var neuron in new[] { left.From, left.To, right.To })
        {
            neuron.Outline.color = WireDone;
            neuron.Body.color = NodeDoneFill;
            Burst(neuron.Rect.anchoredPosition, WireDone);
            StartCoroutine(Pop(neuron.Rect));
        }

        progress[solved].color = WireDone;
        solved++;
        recovered.text += (recovered.text.Length > 0 ? "\n" : "") + $"{left.From.Text} {left.To.Text} {right.To.Text}";

        feedback.color = WireDone;
        if (solved < Sentences.Length)
        {
            feedback.text = "A memory has been recovered.";
            return;
        }

        feedback.text = "Your memories have been restored.";
        foreach (var neuron in neurons)
        {
            if (!neuron.Locked) neuron.gameObject.SetActive(false);   // queda oculto el verbo sobrante
        }
        continueButton.SetActive(true);
    }

    IEnumerator RejectSentence(Wire left, Wire right, List<Issue> issues)
    {
        AudioManager.PlayIncorrect();
        busy = true;
        var wiresInvolved = right != null ? new[] { left, right } : new[] { left };
        var subject = left.From;
        var verb = left.To;
        var complement = right?.To;
        RecordMistake(subject, verb, complement, issues);

        // Se marcan en rojo TODAS las piezas equivocadas y los cables que las unen mal
        var wrongPieces = new List<MemoryNeuron>();
        foreach (var issue in issues) wrongPieces.Add(issue.Piece);

        foreach (var wire in wiresInvolved)
        {
            wire.Wrong = true;
            wire.Pulse.gameObject.SetActive(false);
            wire.Image.color = issues.Exists(i => i.Wire == wire) ? WireWrong : WireNeutral;
        }
        foreach (var piece in wrongPieces) piece.Outline.color = WireWrong;

        string Mark(MemoryNeuron n) => wrongPieces.Contains(n) ? $"<color=#FF6060><b>{n.Text}</b></color>" : n.Text;
        string attempt = complement != null
            ? $"\"{Mark(subject)} {Mark(verb)} {Mark(complement)}\""
            : $"\"{Mark(subject)} {Mark(verb)} ...\"";

        var body = new System.Text.StringBuilder(attempt);
        foreach (var issue in issues) body.Append("\n- ").Append(issue.Text);
        ShowError(issues.Count > 1 ? "Not quite! More than one link needs another look." : "Not quite! This link does not work.",
            body.ToString());

        for (float t = 0f; t < WrongSeconds; t += Time.unscaledDeltaTime)
        {
            float strength = Mathf.Clamp01(1f - t / 0.4f);   // la sacudida dura solo el primer momento
            board.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * 10f * strength, 0f);
            yield return null;
        }
        board.anchoredPosition = Vector2.zero;

        foreach (var piece in wrongPieces) piece.Outline.color = piece.RoleColor;
        foreach (var wire in wiresInvolved) RemoveWire(wire);
        busy = false;
    }

    /// <summary>Guarda el intento para el repaso final (sin repetir el mismo intento).</summary>
    void RecordMistake(MemoryNeuron subject, MemoryNeuron verb, MemoryNeuron complement, List<Issue> issues)
    {
        result.Errors++;

        string attempt = complement != null ? $"{subject.Text} {verb.Text} {complement.Text}" : $"{subject.Text} {verb.Text} ...";
        foreach (var known in result.Mistakes) if (known.Attempt == attempt) return;

        string correct = attempt;
        if (TryFindSentence(subject.Text, out var own)) correct = $"{own.Subject} {own.Verb} {own.Complement}";

        var explanation = new System.Text.StringBuilder();
        foreach (var issue in issues)
        {
            if (explanation.Length > 0) explanation.Append(' ');
            explanation.Append(issue.Text);
        }

        result.Mistakes.Add(new MemoryMistake { Attempt = attempt, Explanation = explanation.ToString(), Correct = correct });
    }

    void Finish()
    {
        modalLock?.Release();
        var callback = onComplete;
        Destroy(gameObject);
        callback?.Invoke(result);
    }

    // ───────────── Utilidades ─────────────

    Image NewWireImage(string objectName, Color color)
    {
        var image = PixelUi.NewImage(objectName, wireLayer, color);
        image.raycastTarget = false;
        image.rectTransform.pivot = new Vector2(0f, 0.5f);
        return image;
    }

    static float PlaceWire(Image wire, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var rect = wire.rectTransform;
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(delta.magnitude, WireThickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        return delta.magnitude;
    }

}
