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
    bool interacted, notebookOpened, notebookClosed, pausedOnce;

    void OnEnable()
    {
        NotebookPanel.Opened += OnNotebookOpened;
        NotebookPanel.Closed += OnNotebookClosed;
        GameManager.PauseChanged += OnPauseChanged;
    }

    void OnDisable()
    {
        NotebookPanel.Opened -= OnNotebookOpened;
        NotebookPanel.Closed -= OnNotebookClosed;
        GameManager.PauseChanged -= OnPauseChanged;
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { player = pc.transform; lastPos = player.position; }
        if (practiceObject != null) practiceObject.onInteract.AddListener(() => interacted = true);
        StartCoroutine(Run());
    }

    void Update()
    {
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
