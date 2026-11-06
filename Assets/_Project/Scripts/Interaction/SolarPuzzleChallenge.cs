using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reto final (ODS 8 y 12): los paneles solares del techo de Aulas 3. Después de la clase se fue la luz de todo el campus
/// (BlackoutDirector) y los paneles quedaron desconectados. En la caja de control hay 3 circuitos caídos; cada uno se
/// reconecta diciendo, en voz pasiva y nombrando al agente, lo que dice una frase activa. Cada fila de paneles del techo
/// es un circuito: si la frase está bien, la fila se enciende y alumbra el techo; si no, chispea y el panel da una guía
/// (sin dar la respuesta) para volver a intentar. Los tres circuitos cubren tiempos distintos: pasado, presente perfecto y futuro.
/// Al conectar el tercero vuelve la luz (done:r6_solar), Andrés dice su frase final y aparece la pantalla de resultados.
///
/// Cumple el estándar de los retos: Andrés queda quieto (SentenceBuilder toma el bloqueo), el feedback dice qué pasó,
/// da una guía y permite reintentar, y suenan PlayCorrect / PlayIncorrect.
/// </summary>
public class SolarPuzzleChallenge : Interactable
{
    [Serializable]
    public class PanelRow
    {
        public SpriteRenderer[] panels;
    }

    [Tooltip("Id del reto (marca 'done:r6_solar' al completarlo).")]
    public string challengeId = BlackoutDirector.ChallengeId;

    [Min(0)] public int memoryReward = 2;

    [Tooltip("Una fila de paneles por circuito, en el orden en que se reconectan.")]
    public PanelRow[] rows;

    [Tooltip("Icono '!' sobre la caja de control (se enciende durante el apagón y se apaga al completar el reto).")]
    public GameObject icon;

    public enum Outcome { Success, Agreement, Tense, Participle, MissingBe, AgentForm, NeedsAgent, WrongAgent, WrongSubject }

    // ───────────── Los 3 circuitos ─────────────

    class Round
    {
        public string Clue;                 // frase activa que hay que decir en pasiva
        public string Passive;              // respuesta (para el cuaderno)
        public BuilderSlot[] Slots;
        public string Subject, Verb, Agent;
        public Dictionary<string, Outcome> VerbErrors;
        public Dictionary<string, Outcome> AgentErrors;   // agentes con un error propio (el resto de agentes incorrectos: WrongAgent)
    }

    static readonly Round[] Rounds =
    {
        // Pasado simple
        new Round
        {
            Clue = "The company cut the cables.",
            Passive = "The cables were cut by the company.",
            Slots = new[]
            {
                new BuilderSlot("Subject", new[] { "The cables", "The company", "The panels" }),
                new BuilderSlot("Passive\nverb", new[] { "were cut", "was cut", "are cut", "were cutting" }),
                new BuilderSlot("Agent\n(optional)", new[] { "by the company", "by the cables", "by the sun" }, optional: true),
            },
            Subject = "The cables", Verb = "were cut", Agent = "by the company",
            VerbErrors = new Dictionary<string, Outcome>
            {
                { "was cut", Outcome.Agreement }, { "are cut", Outcome.Tense }, { "were cutting", Outcome.Participle },
            },
            AgentErrors = new Dictionary<string, Outcome>(),
        },
        // Presente perfecto
        new Round
        {
            Clue = "The workers have just cleaned the panels.",
            Passive = "The panels have been cleaned by the workers.",
            Slots = new[]
            {
                new BuilderSlot("Subject", new[] { "The panels", "The workers", "The sun" }),
                new BuilderSlot("Passive\nverb", new[] { "have been cleaned", "has been cleaned", "were cleaned", "have cleaned", "are being cleaned" }),
                new BuilderSlot("Agent\n(optional)", new[] { "by the workers", "by the panels", "by the sun" }, optional: true),
            },
            Subject = "The panels", Verb = "have been cleaned", Agent = "by the workers",
            VerbErrors = new Dictionary<string, Outcome>
            {
                { "has been cleaned", Outcome.Agreement }, { "were cleaned", Outcome.Tense },
                { "have cleaned", Outcome.MissingBe }, { "are being cleaned", Outcome.Tense },
            },
            AgentErrors = new Dictionary<string, Outcome>(),
        },
        // Futuro: el agente es el propio Andrés ("by me"), que anticipa su frase final
        new Round
        {
            Clue = "I will restore the power.",
            Passive = "The power will be restored by me.",
            Slots = new[]
            {
                new BuilderSlot("Subject", new[] { "The power", "I", "The panels" }),
                new BuilderSlot("Passive\nverb", new[] { "will be restored", "will restore", "is restored", "will been restored", "restored" }),
                new BuilderSlot("Agent\n(optional)", new[] { "by me", "by I", "by the power" }, optional: true),
            },
            Subject = "The power", Verb = "will be restored", Agent = "by me",
            VerbErrors = new Dictionary<string, Outcome>
            {
                { "will restore", Outcome.MissingBe }, { "will been restored", Outcome.MissingBe },
                { "restored", Outcome.MissingBe }, { "is restored", Outcome.Tense },
            },
            AgentErrors = new Dictionary<string, Outcome> { { "by I", Outcome.AgentForm } },
        },
    };

