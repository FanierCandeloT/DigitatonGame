using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Reto 2 (ODS 12): "la frase mueve el mundo". Una estudiante deja botellas junto a la caneca y habla solo en pasiva.
/// El jugador arma una frase con bloques (sujeto + verbo en pasiva + agente) y el mundo reacciona a lo que dice:
/// la frase correcta hace que la estudiante recoja las botellas; una frase con sentido contrario empeora el desorden;
/// y SIN agente nadie se mueve ("en este país, si nadie es nombrado, nadie actúa"), que es la lección del `by`.
/// Cuando no sale, el panel da una guía según el error, sin dar la respuesta.
/// </summary>
public class LitterChallenge : Interactable
{
    [Tooltip("Id del reto (marca 'done:r2_litter' al completarlo).")]
    public string challengeId = "r2_litter";

    [Min(0)] public int memoryReward = 2;

    [Header("Objetos del mundo (los pone el constructor del prefab)")]
    public Transform student;
    public Transform bin;
    [Tooltip("Las primeras 'startingBottles' están visibles; las demás aparecen si la frase empeora el desorden.")]
    public Transform[] bottles;
    public GameObject icon;
    [Min(1)] public int startingBottles = 3;

    [Tooltip("Solo para pruebas: salta el diálogo inicial.")]
    public bool skipIntro;

    enum Outcome { Success, NeedsAgent, AbsurdAgent, AbsurdSubject, Backfire, CarryBin, Agreement, Participle, Tense }

    // ───────────── Bloques ─────────────

    static readonly BuilderSlot[] Slots =
    {
        new BuilderSlot("Subject", new[] { "The bottles", "The bin", "The student", "The campus" }),
        new BuilderSlot("Passive\nverb", new[] { "are collected", "is collected", "are thrown", "are collecting", "were collected" }),
        new BuilderSlot("Agent\n(optional)", new[] { "by the student", "by the bottles", "by the bin" }, optional: true),
    };

    const string RightAgent = "by the student";
    static readonly Color Warn = new Color(1f, 0.78f, 0.34f);
    static readonly Color Good = new Color(0.4f, 0.95f, 0.62f);
    static readonly Color BinClean = new Color(0.45f, 0.8f, 0.5f);

    // ───────────── Estado ─────────────

    SentenceBuilder builder;
    PlayerInteractor lastInteractor;
    bool running;
    int attempts, errors;
    readonly Dictionary<string, int> weak = new Dictionary<string, int>();
    Transform root;
    Vector3 studentHome, binHome;
    static Sprite whiteSprite;

    void Start()
    {
        if (student == null) student = transform;
        root = transform.parent != null ? transform.parent : transform;
        studentHome = student.position;
        if (bin != null) binHome = bin.position;

        for (int i = 0; i < bottles.Length; i++) bottles[i].gameObject.SetActive(i < startingBottles);
        if (GameManager.Instance.IsChallengeDone(challengeId)) ApplyDoneLook();
    }

    // ───────────── Diálogos ─────────────

    static DialogueLine[] IntroLines() => new[]
    {
        new DialogueLine("Valentina", "The bottles are being thrown. The bin is being ignored. It is just... happening."),
        new DialogueLine("Andrés",
            "That is not right. The campus should be cleaned. Let me say it correctly.",
            "Eso no está bien. {The campus should be cleaned}. Déjame {say it correctly}.",
            "Eso no está bien. Hay que limpiar el campus. Déjame decirlo bien."),
    };

    static DialogueLine[] ClosingLines() => new[]
    {
        new DialogueLine("Valentina", "The bottles are being collected by me! The campus is being cleaned, thanks to one sentence."),
        new DialogueLine("Andrés",
            "When the doer is named, things happen. I remember teaching this.",
            "{When the doer is named}, las cosas pasan. Ya recuerdo cuando {I taught this}.",
            "Cuando se nombra a quien lo hace, las cosas pasan. Ya recuerdo que enseñaba esto."),
    };

