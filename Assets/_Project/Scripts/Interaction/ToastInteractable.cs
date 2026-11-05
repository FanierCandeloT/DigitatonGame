using UnityEngine;

/// <summary>Objeto interactivo que muestra un mensaje en pantalla (por ejemplo, un NPC).</summary>
public class ToastInteractable : Interactable
{
    [TextArea] public string message = "...";

    public override void Interact(PlayerInteractor interactor)
    {
        ToastMessage.Show(message, 2.5f);
        base.Interact(interactor);
    }
}
