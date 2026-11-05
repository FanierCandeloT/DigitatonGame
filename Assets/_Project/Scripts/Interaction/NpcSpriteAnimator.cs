using UnityEngine;

/// <summary>
/// Animación por cuadros para un NPC quieto (p. ej. Lucía): ciclo de idle y, mientras hay un diálogo abierto
/// con el jugador cerca, ciclo de "hablar". No usa Animator: solo cambia el sprite del SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NpcSpriteAnimator : MonoBehaviour
{
    public Sprite[] idle;
    public Sprite[] talk;

    [Min(1f)] public float idleFps = 4f;
    [Min(1f)] public float talkFps = 6f;

    [Tooltip("Distancia máxima del jugador para considerar que el diálogo abierto es con este NPC.")]
    [Min(0f)] public float talkRadius = 4f;

    SpriteRenderer sr;
    Transform player;
    float clock;
    bool wasTalking;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        bool talking = IsTalking();
        if (talking != wasTalking)
        {
            clock = 0f;   // cada ciclo arranca en su primer cuadro
            wasTalking = talking;
        }

        var frames = talking && talk != null && talk.Length > 0 ? talk : idle;
        if (frames == null || frames.Length == 0) return;

        clock += Time.deltaTime;
        float fps = talking ? talkFps : idleFps;
        sr.sprite = frames[Mathf.FloorToInt(clock * fps) % frames.Length];
    }

    bool IsTalking()
    {
        if (!GameManager.Exists || !GameManager.Instance.DialogueOpen) return false;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return false;
            player = go.transform;
        }
        return Vector2.Distance(player.position, transform.position) <= talkRadius;
    }
}