    /// <summary>
    /// Evalúa una frase de un circuito. Orden: primero la forma del verbo (concordancia, tiempo, participio, 'be'),
    /// después qué cosa recibe la acción (el sujeto de la pasiva) y por último el agente: sin agente nadie actúa.
    /// </summary>
    public static Outcome Evaluate(int round, string subject, string verb, string agent)
    {
        var r = Rounds[Mathf.Clamp(round, 0, Rounds.Length - 1)];

        if (verb != r.Verb && r.VerbErrors.TryGetValue(verb, out var verbOutcome)) return verbOutcome;
        if (subject != r.Subject) return Outcome.WrongSubject;
        if (verb != r.Verb) return Outcome.Participle;   // un verbo que no está en la lista de errores conocidos: no es una pasiva bien formada
        if (string.IsNullOrEmpty(agent)) return Outcome.NeedsAgent;
        if (agent == r.Agent) return Outcome.Success;
        return r.AgentErrors.TryGetValue(agent, out var agentOutcome) ? agentOutcome : Outcome.WrongAgent;
    }

    // ───────────── Aspecto de los paneles ─────────────

    static readonly Color PanelOff = new Color(0.30f, 0.34f, 0.44f);
    static readonly Color PanelOn = Color.white;
    static readonly Color PanelSpark = new Color(1f, 0.38f, 0.28f);
    static readonly Color Warn = new Color(1f, 0.78f, 0.34f);

    // ───────────── Estado ─────────────

    SentenceBuilder builder;
    PlayerInteractor lastInteractor;
    bool running;
    int round;                         // circuito actual (los ya conectados se conservan si el jugador sale y vuelve)
    int attempts, errors, roundAttempts;
    readonly Dictionary<string, int> weak = new Dictionary<string, int>();

    void OnEnable() => GameManager.ProgressChanged += OnProgressChanged;
    void OnDisable() => GameManager.ProgressChanged -= OnProgressChanged;

    void Start()
    {
        RefreshLook();
    }

    void Update()
    {
        if (icon == null || !GameManager.Exists) return;
        var gm = GameManager.Instance;
        bool active = (gm.HasFlag(BlackoutDirector.FlagStarted) || gm.HasFlag(GameManager.FlagDevOpen)) && !gm.IsChallengeDone(challengeId);
        if (icon.activeSelf != active) icon.SetActive(active);
    }

    void OnProgressChanged()
    {
        if (!running) RefreshLook();
    }

    /// <summary>Paneles apagados durante el apagón, encendidos si todo está bien o ya se arregló.</summary>
    void RefreshLook()
    {
        if (rows == null || !GameManager.Exists) return;
        var gm = GameManager.Instance;
        bool on = gm.IsChallengeDone(challengeId) || !BlackoutDirector.IsDark;
        for (int i = 0; i < rows.Length; i++)
            SetRow(i, on || i < round, false);
    }

    void SetRow(int index, bool on, bool withLight)
    {
        if (rows == null || index < 0 || index >= rows.Length || rows[index].panels == null) return;
        foreach (var panel in rows[index].panels)
        {
            if (panel == null) continue;
            panel.color = on ? PanelOn : PanelOff;
            var glow = panel.transform.Find("Luz del panel");
            if (on && withLight && glow == null) glow = CreateGlow(panel.transform);
            if (glow != null) glow.gameObject.SetActive(on && withLight);
        }
    }

