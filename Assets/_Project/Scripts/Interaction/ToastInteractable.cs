using UnityEngine;

/// <summary>
/// Objeto o NPC con el que se habla con E.
///  - Si tiene un diálogo asignado ("Dialogue"), se abre la caja de diálogo (máquina de escribir, varias líneas, varios personajes).
///  - Si no, muestra el mensaje corto en pantalla.
/// </summary>
public class ToastInteractable : Interactable
{
    [TextArea] public string message = "...";

    [Tooltip("Diálogo del NPC (asset de Assets/_Project/Data/Dialogues). Vacío = solo se muestra el mensaje corto.")]
    public DialogueAsset dialogue;

    public override void Interact(PlayerInteractor interactor)
    {
        if (dialogue != null && dialogue.HasLines)
        {
            dialogue.Show();
        }
        else
        {
            ToastMessage.Show(message, 2.5f);
        }
        base.Interact(interactor);
    }
}
