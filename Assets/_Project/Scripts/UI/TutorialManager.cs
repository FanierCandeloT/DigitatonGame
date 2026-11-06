using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Guía paso a paso de la escena Tutorial: moverse, interactuar (E), Cuaderno del profe (C) y pausa (Esc).
/// Al final, la puerta amarilla vuelve al menú principal. El texto respeta la velocidad elegida en Settings.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public TMP_Text messageText;
    public Interactable practiceObject;

    Transform player;
    Vector3 lastPos;
    float distanceMoved;
    [Tooltip("Puerta de salida al menú: solo se abre después de leer el libro de la Profe Dalia.")]
    public Door exitDoor;

    bool interacted, notebookOpened, notebookClosed, pausedOnce, bookRead;
    string bookFlag;

    // ── Los mensajes de guía salen como una notificación arriba a la derecha ──
    const float Margin = 24f;          // distancia a la orilla de la pantalla (unidades de canvas)
    const float PadX = 14f, PadY = 9f; // relleno del panel
    const float MaxTextWidth = 340f;   // ancho máximo del texto; si no cabe, baja de renglón
    const float EnterSeconds = 0.3f;
    const float NoticeFontSize = 20f;

    RectTransform panel;
    CanvasGroup group;
    float enterT = 1f;
    float hideAlpha = 1f;
    bool hasMessage;

    void SetupPanel()
    {
        panel = messageText.transform.parent as RectTransform;
        if (panel == null) return;
        group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var image = panel.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.color = new Color(0.04f, 0.06f, 0.14f, 0.78f);   // semitransparente
            image.raycastTarget = false;
        }

        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);   // esquina superior derecha
        panel.anchoredPosition = new Vector2(-Margin, -Margin);

        var t = messageText.rectTransform;
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = new Vector2(PadX, PadY);
        t.offsetMax = new Vector2(-PadX, -PadY);
        messageText.fontSize = NoticeFontSize;
        messageText.enableAutoSizing = false;
        messageText.alignment = TextAlignmentOptions.TopLeft;
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.raycastTarget = false;
    }

    /// <summary>Ajusta el panel al tamaño del mensaje COMPLETO (no cambia de tamaño mientras se escribe) y lo hace entrar.</summary>
    void ShowNotice(string message)
    {
        if (panel == null) return;
        var size = messageText.GetPreferredValues(message, MaxTextWidth, 0f);
        panel.sizeDelta = new Vector2(Mathf.Ceil(Mathf.Min(size.x, MaxTextWidth)) + 2f * PadX, Mathf.Ceil(size.y) + 2f * PadY);
        enterT = 0f;
        hasMessage = true;
    }

    void UpdateNotice()
    {
        if (panel == null) return;

        // Entrada: se desliza desde la derecha con un fundido corto
        enterT = Mathf.Min(1f, enterT + Time.unscaledDeltaTime / EnterSeconds);
        float ease = 1f - Mathf.Pow(1f - enterT, 3f);
        float hiddenX = panel.sizeDelta.x + Margin + 40f;
        panel.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, -Margin, ease), -Margin);

        // El libro de la Profe Dalia tiene prioridad: la notificación se esconde mientras está abierto y vuelve al cerrarlo
        float target = TutorialBookPanel.IsOpen ? 0f : 1f;
        hideAlpha = Mathf.MoveTowards(hideAlpha, target, Time.unscaledDeltaTime * 8f);
        group.alpha = hasMessage ? Mathf.Min(ease, hideAlpha) : 0f;
    }

    void OnEnable()
    {
        NotebookPanel.Opened += OnNotebookOpened;
        NotebookPanel.Closed += OnNotebookClosed;
        GameManager.PauseChanged += OnPauseChanged;
        TutorialBookPanel.Closed += OnBookClosed;
    }

    void OnDisable()
    {
        NotebookPanel.Opened -= OnNotebookOpened;
        NotebookPanel.Closed -= OnNotebookClosed;
        GameManager.PauseChanged -= OnPauseChanged;
        TutorialBookPanel.Closed -= OnBookClosed;
    }

    void Start()
    {
        SetupPanel();
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { player = pc.transform; lastPos = player.position; }
        if (practiceObject != null) practiceObject.onInteract.AddListener(() => interacted = true);

        // La salida pide haber leído el libro en ESTA visita al tutorial: la marca lleva un código único para que
        // una visita anterior (las marcas viven mientras el juego está abierto) no deje la puerta abierta de entrada.
        if (exitDoor != null)
        {
            bookFlag = exitDoor.requiredFlag + "_" + Guid.NewGuid().ToString("N");
            exitDoor.requiredFlag = bookFlag;
            exitDoor.devBypass = false;
        }
        StartCoroutine(Run());
    }

    void OnBookClosed()
    {
        bookRead = true;
        if (bookFlag != null) GameManager.Instance.SetFlag(bookFlag);
    }

    void Update()
    {
        UpdateNotice();
        if (player == null) return;
        distanceMoved += Vector3.Distance(player.position, lastPos);
        lastPos = player.position;
    }

    void OnNotebookOpened() => notebookOpened = true;
    void OnNotebookClosed() { if (notebookOpened) notebookClosed = true; }
    void OnPauseChanged(bool paused) { if (paused) pausedOnce = true; }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(0.8f);   // deja terminar el fundido de entrada

        distanceMoved = 0f;
        yield return Step("Welcome! Move around with W A S D or the arrow keys.", () => distanceMoved > 3f);

        interacted = false;
        yield return Step("Walk up to the cyan object. When \"Press E\" appears, press E to interact.", () => interacted);

        notebookOpened = notebookClosed = false;
        yield return Step("Press C to open the Professor's Notebook.", () => notebookOpened);
        yield return Step("Press C again (or Esc) to close the notebook.", () => notebookClosed);

        pausedOnce = false;
        yield return Step("Press Esc to pause the game, then choose Resume.", () => pausedOnce && !GameManager.Instance.IsPaused);

        yield return Step("Talk to Professor Dalia: walk up to her and press E. Read the book she gives you.", () => bookRead);

        yield return Type("Great job! Go through the yellow door to return to the main menu.");
    }

    IEnumerator Step(string message, Func<bool> isDone)
    {
        yield return Type(message);
        while (!isDone()) yield return null;
        yield return new WaitForSecondsRealtime(0.7f);
    }

    IEnumerator Type(string message)
    {
        messageText.text = message;
        ShowNotice(message);
        messageText.ForceMeshUpdate();
        int total = messageText.textInfo.characterCount;
        messageText.maxVisibleCharacters = 0;

        float shown = 0f;
        while (shown < total)
        {
            shown += SettingsData.CharsPerSecond * Time.unscaledDeltaTime;
            messageText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }
        messageText.maxVisibleCharacters = total;
    }
}
