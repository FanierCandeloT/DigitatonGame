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
        moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        // Pausa, cuaderno abierto o transición de escena: Andrés no se mueve.
        if (GameManager.Instance.InputLocked) moveInput = Vector2.zero;
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveInput * speed;
    }
}
