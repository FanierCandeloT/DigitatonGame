using UnityEngine;

/// <summary>
/// Atril del profesor: Andrés da la lección "Atrapa el error". Primero un breve diálogo con los estudiantes y
/// luego el minijuego. Hay que alcanzar un puntaje mínimo: si no se aprueba, el juego da una guía y se puede
/// volver a intentar. Al aprobar queda la marca "done:{challengeId}" y el puntaje se guarda en el GameManager.
/// </summary>
public class ClassLessonInteractable : Interactable
{
    [Tooltip("Id único de la lección (marca 'done:class1' al aprobar).")]
    public string challengeId = "class1";

    [Tooltip("Fracción del puntaje máximo que hay que alcanzar para aprobar.")]
    [Range(0.4f, 1f)] public float passFraction = 0.7f;

    public DialogueLine[] intro =
    {
        new DialogueLine("Andrés", "Good morning, class! Today we are practicing the passive voice."),
        new DialogueLine("Valentina", "Professor, the sentences will be read by us, right?"),
        new DialogueLine("Andrés", "Exactly! Listen carefully. If a sentence has a mistake, you must stop it."),
    };

    public DialogueLine[] passed =
    {
        new DialogueLine("Valentina", "Great class, professor! The mistakes were found by everyone."),
        new DialogueLine("Andrés", "Well done, everybody. See you tomorrow."),
    };

    [TextArea] public string doneMessage = "The lesson is over. Good work, professor!";

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;
        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show(doneMessage, 2.5f);
            return;
        }

        DialoguePanel.Show(intro, () => ClassErrorHunt.Open(result => OnFinished(interactor, result), passFraction));
    }

    void OnFinished(PlayerInteractor interactor, HuntResult result)
    {
        if (!result.Passed)
        {
            ToastMessage.Show("Take a short break, professor. The lesson can be tried again.", 3f);
            return;
        }

        var gm = GameManager.Instance;
        gm.CompleteChallenge(challengeId, 0);
        gm.SetStat(challengeId + ":score", result.Score);
        gm.SetStat(challengeId + ":max", result.MaxScore);
        gm.SetStat(challengeId + ":attempts", result.Attempts);
        base.Interact(interactor);
        DialoguePanel.Show(passed, null);
    }
}
