using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimiento de Andrés en 8 direcciones (WASD y flechas) a velocidad constante.
/// Usa el nuevo Input System y Rigidbody2D, así que choca con los muros.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Min(0f)] public float speed = 5f;

    Rigidbody2D rb;
    InputAction moveAction;
    Vector2 moveInput;

    /// <summary>
    /// Entrada simulada, solo para pruebas automáticas: se suma al teclado y pasa por el mismo bloqueo
    /// (InputLocked), así se puede comprobar que un reto realmente deja quieto a Andrés.
    /// </summary>
    [System.NonSerialized] public Vector2 SimulatedInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector(mode=2)")      // mode 2 = DigitalNormalized: diagonales a la misma velocidad
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector(mode=2)")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        PlaceAtSpawn();
    }

    void OnEnable() => moveAction.Enable();
    void OnDisable() => moveAction.Disable();
    void OnDestroy() => moveAction?.Dispose();

    void PlaceAtSpawn()
    {
        var spawn = SpawnPoint.Resolve(SceneTransition.ConsumePendingSpawnId());
        if (spawn == null) return;
        transform.position = spawn.transform.position;
        rb.position = spawn.transform.position;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>() + SimulatedInput;
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        // Pausa, cuaderno abierto o transición de escena: Andrés no se mueve.
        if (GameManager.Instance.InputLocked) moveInput = Vector2.zero;

        // Pasos: solo suenan si Andrés realmente se está moviendo (no en pausa, fundidos, cuaderno, diálogos ni contra un muro).
        if (moveInput != Vector2.zero && rb.linearVelocity.sqrMagnitude > 0.25f) AudioManager.NotifyWalking();
    }

    void FixedUpdate()
    {
        // Estándar de los retos: con un diálogo o reto abierto Andrés queda completamente quieto: velocidad cero y posición
        // congelada, para que ni siquiera un NPC que se mueva durante el reto (o una colisión) pueda empujarlo.
        bool locked = GameManager.Instance.InputLocked;
        rb.constraints = locked ? RigidbodyConstraints2D.FreezeAll : RigidbodyConstraints2D.FreezeRotation;
        rb.linearVelocity = locked ? Vector2.zero : moveInput * speed;
    }
}
