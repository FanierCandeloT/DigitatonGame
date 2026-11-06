using UnityEngine;

/// <summary>
/// La Profe Dalia del Tutorial. Al presionar E dice su diálogo (DialoguePanel, con el sonido WriteText) y,
/// al terminarlo, se abre el libro. Se puede volver a hablar con ella las veces que se quiera.
/// Su collider debe estar solo en los pies (así Andrés puede pasar por detrás).
/// </summary>
public class ProfessorDalia : Interactable
{
    [Tooltip("Lo que dice al hablarle (Assets/_Project/Data/Dialogues).")]
    public DialogueAsset dialogue;

    [Tooltip("El libro que se abre al terminar el diálogo (Assets/_Project/Data).")]
    public TutorialBookAsset book;

    public override void Interact(PlayerInteractor interactor)
    {
        base.Interact(interactor);
        if (dialogue != null && dialogue.HasLines) dialogue.Show(OpenBook);
        else OpenBook();
    }

    void OpenBook()
    {
        if (book != null) TutorialBookPanel.Open(book);
    }
}