    // ───────────── Flujo ─────────────

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;
        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show("Valentina: The campus is being kept clean. Thank you!", 2.5f);
            return;
        }
        if (running) return;

        running = true;
        lastInteractor = interactor;
        DialoguePanel.Show(skipIntro ? new DialogueLine[0] : IntroLines(), OpenBuilder);
    }

    void OpenBuilder()
    {
        builder = SentenceBuilder.Open("Say it in the passive voice, so the world can change.", Slots, OnSubmit, () => running = false);
    }

    void OnSubmit(string[] values)
    {
        StartCoroutine(React(values[0], values[1], values[2]));
    }

    IEnumerator React(string subject, string verb, string agent)
    {
        builder.Hide();
        attempts++;
        var outcome = Evaluate(subject, verb, agent);

        switch (outcome)
        {
            case Outcome.Success:
                yield return CollectAll();
                yield return Finish();
                yield break;

            case Outcome.Backfire:
                yield return MakeMess();
                break;
            case Outcome.CarryBin:
                yield return CarryBinAway();
                break;
            case Outcome.NeedsAgent:
                yield return Say("Collected... by whom? Nobody is named, so nobody moves.", 2.6f);
                break;
            case Outcome.AbsurdAgent:
                yield return Say(agent == "by the bin" ? "The bin cannot walk!" : "Bottles cannot collect bottles!", 2.2f);
                break;
            case Outcome.AbsurdSubject:
                yield return Say(subject == "The student" ? "I am not a bottle!" : "The campus cannot be carried!", 2.2f);
                break;
            case Outcome.Tense:
                yield return Say("They were collected? But they are still here!", 2.4f);
                break;
            default:   // concordancia o participio: la frase no se entiende
                yield return Say("I do not understand that sentence.", 2.0f);
                break;
        }

        errors++;
        string rule = RuleLabel(outcome);
        GameManager.Instance.AddRuleMistake(rule);
        weak[rule] = weak.TryGetValue(rule, out var count) ? count + 1 : 1;
        AudioManager.PlayIncorrect();
        builder.Show();
        builder.SetFeedback(Guidance(outcome), Warn);
    }

    IEnumerator Finish()
    {
        AudioManager.PlayCorrect();
        builder.Close();
        var gm = GameManager.Instance;
        gm.CompleteChallenge(challengeId, memoryReward);
        gm.SetStat(challengeId + ":attempts", attempts);
        gm.SetStat(challengeId + ":errors", errors);
        gm.SetFeedback(challengeId, BuildFeedback());
        ApplyDoneLook();
        base.Interact(lastInteractor);
        running = false;
        DialoguePanel.Show(ClosingLines(), null);
        yield break;
    }

    /// <summary>Texto del cuaderno: la frase correcta y, si hubo fallos, qué reglas repasar.</summary>
    string BuildFeedback()
    {
        string answer = "The bottles are collected by the student.";
        if (errors == 0)
            return $"Solved on the first try: \"{answer}\" The receiver comes first, then be + participle, and 'by' names the doer.";

        var sb = new System.Text.StringBuilder($"Solved in {attempts} tries, with {errors} mistake(s).\nThe sentence: \"{answer}\"\n\nRules to review:");
        foreach (var pair in weak)
            sb.Append("\n- ").Append(pair.Key).Append(" (x").Append(pair.Value).Append("): ").Append(ErrorHuntContent.Reminder(pair.Key));
        return sb.ToString();
    }

    // ───────────── Reglas ─────────────

    /// <summary>Evalúa la frase: primero la gramática (forma), luego el significado (qué pasa en el mundo).</summary>
    static Outcome Evaluate(string subject, string verb, string agent)
    {
        bool subjectPlural = subject == "The bottles";
        bool verbPlural = verb.StartsWith("are ") || verb.StartsWith("were ");

        if (verb == "are collecting") return Outcome.Participle;      // no es pasiva: -ing en vez de participio pasado
        if (subjectPlural != verbPlural) return Outcome.Agreement;    // el verbo no concuerda con el sujeto
        if (verb == "were collected") return Outcome.Tense;           // las botellas siguen ahí: es presente, no pasado
        if (subject == "The student" || subject == "The campus") return Outcome.AbsurdSubject;

        if (string.IsNullOrEmpty(agent)) return Outcome.NeedsAgent;   // sin agente nadie actúa
        if (agent != RightAgent) return Outcome.AbsurdAgent;

        if (subject == "The bin") return Outcome.CarryBin;
        return verb == "are thrown" ? Outcome.Backfire : Outcome.Success;
    }

    /// <summary>Regla que falló, con el mismo nombre que usan el cuaderno y los resultados.</summary>
    static string RuleLabel(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Agreement: return ErrorHuntContent.Label(ErrorKind.Agreement);
            case Outcome.Participle: return ErrorHuntContent.Label(ErrorKind.Participle);
            case Outcome.Tense: return ErrorHuntContent.Label(ErrorKind.Tense);
            case Outcome.NeedsAgent:
            case Outcome.AbsurdAgent: return ErrorHuntContent.Label(ErrorKind.Agent);
            default: return "The receiver of the action";   // sujeto absurdo, frase que empeora el desorden...
        }
    }

    /// <summary>Guía según el error: orienta sin dar la respuesta. Desde el tercer intento da más detalle.</summary>
    string Guidance(Outcome outcome)
    {
        int level = attempts >= 3 ? 1 : 0;
        switch (outcome)
        {
            case Outcome.Agreement: return ErrorHuntContent.Hint(ErrorKind.Agreement, level);
            case Outcome.Participle: return ErrorHuntContent.Hint(ErrorKind.Participle, level);
            case Outcome.Tense:
                return level == 0 ? "The bottles are on the ground right now. Is it something that already finished, or something that happens regularly? Choose the tense that fits."
                                  : "Present: is / are (it happens regularly). Past: was / were (it already finished). Choose the one that fits the situation.";
            case Outcome.NeedsAgent:
                return level == 0 ? "Nobody is named, so nobody moves. Who should do the action?"
                                  : "In a passive sentence, the doer is introduced by 'by'. Add a doer in the last row.";
            case Outcome.AbsurdAgent: return "That doer cannot do this action. Who can pick things up?";
            case Outcome.AbsurdSubject: return "What is on the ground and needs the action? Choose the subject again.";
            case Outcome.Backfire: return "The sentence is correct, but it made things worse. What should happen to the bottles?";
            case Outcome.CarryBin: return "Good grammar, but check what the action is about: should the bin or the bottles be collected?";
            default: return "Try again.";
        }
    }

    // ───────────── Reacciones del mundo ─────────────

    List<Transform> ActiveBottles()
    {
        var list = new List<Transform>();
        foreach (var b in bottles) if (b != null && b.gameObject.activeSelf) list.Add(b);
        return list;
    }

    /// <summary>La estudiante lleva cada botella a la caneca.</summary>
    IEnumerator CollectAll()
    {
        yield return Say("Collected by me! Sure!", 1.0f, wait: false);

        foreach (var bottle in ActiveBottles())
        {
            yield return MoveTo(student, bottle.position + new Vector3(-0.9f, 0f, 0f));
            bottle.SetParent(student, true);
            bottle.localPosition = new Vector3(0.7f, 0.3f, 0f);            // la lleva en la mano
            yield return MoveTo(student, binHome + new Vector3(1.2f, 0f, 0f));
            bottle.SetParent(root, true);
            yield return MoveTo(bottle, binHome + new Vector3(0f, 0.2f, 0f), 9f);
            yield return Shrink(bottle);
        }
        yield return MoveTo(student, studentHome);
    }

    /// <summary>Frase con sentido contrario: aparecen más botellas.</summary>
    IEnumerator MakeMess()
    {
        int shown = 0;
        foreach (var b in bottles)
        {
            if (b == null || b.gameObject.activeSelf || shown >= 3) continue;
            b.gameObject.SetActive(true);
            StartCoroutine(Pop(b));
            shown++;
        }
        yield return Say(shown > 0 ? "Thrown by me? Okay! Another one!" : "There is no more room for bottles!", 2.2f);
    }

    /// <summary>Frase con la caneca como sujeto: la estudiante se la lleva y la devuelve.</summary>
    IEnumerator CarryBinAway()
    {
        yield return MoveTo(student, binHome + new Vector3(1.2f, 0f, 0f));
        bin.SetParent(student, true);
        yield return Say("The bin is collected! Bye, bin!", 1.2f, wait: false);
        yield return MoveTo(student, studentHome + new Vector3(3f, 0f, 0f));
        yield return PixelUi.Wait(0.8f);
        bin.SetParent(root, true);
        yield return MoveTo(bin, binHome, 8f);
        yield return MoveTo(student, studentHome);
    }

    void ApplyDoneLook()
    {
        foreach (var b in bottles) if (b != null) b.gameObject.SetActive(false);
        if (icon != null) icon.SetActive(false);
        if (bin != null && bin.TryGetComponent(out SpriteRenderer sr)) sr.color = BinClean;
    }

    // ───────────── Animaciones sencillas ─────────────

    static IEnumerator MoveTo(Transform t, Vector3 target, float speed = 7f)
    {
        while ((t.position - target).sqrMagnitude > 0.0004f)
        {
            t.position = Vector3.MoveTowards(t.position, target, speed * Time.unscaledDeltaTime);
            yield return null;
        }
        t.position = target;
    }

    static IEnumerator Shrink(Transform t)
    {
        var start = t.localScale;
        for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime)
        {
            t.localScale = Vector3.Lerp(start, Vector3.zero, k / 0.25f);
            yield return null;
        }
        t.gameObject.SetActive(false);
        t.localScale = start;
    }

    static IEnumerator Pop(Transform t)
    {
        var end = t.localScale;
        for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime)
        {
            t.localScale = Vector3.Lerp(Vector3.zero, end, k / 0.25f);
            yield return null;
        }
        t.localScale = end;
    }

    // ───────────── Globo de texto sobre la estudiante ─────────────

    IEnumerator Say(string text, float seconds, bool wait = true)
    {
        if (whiteSprite == null)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        var go = new GameObject("Burbuja");
        go.transform.SetParent(student, false);
        // La burbuja va sobre la cabeza, sea cual sea el pivote del sprite (centro en la caja de color, pies en el sprite final)
        float head = student.TryGetComponent(out SpriteRenderer studentSprite) ? studentSprite.bounds.max.y - student.position.y : 1f;
        go.transform.localPosition = new Vector3(0f, head + 1.9f, 0f);

        var back = new GameObject("Fondo").AddComponent<SpriteRenderer>();
        back.transform.SetParent(go.transform, false);
        back.sprite = whiteSprite;
        back.color = new Color(0.05f, 0.07f, 0.15f, 0.92f);
        back.sortingOrder = 1999;
        back.transform.localScale = new Vector3(10f, 1.5f, 1f);

        var label = new GameObject("Texto").AddComponent<TextMeshPro>();
        label.transform.SetParent(go.transform, false);
        label.text = text;
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.rectTransform.sizeDelta = new Vector2(9.6f, 1.4f);
        label.sortingOrder = 2000;

        if (wait)
        {
            yield return PixelUi.Wait(seconds);
            if (go != null) Destroy(go);
        }
        else
        {
            Destroy(go, seconds);
        }
    }
}
