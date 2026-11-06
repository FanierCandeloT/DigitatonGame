using UnityEngine;

/// <summary>
/// Anima a Andrés según su movimiento (lee la velocidad del Rigidbody2D) y según
/// estados especiales (hablar / pensar) que otros sistemas pueden pedir con SetEmote.
/// Los estados del Animator se llaman igual que los valores de <see cref="State"/>.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class AndresAnimator : MonoBehaviour
{
    public enum Emote { None, Talk, Think }
    enum Facing { Down, Up, Left, Right }

    const float MoveThreshold = 0.05f;

    Animator animator;
    Rigidbody2D rb;
    Facing facing = Facing.Down;
    Emote emote = Emote.None;
    int currentHash;

    /// <summary>Activa la animación de hablar/pensar. Usa Emote.None para volver a lo normal.</summary>
    public void SetEmote(Emote value) => emote = value;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        Vector2 velocity = rb.linearVelocity;
        bool moving = velocity.sqrMagnitude > MoveThreshold * MoveThreshold;

        if (moving)
            facing = FacingFrom(velocity);

        // Hablar / pensar solo cuando Andrés está quieto.
        int hash;
        if (!moving && emote == Emote.Talk) hash = TalkHash;
        else if (!moving && emote == Emote.Think) hash = ThinkHash;
        else hash = (moving ? WalkHashes : IdleHashes)[(int)facing];   // sin crear strings por frame (basura en WebGL)

        Play(hash);
    }

    static readonly int TalkHash = Animator.StringToHash("Talk");
    static readonly int ThinkHash = Animator.StringToHash("Think");
    static readonly int[] WalkHashes = BuildHashes("Walk_");
    static readonly int[] IdleHashes = BuildHashes("Idle_");

    static int[] BuildHashes(string prefix)
    {
        var hashes = new int[4];
        for (int i = 0; i < hashes.Length; i++) hashes[i] = Animator.StringToHash(prefix + (Facing)i);
        return hashes;
    }

    static Facing FacingFrom(Vector2 v)
    {
        // En diagonal gana el eje vertical, que tiene más cuadros de animación.
        if (Mathf.Abs(v.y) >= Mathf.Abs(v.x))
            return v.y > 0f ? Facing.Up : Facing.Down;
        return v.x > 0f ? Facing.Right : Facing.Left;
    }

    void Play(int hash)
    {
        if (hash == currentHash) return;
        currentHash = hash;
        animator.Play(hash);
    }
}
