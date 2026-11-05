using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC que inicia un reto con un diálogo previo: primero conversan, y al terminar se abre el minijuego
/// de reconstrucción de memoria. Al completarlo suma los recuerdos, no se repite y cierra con un diálogo
/// que repasa los errores cometidos (el juego es educativo: el error se explica, no solo se cuenta).
/// </summary>
public class NpcChallengeInteractable : Interactable
{
    /// <summary>Máximo de errores que se repasan en el diálogo final, para que no se haga largo.</summary>
    const int MaxReviewed = 2;

    [Tooltip("Id único del reto. 'q1_4' abre los edificios del campus.")]
    public string challengeId = "q1_4";

    [Min(0)] public int memoryReward = 4;

    public DialogueLine[] intro =
    {
        new DialogueLine("Andrés", "Everything is blank... Maybe talking to people will help me recover my memories, and who I am."),
        new DialogueLine("Lucía", "You were found near the campus yesterday. You were brought here by a student."),
        new DialogueLine("Lucía", "Your memories can be repaired. The pieces must be connected, one by one."),
        new DialogueLine("Andrés", "Okay... let's try."),
    };

    [TextArea] public string doneMessage = "Lucía: Take your time.";

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;
        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show(doneMessage, 2.5f);
            return;
        }

        DialoguePanel.Show(intro, () => MemoryReconstructionGame.Open(result => Complete(interactor, result)));
    }

    void Complete(PlayerInteractor interactor, MemoryResult result)
    {
        GameManager.Instance.CompleteChallenge(challengeId, memoryReward);
        base.Interact(interactor);
        DialoguePanel.Show(BuildClosing(result), () => ToastMessage.Show("Your memories have been restored.", 2.5f));
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

        lines.Add(new DialogueLine("Andrés", "I remember now... I'm a teacher. English was taught in my classroom."));
        lines.Add(new DialogueLine("Lucía", "The campus can be explored now. Your other memories are waiting there."));
        return lines.ToArray();
    }
}
