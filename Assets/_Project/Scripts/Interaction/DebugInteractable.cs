using UnityEngine;

/// <summary>Objeto interactivo de prueba: escribe un mensaje en la consola.</summary>
public class DebugInteractable : Interactable
{
    public string message = "Interacción de prueba ejecutada";

    public override void Interact(PlayerInteractor interactor)
    {
        Debug.Log(message, this);
        base.Interact(interactor);
    }
}
