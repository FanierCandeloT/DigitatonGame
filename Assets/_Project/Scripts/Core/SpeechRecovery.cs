using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Cómo habla Andrés según cuánto ha recuperado.</summary>
public enum SpeechStage { Confused, Mixed, Fluent }

/// <summary>
/// Mecánica "Andrés recupera el habla": tras el accidente solo habla un español entrecortado; con cada recuerdo recuperado
/// le vuelven palabras en inglés (se resaltan) hasta que habla con fluidez. Las líneas de Andrés se escriben en tres versiones
/// (ver DialogueLine) y aquí se elige la que toca. Los umbrales se ajustan en MixedAt / FluentAt.
/// </summary>
public static class SpeechRecovery
{
    /// <summary>Recuerdos necesarios para empezar a mezclar inglés.</summary>
    public const int MixedAt = 4;

    /// <summary>Recuerdos necesarios para hablar con fluidez.</summary>
    public const int FluentAt = 8;

    public const string SpeakerName = "Andrés";

    const string WordColor = "6FD9FF";   // las palabras recuperadas se ven en cian

    public static SpeechStage StageFor(int memories) =>
        memories >= FluentAt ? SpeechStage.Fluent : (memories >= MixedAt ? SpeechStage.Mixed : SpeechStage.Confused);

    public static SpeechStage Stage => StageFor(GameManager.Instance.MemoriesRecovered);

    /// <summary>De 0 a 1: qué tanto del habla ha vuelto (para el HUD).</summary>
    public static float Progress => Mathf.Clamp01(GameManager.Instance.MemoriesRecovered / (float)GameManager.MaxMemories);

    public static bool IsAndres(DialogueLine line) => line != null && line.speaker == SpeakerName;

    /// <summary>Texto que se muestra para esta línea: la versión de Andrés que corresponde a su etapa actual.</summary>
    public static string Resolve(DialogueLine line, SpeechStage stage)
    {
        if (line == null) return "";
        if (!IsAndres(line) || line.fluent) return line.text;

        switch (stage)
        {
            case SpeechStage.Confused:
                return string.IsNullOrEmpty(line.spanish) ? line.text : line.spanish;
            case SpeechStage.Mixed:
                return string.IsNullOrEmpty(line.mixed) ? line.text : Highlight(line.mixed);
            default:
                return line.text;
        }
    }

    /// <summary>Las palabras entre {llaves} son las que ya recuperó en inglés: se pintan de cian.</summary>
    public static string Highlight(string mixed) =>
        Regex.Replace(mixed, @"\{(.+?)\}", $"<color=#{WordColor}>$1</color>");

    /// <summary>Largo del texto sin etiquetas (para la máquina de escribir).</summary>
    public static int VisibleLength(string richText) => Regex.Replace(richText, "<.*?>", "").Length;

    public static string StageName(SpeechStage stage)
    {
        switch (stage)
        {
            case SpeechStage.Confused: return "confused";
            case SpeechStage.Mixed: return "coming back";
            default: return "fluent";
        }
    }

    public static Color StageColor(SpeechStage stage)
    {
        switch (stage)
        {
            case SpeechStage.Confused: return new Color(1f, 0.78f, 0.34f);
            case SpeechStage.Mixed: return new Color(0.44f, 0.85f, 1f);
            default: return new Color(0.4f, 0.95f, 0.62f);
        }
    }

    /// <summary>Aviso cuando el habla mejora de etapa; null si no cambió.</summary>
    public static string ImprovementMessage(int before, int after)
    {
        var from = StageFor(before);
        var to = StageFor(after);
        if (to == from || to < from) return null;
        return to == SpeechStage.Mixed
            ? "Your speech is coming back! A few English words return to you."
            : "Your English is fully restored!";
    }
}
