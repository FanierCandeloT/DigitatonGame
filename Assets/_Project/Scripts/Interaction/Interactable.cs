using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cualquier objeto con el que Andrés pueda interactuar con E.
/// Necesita un Collider2D (normalmente trigger). Hereda de esta clase para acciones propias.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Tooltip("Texto que aparece cuando Andrés está cerca.")]
    public string prompt = "Press E";

    [Tooltip("Acción que se ejecuta al presionar E.")]
    public UnityEvent onInteract;

    public virtual void Interact(PlayerInteractor interactor)
    {
        onInteract?.Invoke();
    }
}
