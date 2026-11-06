using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detecta objetos Interactable cerca de Andrés, muestra "Press E" y ejecuta la acción al presionar E.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Min(0.1f)] public float interactRadius = 1.6f;
    [Tooltip("Texto TextMeshPro que muestra el aviso (hijo del jugador).")]
    public TextMeshPro promptLabel;

    InputAction interactAction;
    Interactable current;
    Interactable soundGivenFor;   // objeto cerca del cual ya sonó el aviso (se limpia al alejarse)
    readonly Collider2D[] hits = new Collider2D[16];

    void Awake()
    {
        interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        SetPrompt(null);
    }

    void OnEnable() => interactAction.Enable();
    void OnDisable() => interactAction.Disable();
    void OnDestroy() => interactAction?.Dispose();

    void Update()
    {
        // En pausa, con el cuaderno abierto o durante un fundido no se interactúa.
        if (GameManager.Instance.InputLocked)
        {
            if (current != null) { current = null; SetPrompt(null); }
            return;
        }

        var nearest = FindNearest();
        if (nearest != current)
        {
            current = nearest;
            SetPrompt(current);

            // El sonido suena una sola vez al aparecer "Press E". Mientras siga cerca no se repite (tampoco al cerrar un
            // diálogo); vuelve a sonar solo si se aleja y se acerca otra vez, o si pasa a otro objeto.
            if (current != null && current != soundGivenFor) AudioManager.PlayInteraction();
        }
        soundGivenFor = nearest;

        if (current != null && interactAction.WasPressedThisFrame())
            current.Interact(this);
    }

    Interactable FindNearest()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, interactRadius, hits);
        Interactable best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!hits[i].TryGetComponent(out Interactable it) || !it.isActiveAndEnabled) continue;
            float d = ((Vector2)it.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }

    void SetPrompt(Interactable target)
    {
        if (promptLabel == null) return;
        bool show = target != null;
        promptLabel.gameObject.SetActive(show);
        if (show) promptLabel.text = target.prompt;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
