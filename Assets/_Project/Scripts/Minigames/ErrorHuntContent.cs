using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Tipo de error gramatical de una frase. Cada tipo tiene su guía de ayuda (sin dar la respuesta).</summary>
public enum ErrorKind { None, Agreement, Tense, Participle, Agent, MissingBe }

/// <summary>Cómo se equivocó el jugador con una frase.</summary>
public enum MistakeType { Missed, FalseAlarm, WrongWord, WrongFix }

/// <summary>
/// Una frase de un estudiante, dividida en piezas. Si tiene error, una de las piezas (WrongIndex) es la equivocada,
/// Fix es su corrección y Distractors son las otras opciones (incorrectas) que se ofrecen al corregir.
/// </summary>
public class HuntSentence
{
    public readonly string[] Segments;
    public readonly int WrongIndex;
    public readonly string Fix;
    public readonly string[] Distractors;
    public readonly ErrorKind Kind;

    public bool HasError => WrongIndex >= 0;
    public string FullText => string.Join(" ", Segments);

    /// <param name="segments">Piezas separadas por '|'.</param>
    public HuntSentence(string segments, ErrorKind kind = ErrorKind.None, int wrongIndex = -1, string fix = null, params string[] distractors)
    {
        Segments = segments.Split('|');
        Kind = kind;
        WrongIndex = wrongIndex;
        Fix = fix;
        Distractors = distractors;
    }
}

/// <summary>
/// Banco de frases de la lección "Atrapa el error" (Passive Voice, nivel B1, temas de los ODS 8 y 12 y de la vida escolar),
/// con las guías que se muestran cuando el jugador no aprueba. Las guías orientan, no dan la respuesta.
/// </summary>
public static class ErrorHuntContent
{
    public static readonly HuntSentence[] Pool =
    {
        // ── Concordancia sujeto-verbo ──
        new HuntSentence("The workers|was paid|on time.", ErrorKind.Agreement, 1, "were paid", "is paid", "be paid"),
        new HuntSentence("Fair contracts|is signed|by every company.", ErrorKind.Agreement, 1, "are signed", "was signed", "be signed"),
        new HuntSentence("These products|was made|with recycled paper.", ErrorKind.Agreement, 1, "were made", "is made", "be made"),
        new HuntSentence("The new law|are explained|by the teacher.", ErrorKind.Agreement, 1, "is explained", "were explained", "be explained"),

        // ── Tiempo verbal ──
        new HuntSentence("Plastic|is recycled|yesterday.", ErrorKind.Tense, 1, "was recycled", "will be recycled", "are recycled"),
        new HuntSentence("The report|will be finished|last Monday.", ErrorKind.Tense, 1, "was finished", "is finished", "be finished"),
        new HuntSentence("The exams|were corrected|tomorrow.", ErrorKind.Tense, 1, "will be corrected", "had been corrected", "was corrected"),
        new HuntSentence("The wages|have been paid|last Friday.", ErrorKind.Tense, 1, "were paid", "are paid", "will be paid"),

        // ── Participio pasado ──
        new HuntSentence("The essays|were write|by the students.", ErrorKind.Participle, 1, "were written", "were wrote", "were writing"),
        new HuntSentence("Energy|is waste|in many offices.", ErrorKind.Participle, 1, "is wasted", "is wasting", "is wastes"),
        new HuntSentence("My lesson|was teach|in Room 204.", ErrorKind.Participle, 1, "was taught", "was teached", "was teaching"),
        new HuntSentence("All the plastic|was throw|away.", ErrorKind.Participle, 1, "was thrown", "was threw", "was throwing"),
        new HuntSentence("The report|has been write|by Ana.", ErrorKind.Participle, 1, "has been written", "has been wrote", "has been writing"),

        // ── Quien hace la acción (by + agente) ──
        new HuntSentence("The exams|were corrected|from the teacher.", ErrorKind.Agent, 2, "by the teacher.", "to the teacher.", "of the teacher."),
        new HuntSentence("These jobs|were created|of the new factory.", ErrorKind.Agent, 2, "by the new factory.", "to the new factory.", "on the new factory."),
        new HuntSentence("The windows|were broken|of the storm.", ErrorKind.Agent, 2, "by the storm.", "to the storm.", "on the storm."),

        // ── Falta el verbo "to be" ──
        new HuntSentence("The classroom|cleaned|every day.", ErrorKind.MissingBe, 1, "is cleaned", "cleaning", "clean"),
        new HuntSentence("The bottles|collected|every Friday.", ErrorKind.MissingBe, 1, "are collected", "collecting", "to collect"),
        new HuntSentence("These exams|corrected|by Professor Andrés.", ErrorKind.MissingBe, 1, "were corrected", "correcting", "to correct"),
        new HuntSentence("Plastic|must recycled|at school.", ErrorKind.MissingBe, 1, "must be recycled", "must recycling", "must to recycle"),
        new HuntSentence("The wages|have paid|by the company.", ErrorKind.MissingBe, 1, "have been paid", "have paying", "has been paid"),

        // ── Frases correctas (hay que dejarlas pasar) ──
        new HuntSentence("The workers|were paid|on time."),
        new HuntSentence("Fair contracts|are signed|by every company."),
        new HuntSentence("Plastic|is recycled|every day."),
        new HuntSentence("The essays|were written|by the students."),
        new HuntSentence("The report|will be finished|on Monday."),
        new HuntSentence("These products|were made|with recycled paper."),
        new HuntSentence("The exams|were corrected|by Professor Andrés."),
        new HuntSentence("Energy|is saved|in this building."),
        new HuntSentence("The bottles|are collected|every Friday."),
        new HuntSentence("My lesson|was taught|in Room 204."),
        new HuntSentence("The windows|have been cleaned|already."),
        new HuntSentence("Waste|should be reduced|in factories."),
        new HuntSentence("Safety helmets|must be worn|on the site."),
    };

