using UnityEngine;

/// <summary>
/// Columna vertebral de la historia: dónde empieza el juego, la introducción y el orden de los retos con su objetivo
/// (lo que el HUD le dice al jugador que haga ahora). El guion completo está en Docs/HISTORIA.md.
///
/// Orden: introducción (accidente) → R1 hospital → R2 campus → R3 exámenes → R4 recuerdo de clase → R5 confrontación → cierre.
/// Un paso con Built = false todavía no existe en el juego: el objetivo lo salta. Al construirlo, solo hay que poner Built = true.
/// </summary>
public static class StoryFlow
{
    /// <summary>El juego empieza en el hospital: el accidente ya ocurrió y se cuenta en la introducción.</summary>
    public const string StartScene = "Bienestar";
    public const string StartSpawn = "Bienestar_Bed";

    public const string FlagIntroDone = "intro_done";

    /// <summary>Segundos que dura cada pantalla de la introducción (se puede saltar con cualquier tecla o clic).</summary>
    public const float CaptionSeconds = 4.6f;

    public static readonly string[] IntroCaptions =
    {
        "Andrés taught English at the Universidad Autónoma de Occidente.",
        "In this country, everybody speaks in the passive voice. Things are done. Nobody says who did them.",
        "Andrés believed that every action has a doer.",
        "Then, one day, an accident happened.",
        "A scaffold was left unsecured. By whom? Nobody knows.",
        "Your memory has been lost.",
    };

    public class Step
    {
        public readonly string Id;          // el reto se marca como "done:{Id}" al completarse
        public readonly string Objective;   // texto del HUD (en inglés, como el resto del juego)
        public readonly bool Built;

        public Step(string id, string objective, bool built)
        {
            Id = id;
            Objective = objective;
            Built = built;
        }
    }

    public static readonly Step[] Steps =
    {
        new Step("q1_4", "Talk to Lucía, the nurse, to recover your first memories.", true),
        new Step("r2_litter", "A student with a \"!\" near Aulas 1 is throwing bottles. Help her.", true),
        new Step("r3_exams", "Find your students' exams in the Teachers' Room (inside Aulas 3).", false),
        new Step("class1", "Remember your last class: use the podium in Aulas 2.", true),
        new Step("r5_confrontation", "Face the contractor at the construction site.", false),
    };

    /// <summary>Texto del objetivo actual: el primer reto construido que todavía no se completó.</summary>
    public static string CurrentObjective(GameManager gm)
    {
        // En modo desarrollo (Play directo en una escena) también se muestra el objetivo aunque no se haya visto la introducción
        if (gm == null || !(gm.HasFlag(FlagIntroDone) || gm.HasFlag(GameManager.FlagDevOpen))) return "";
        foreach (var step in Steps)
        {
            if (!step.Built) continue;
            if (!gm.IsChallengeDone(step.Id)) return step.Objective;
        }
        return "More memories are still missing... (in development)";
    }

    /// <summary>Cuántos pasos construidos lleva el jugador (para mostrar el avance).</summary>
    public static void Progress(GameManager gm, out int done, out int total)
    {
        done = 0;
        total = 0;
        foreach (var step in Steps)
        {
            if (!step.Built) continue;
            total++;
            if (gm != null && gm.IsChallengeDone(step.Id)) done++;
        }
    }
}
