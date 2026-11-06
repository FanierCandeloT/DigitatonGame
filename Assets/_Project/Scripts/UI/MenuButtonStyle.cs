using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Estados visuales de los botones del menú (y de los botones de Settings): al pasar el mouse o seleccionarlo con el
/// teclado el botón se agranda un poco, se ilumina y aparece una flecha a su izquierda; al presionarlo se oscurece un
/// instante. Todo con el tiempo real (funciona también con el juego en pausa) y con transiciones rápidas y suaves.
/// El mouse, al pasar por encima, SELECCIONA el botón: así solo hay uno resaltado a la vez (mouse y teclado coinciden).
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonStyle : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    public Image body;          // el sprite del botón (se oscurece al presionar)
    public Image glow;          // capa clara encima (se ilumina al resaltar)
    public RectTransform arrow; // flecha pequeña a la izquierda
    public CanvasGroup arrowGroup;

    [Range(1f, 1.3f)] public float hoverScale = 1.07f;
    public float speed = 14f;
    public float glowAlpha = 0.32f;
    public float arrowBob = 4f;

    bool highlighted, pressed;
    float pressTimer;
    float t;       // 0 = normal, 1 = resaltado
    float press;   // 0 = normal, 1 = oscurecido
    Vector2 arrowBase;
    Color bodyColor = Color.white;
    bool ready;

    void Awake()
    {
        GetComponent<Button>().transition = Selectable.Transition.None;   // los estados los dibuja este componente
        if (arrow != null) arrowBase = arrow.anchoredPosition;
        if (body != null) bodyColor = body.color;
        ready = true;
    }

    void OnEnable()
    {
        pressed = false; pressTimer = 0f; press = 0f;
        highlighted = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        t = highlighted ? 1f : 0f;
        if (ready) Apply();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!GetComponent<Button>().IsInteractable() || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(gameObject);   // el mouse y el teclado comparten el resaltado
    }

    public void OnSelect(BaseEventData e) => highlighted = true;
    public void OnDeselect(BaseEventData e) { highlighted = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; pressTimer = 0.12f; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSubmit(BaseEventData e) { pressTimer = 0.12f; }   // Enter también oscurece un instante

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t = Mathf.MoveTowards(t, highlighted ? 1f : 0f, dt * speed);
        if (pressTimer > 0f) pressTimer -= dt;
        press = Mathf.MoveTowards(press, (pressed || pressTimer > 0f) ? 1f : 0f, dt * speed * 1.5f);
        Apply();
    }

    void Apply()
    {
        float ease = t * t * (3f - 2f * t);   // smoothstep
        transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, ease);
        if (glow != null) { var c = glow.color; c.a = glowAlpha * ease * (1f - press); glow.color = c; }
        if (body != null) body.color = Color.Lerp(bodyColor, bodyColor * new Color(0.55f, 0.5f, 0.5f, 1f), press);
        if (arrowGroup != null) arrowGroup.alpha = ease;
        if (arrow != null)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 8f) * arrowBob * ease;
            arrow.anchoredPosition = arrowBase + new Vector2(bob - (1f - ease) * 10f, 0f);
        }
    }
}
