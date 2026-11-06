using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC que inicia un reto con un diálogo previo: primero conversan, y al terminar se abre el minijuego
/// de reconstrucción de memoria. Al completarlo suma los recuerdos, no se repite y cierra con un diálogo
/// que repasa los errores cometidos (el juego es educativo: el error se explica, no solo se cuenta).
/// Las líneas de Andrés cambian según cuánto ha recuperado el habla (ver SpeechRecovery).
/// </summary>
public class NpcChallengeInteractable : Interactable
{
    /// <summary>Máximo de errores que se repasan en el diálogo final, para que no se haga largo.</summary>
    const int MaxReviewed = 2;

    [Tooltip("Id único del reto. 'q1_4' abre los edificios del campus.")]
    public string challengeId = "q1_4";

    [Min(0)] public int memoryReward = 4;

    [Tooltip("Vacío = se usa el diálogo por defecto (definido en código, así no se pierde al combinar escenas con git).")]
    public DialogueLine[] introOverride;

    [Tooltip("Solo para pruebas: salta el diálogo inicial y abre el minijuego directo.")]
    public bool skipIntro;

    [TextArea] public string doneMessage = "Lucía: Take your time.";

    /// <summary>Diálogo inicial. Andrés trae sus tres versiones: inglés, mezcla (palabras recuperadas entre llaves) y español.</summary>
    static readonly DialogueLine[] DefaultIntro =
    {
        new DialogueLine("Andrés",
            "Everything is blank... Maybe talking to people will help me recover my memories, and who I am.",
            "{Everything is blank}... Quizás hablar con la gente me ayude a recuperar mis {memories}, y a saber {who I am}.",
            "Todo está en blanco... Quizás hablar con la gente me ayude a recuperar mis recuerdos, y a saber quién soy."),
        new DialogueLine("Lucía", "You were found near the campus yesterday. You were brought here by a student."),
        new DialogueLine("Lucía", "Don't worry about your Spanish. The accident damaged the part of your brain that stores English. It will come back as your memories are recovered."),
        new DialogueLine("Lucía", "Your memories can be repaired. The pieces must be connected, one by one."),
        new DialogueLine("Andrés",
            "I understand. Okay... let's try.",
            "Entiendo. {Okay}... {let's try}, paso a paso.",
            "Entiendo. Está bien... vamos a intentarlo."),
    };

    DialogueLine[] Intro
    {
        get
        {
            if (skipIntro) return new DialogueLine[0];
            return introOverride != null && introOverride.Length > 0 ? introOverride : DefaultIntro;
        }
    }

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;
        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show(doneMessage, 2.5f);
            return;
        }

        DialoguePanel.Show(Intro, () => MemoryReconstructionGame.Open(result => Complete(interactor, result)));
    }

    void Complete(PlayerInteractor interactor, MemoryResult result)
    {
        // Primero se suman los recuerdos: así el cierre ya muestra el habla de Andrés en su nueva etapa.
        GameManager.Instance.CompleteChallenge(challengeId, memoryReward);
        base.Interact(interactor);
        GameManager.Instance.SetFeedback(challengeId, BuildFeedback(result));
        // Al cerrar el diálogo Lucía ya le entregó el cuaderno: se abre con el mini tutorial (C). El aviso de "tu habla mejora" lo da MemoryHud.
        DialoguePanel.Show(BuildClosing(result), NotebookPanel.GiveToPlayer);
    }

    /// <summary>Texto del cuaderno para este reto: qué salió bien y cada error con su explicación.</summary>
    static string BuildFeedback(MemoryResult result)
    {
        if (result == null || result.Errors == 0)
            return "All the sentences were built perfectly: subject + be + past participle (+ by + agent).";

        var sb = new System.Text.StringBuilder($"Mistakes: {result.Errors}. Review each one:\n");
        foreach (var m in result.Mistakes)
            sb.Append($"\n- \"{m.Attempt}\"\n  {m.Explanation}\n  Correct: {m.Correct}\n");
        return sb.ToString();
    }

    /// <summary>Diálogo final: celebra el logro y, si hubo errores, los explica con la frase correcta.</summary>
    static DialogueLine[] BuildClosing(MemoryResult result)
    {
        var lines = new List<DialogueLine>
        {
            new DialogueLine("Lucía", "The pieces were connected. Your first memories have been restored!"),
        };

        if (result == null || result.Errors == 0)
        {
            lines.Add(new DialogueLine("Lucía", "No mistakes were made. All the sentences were built perfectly."));
        }
        else
        {
            lines.Add(new DialogueLine("Lucía", "A few pieces were mixed up on the way. The mistakes should be reviewed."));

            int reviewed = Mathf.Min(MaxReviewed, result.Mistakes.Count);
            for (int i = 0; i < reviewed; i++)
            {
                var m = result.Mistakes[i];
                lines.Add(new DialogueLine("Lucía", $"\"{m.Attempt}\"\n{m.Explanation}\nCorrect: {m.Correct}"));
            }

            lines.Add(new DialogueLine("Lucía", "Remember: the passive voice is formed with be + past participle, and be must agree with the subject."));
        }

        // Con 4 recuerdos Andrés ya mezcla inglés: aquí se nota la mecánica del habla
        lines.Add(new DialogueLine("Andrés",
            "I remember now... I'm a teacher. English was taught in my classroom.",
            "Ahora recuerdo... {I'm a teacher}. Yo enseñaba inglés: {English was taught} en mi salón.",
            "Ahora lo recuerdo... soy profesor. Enseñaba inglés en mi salón."));
        lines.Add(new DialogueLine("Lucía", "Take this notebook. It's yours. Everything you remember should be written down in it."));
        lines.Add(new DialogueLine("Lucía", "Your tasks are listed there, and after each one you can read what you did well and what to review. Press C to open it."));
        lines.Add(new DialogueLine("Lucía", "The campus can be explored now. Your other memories are waiting there."));
        return lines.ToArray();
    }
}
