using UnityEngine;

/// <summary>
/// Marcador provisional de un reto que todavía no se construyó (objeto cian): al interactuar muestra "(pending)",
/// suma recuerdos y no se puede repetir. Los retos reales ya tienen su propio componente (NpcChallengeInteractable,
/// LitterChallenge, ClassLessonInteractable); este queda para los que faltan (ver StoryFlow).
/// </summary>
public class ChallengeInteractable : Interactable
{
    [Tooltip("Id único del reto (por ejemplo q5, q6, q9_11).")]
    public string challengeId;

    [Tooltip("Nombre que se muestra en pantalla, sin el '(pending)'.")]
    public string displayName = "Challenge";

    [Min(0)] public int memoryReward = 1;

    [Tooltip("Recuerdos mínimos para poder activarlo (la pregunta 12 pide 11).")]
    [Min(0)] public int requiredMemories;

    public string lockedMessage = "Something is still missing...";

    [Tooltip("Si está activo, al completarlo aparece la pantalla de resultados.")]
    public bool endsGame;

    static readonly Color DoneColor = new Color(0.35f, 0.5f, 0.5f);

    void Start()
    {
        if (GameManager.Instance.IsChallengeDone(challengeId)) MarkDone();
    }

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;

        if (gm.IsChallengeDone(challengeId))
        {
            ToastMessage.Show("Already completed.");
            return;
        }
        if (gm.MemoriesRecovered < requiredMemories)
        {
            ToastMessage.Show(lockedMessage);
            return;
        }

        Complete(interactor, $"{displayName} (pending)");
    }

    void Complete(PlayerInteractor interactor, string message)
    {
        var gm = GameManager.Instance;
        gm.CompleteChallenge(challengeId, memoryReward);
        ToastMessage.Show(message, 2.5f);
        MarkDone();
        base.Interact(interactor);

        if (endsGame) gm.ShowResults();
    }

    void MarkDone()
    {
        if (TryGetComponent(out SpriteRenderer sr)) sr.color = DoneColor;
        enabled = false;   // un componente desactivado ya no cuenta como interactuable
    }
}