    /// <summary>
    /// Arma una tanda de frases: reparte los tipos de error y da prioridad a los tipos en los que el jugador falló antes.
    /// </summary>
    public static List<HuntSentence> BuildRun(int errors, int correct, ICollection<ErrorKind> focus)
    {
        var wrongPool = Shuffle(Pool.Where(s => s.HasError).ToList());
        var count = new Dictionary<ErrorKind, int>();
        foreach (var kind in wrongPool.Select(s => s.Kind).Distinct())
            count[kind] = (focus != null && focus.Contains(kind)) ? -1 : 0;   // los tipos que fallaste salen primero

        var chosen = new List<HuntSentence>();
        while (chosen.Count < errors && wrongPool.Count > 0)
        {
            var next = wrongPool.OrderBy(s => count[s.Kind]).First();
            chosen.Add(next);
            wrongPool.Remove(next);
            count[next.Kind]++;
        }

        // Las correctas no usan el mismo sujeto que una frase con error ya elegida: así no se pueden comparar entre sí.
        var rightPool = Shuffle(Pool.Where(s => !s.HasError && !chosen.Any(c => c.Segments[0] == s.Segments[0])).ToList());
        chosen.AddRange(rightPool.Take(correct));
        return Shuffle(chosen);
    }

    public static string Label(ErrorKind kind)
    {
        switch (kind)
        {
            case ErrorKind.Agreement: return "Subject-verb agreement";
            case ErrorKind.Tense: return "Verb tense";
            case ErrorKind.Participle: return "Past participle";
            case ErrorKind.Agent: return "The doer of the action (by + agent)";
            case ErrorKind.MissingBe: return "The verb 'to be'";
            default: return "Correct sentences you stopped";
        }
    }

    /// <summary>Guía de ayuda: nivel 0 orienta; nivel 1 (después de fallar otra vez) explica la regla. Nunca da la respuesta.</summary>
    public static string Hint(ErrorKind kind, int level)
    {
        bool more = level >= 1;
        switch (kind)
        {
            case ErrorKind.Agreement:
                return more ? "Singular subjects use is / was. Plural subjects use are / were. Find the noun before the verb."
                            : "Look at the subject. Is it singular or plural? The verb 'to be' has to match it.";
            case ErrorKind.Tense:
                return more ? "Past: was / were. Present: is / are. Future: will be. Present perfect (have / has been) is not used with a finished time like 'last Friday'. Match the time clue."
                            : "Look for a time clue in the sentence (yesterday, tomorrow, every day...). The verb 'to be' shows the time.";
            case ErrorKind.Participle:
                return more ? "Passive = be + past participle. Irregular verbs change their form (see > seen, break > broken)."
                            : "After 'to be', the main verb needs a special form. Check that verb carefully.";
            case ErrorKind.Agent:
                return more ? "The person or thing that does the action comes after a connector. Check that little word."
                            : "Who does the action? A small word introduces the doer in a passive sentence. Check it.";
            case ErrorKind.MissingBe:
                return more ? "Passive = be (is / are / was / were) + past participle. After a modal (must, can, should) use 'be'; after have / has use 'been'. Both parts must be there."
                            : "A passive sentence has two parts. Is any part missing from the verb?";
            default:
                return "Before you stop a sentence, check three things: the subject, the verb 'to be' and the participle. If all three fit, let it pass.";
        }
    }

    /// <summary>Regla del reto 1: unir sujeto, verbo y complemento que tengan sentido juntos.</summary>
    public const string PiecesRule = "Matching the sentence pieces";

    /// <summary>Recordatorio corto de la regla que corresponde a una etiqueta de error (la usan el cuaderno y los resultados).</summary>
    public static string Reminder(string label)
    {
        if (label == Label(ErrorKind.Agreement)) return "'be' must match the subject (is / are, was / were).";
        if (label == Label(ErrorKind.Tense)) return "keep the tense of the active verb (present, past, present perfect, future).";
        if (label == Label(ErrorKind.Participle)) return "after 'be' use the past participle (written, taught, worn).";
        if (label == Label(ErrorKind.Agent)) return "'by' introduces the doer. Name the doer when it matters who is responsible.";
        if (label == Label(ErrorKind.MissingBe)) return "do not forget 'be' (modal + be, have + been).";
        if (label == Label(ErrorKind.None)) return "before stopping a sentence, check the subject, 'be' and the participle; if all three fit, let it pass.";
        if (label == PiecesRule) return "the subject, the passive verb and the ending must make sense together.";
        if (label == "Word order") return "the receiver goes first and 'by + doer' goes last.";
        return "the passive focuses on the receiver; the doer can be left out only when it is unknown or unimportant.";
    }

    public static string Describe(MistakeType type)
    {
        switch (type)
        {
            case MistakeType.Missed: return "You let this sentence pass:";
            case MistakeType.WrongWord: return "You stopped it, but pointed at the wrong part:";
            case MistakeType.WrongFix: return "You found the mistake, but the fix did not work:";
            default: return "You stopped a correct sentence:";
        }
    }

    public static List<T> Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