    /// <summary>Luz puntual cálida: cuando un circuito se conecta alumbra el techo a oscuras.</summary>
    static Transform CreateGlow(Transform panel)
    {
        var go = new GameObject("Luz del panel");
        go.transform.SetParent(panel, false);
        go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.95f, 0.72f);
        light.intensity = 1.1f;
        light.pointLightInnerRadius = 0.4f;
        light.pointLightOuterRadius = 3.6f;
        light.falloffIntensity = 0.55f;
        return go.transform;
    }

    // ───────────── Diálogos ─────────────

    static DialogueLine[] IntroLines() => new[]
    {
        new DialogueLine("Andrés",
            "This is the control box. Three circuits are down.",
            "Esta es la {control box}. Tres {circuits} están caídos.",
            "Esta es la caja de control. Tres circuitos están caídos."),
        new DialogueLine("Andrés",
            "Each circuit needs a sentence in the passive voice. And the doer must be named!",
            "Cada {circuit} necesita una frase en {passive voice}. ¡Y hay que nombrar a quien lo hace!",
            "Cada circuito necesita una frase en voz pasiva. ¡Y hay que nombrar a quien lo hace!"),
    };

    static DialogueLine[] ClosingLines() => new[]
    {
        new DialogueLine("Andrés",
            "The lights are back! The power was restored by me.",
            "¡Las luces {are back}! {The power was restored} {by me}.",
            "¡Volvió la luz! La energía fue restaurada por mí."),
        new DialogueLine("Andrés",
            "I remember everything now: who I am, and who did what.",
            "Ahora {I remember everything}: quién soy, y quién {did what}.",
            "Ahora lo recuerdo todo: quién soy, y quién hizo qué."),
        new DialogueLine("Andrés",
            "In this country, things were done by nobody. That ends today.",
            "En este país, todo {were done by nobody}. Eso {ends today}.",
            "En este país, todo fue hecho por nadie. Eso termina hoy."),
        new DialogueLine("Andrés",
            "The workers will be heard by me.",
            "{The workers will be heard} {by me}.",
            "Los trabajadores serán escuchados por mí."),
    };

    // ───────────── Flujo ─────────────

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;
        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show("The panels are working. The campus has power again!", 2.5f);
            return;
        }
        if (!gm.HasFlag(BlackoutDirector.FlagStarted) && !gm.HasFlag(GameManager.FlagDevOpen))
        {
            ToastMessage.Show("The control box is quiet. There is nothing to fix... yet.", 2.8f);
            return;
        }
        if (running) return;

        running = true;
        lastInteractor = interactor;
        if (round == 0)
        {
            attempts = errors = 0;
            weak.Clear();
            for (int i = 0; i < rows.Length; i++) SetRow(i, false, false);   // todo apagado (también en modo desarrollo)
        }
        DialoguePanel.Show(round == 0 ? IntroLines() : new DialogueLine[0], OpenRound);
    }

    void OpenRound()
    {
        roundAttempts = 0;
        var r = Rounds[round];
        string instruction = $"Circuit {round + 1} of {Rounds.Length}. Active: \"{r.Clue}\"  Say it in the passive voice.";
        builder = SentenceBuilder.Open(instruction, r.Slots, OnSubmit, OnLeave);
    }

    void OnLeave()
    {
        running = false;
    }

    void OnSubmit(string[] values)
    {
        StartCoroutine(React(values[0], values[1], values[2]));
    }

    IEnumerator React(string subject, string verb, string agent)
    {
        builder.Hide();
        attempts++;
        roundAttempts++;
        var outcome = Evaluate(round, subject, verb, agent);

        yield return new WaitForSeconds(0.35f);

        if (outcome == Outcome.Success)
        {
            AudioManager.PlayCorrect();
            yield return LightRow(round);
            round++;

            if (round >= Rounds.Length)
            {
                yield return Finish();
                yield break;
            }

            // Siguiente circuito: se cierra este constructor y se abre el siguiente (el bloqueo de Andrés se renueva en el mismo cuadro)
            builder.Close();
            ToastMessage.Show($"Circuit {round} connected! {Rounds.Length - round} to go.", 2f);
            OpenRound();
            yield break;
        }

        errors++;
        Track(outcome);
        AudioManager.PlayIncorrect();
        yield return Spark(round);
        builder.Show();
        builder.SetFeedback(Guidance(outcome), Warn);
    }

    IEnumerator LightRow(int index)
    {
        if (rows == null || index >= rows.Length || rows[index].panels == null) yield break;
        foreach (var panel in rows[index].panels)
        {
            if (panel == null) continue;
            panel.color = PanelOn;
            var glow = panel.transform.Find("Luz del panel");
            if (glow == null) glow = CreateGlow(panel.transform);
            glow.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.14f);
        }
        yield return new WaitForSeconds(0.25f);
    }

    /// <summary>El circuito chispea en rojo: la frase no lo reconectó.</summary>
    IEnumerator Spark(int index)
    {
        if (rows == null || index >= rows.Length || rows[index].panels == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            foreach (var panel in rows[index].panels) if (panel != null) panel.color = PanelSpark;
            yield return new WaitForSeconds(0.1f);
            foreach (var panel in rows[index].panels) if (panel != null) panel.color = PanelOff;
            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator Finish()
    {
        builder.Close();
        var gm = GameManager.Instance;
        gm.CompleteChallenge(challengeId, memoryReward);   // la luz vuelve sola (BlackoutDirector mira la marca done:r6_solar)
        gm.SetStat(challengeId + ":attempts", attempts);
        gm.SetStat(challengeId + ":errors", errors);
        gm.SetFeedback(challengeId, BuildFeedback());
        if (icon != null) icon.SetActive(false);
        ToastMessage.Show("All the circuits are connected!", 2.2f);
        base.Interact(lastInteractor);
        running = false;
        round = 0;

        // Al cerrar el diálogo final, termina el juego: pantalla de resultados
        DialoguePanel.Show(ClosingLines(), gm.ShowResults);
        yield break;
    }

    // ───────────── Guía y cuaderno ─────────────

    void Track(Outcome outcome)
    {
        string label = Label(outcome);
        GameManager.Instance.AddRuleMistake(label);
        weak[label] = weak.TryGetValue(label, out var n) ? n + 1 : 1;
    }

    static string Label(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Agreement: return ErrorHuntContent.Label(ErrorKind.Agreement);
            case Outcome.Tense: return ErrorHuntContent.Label(ErrorKind.Tense);
            case Outcome.Participle: return ErrorHuntContent.Label(ErrorKind.Participle);
            case Outcome.MissingBe: return ErrorHuntContent.Label(ErrorKind.MissingBe);
            case Outcome.AgentForm:
            case Outcome.NeedsAgent:
            case Outcome.WrongAgent: return ErrorHuntContent.Label(ErrorKind.Agent);
            default: return "Word order";
        }
    }

    /// <summary>Guía según el error: orienta sin dar la respuesta; desde el tercer intento del circuito explica la regla.</summary>
    string Guidance(Outcome outcome)
    {
        int level = roundAttempts >= 3 ? 1 : 0;
        switch (outcome)
        {
            case Outcome.Agreement: return ErrorHuntContent.Hint(ErrorKind.Agreement, level);
            case Outcome.Tense: return ErrorHuntContent.Hint(ErrorKind.Tense, level);
            case Outcome.Participle: return ErrorHuntContent.Hint(ErrorKind.Participle, level);
            case Outcome.MissingBe: return ErrorHuntContent.Hint(ErrorKind.MissingBe, level);
            case Outcome.NeedsAgent:
                return "Nothing happens... Who does the action? No doer is named, so nobody moves. Add the agent with 'by'.";
            case Outcome.AgentForm:
                return level == 0 ? "Almost! Look at the word after 'by'. Is it the right form for a person after a preposition?"
                                  : "After 'by' use the object form of a pronoun: me, him, her, us, them (not I, he, she, we, they).";
            case Outcome.WrongAgent:
                return "That cannot do the action! Read the active sentence again: who really does it?";
            default:   // WrongSubject
                return level == 0 ? "The circuit does not answer. In the passive, what goes first?"
                                  : "The passive starts with the thing that receives the action (the object of the active sentence).";
        }
    }

    string BuildFeedback()
    {
        var sb = new StringBuilder();
        if (errors == 0)
        {
            sb.Append("All three circuits were connected on the first try: receiver first, passive verb (be + participle) and 'by + doer'.\n");
        }
        else
        {
            sb.Append($"3 circuits connected in {attempts} tries, with {errors} mistake(s). Rules to review:\n");
            foreach (var pair in weak) sb.Append("\n- ").Append(pair.Key).Append(" (x").Append(pair.Value).Append(")");
            sb.Append("\n");
        }
        sb.Append("\nThe sentences:");
        foreach (var r in Rounds) sb.Append("\n- ").Append(r.Clue).Append("  >  ").Append(r.Passive);
        return sb.ToString();
    }
}
