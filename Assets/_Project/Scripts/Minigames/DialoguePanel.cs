using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Una línea de diálogo: quién habla y qué dice.</summary>
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea] public string text;

    public DialogueLine() { }

    public DialogueLine(string speaker, string text)
    {
        this.speaker = speaker;
        this.text = text;
    }
}

/// <summary>
/// Caja de diálogo inferior con máquina de escribir. Avanza con E, Espacio, Enter, clic o toque.
/// Se construye por código; el retrato es un placeholder de color con la inicial del personaje.
/// </summary>
public class DialoguePanel : MonoBehaviour
{
    const float CharsPerSecond = 55f;

    static readonly Color Frame = new Color(0.55f, 0.65f, 1f);
    static readonly Color Fill = new Color(0.05f, 0.07f, 0.15f, 0.97f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.85f);

    DialogueLine[] lines;
    int index;
    Action onDone;

    CanvasGroup group;
    Image portraitFrame, portraitFill;
    TextMeshProUGUI nameLabel, bodyLabel, initialLabel, hint;
    float shown;
    bool lineComplete;
    int startFrame;

    static DialoguePanel current;

    /// <summary>Muestra las líneas en orden y llama a onDone al terminar.</summary>
    public static DialoguePanel Show(DialogueLine[] lines, Action onDone)
    {
        if (lines == null || lines.Length == 0)
        {
            onDone?.Invoke();
            return null;
        }

        var canvas = PixelUi.NewCanvas("DialoguePanel", 400);
        var panel = canvas.gameObject.AddComponent<DialoguePanel>();
        panel.lines = lines;
        panel.onDone = onDone;
        panel.startFrame = Time.frameCount;   // ignora la misma pulsación de E que abrió el diálogo
        panel.Build();
        panel.ShowLine(0);

        current = panel;
        GameManager.Instance.DialogueOpen = true;
        return panel;
    }

    void OnDestroy()
    {
        // Si el panel desaparece por un cambio de escena, el jugador no puede quedar bloqueado.
        if (current == this)
        {
            current = null;
            if (GameManager.Exists) GameManager.Instance.DialogueOpen = false;
        }
    }

    void Build()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var dim = PixelUi.NewImage("Oscurecer", transform, new Color(0f, 0f, 0.03f, 0.4f));
        PixelUi.Stretch(dim.rectTransform);

        var box = PixelUi.NewPanel("Caja", transform, Frame, Fill, out _);
        PixelUi.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1120f, 240f));
        box.raycastTarget = false;

        portraitFrame = PixelUi.NewPanel("Retrato", box.transform, Color.white, Color.black, out portraitFill);
        PixelUi.Place(portraitFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(140f, 140f));
        portraitFrame.raycastTarget = false;
        initialLabel = PixelUi.NewLabel("Inicial", portraitFill.transform, "", 80, Vector2.zero, new Vector2(140f, 140f), FontStyles.Bold);

        nameLabel = PixelUi.NewLabel("Nombre", box.transform, "", 30, Vector2.zero, new Vector2(600f, 36f), FontStyles.Bold, TextAlignmentOptions.Left);
        PixelUi.Place(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -22f), new Vector2(600f, 36f));

        bodyLabel = PixelUi.NewLabel("Texto", box.transform, "", 28, Vector2.zero, new Vector2(880f, 140f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;
        PixelUi.Place(bodyLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -66f), new Vector2(880f, 140f));

        hint = PixelUi.NewLabel("Aviso", box.transform, "E / click to continue", 20, Vector2.zero, new Vector2(400f, 30f), FontStyles.Italic, TextAlignmentOptions.Right);
        hint.color = Muted;
        PixelUi.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 14f), new Vector2(400f, 30f));
    }

    void ShowLine(int i)
    {
        index = i;
        var line = lines[i];
        var color = SpeakerColor(line.speaker);

        nameLabel.text = line.speaker;
        nameLabel.color = color;
        bodyLabel.text = line.text;
        bodyLabel.maxVisibleCharacters = 0;
        portraitFrame.color = color;
        portraitFill.color = color * 0.35f;
        initialLabel.text = string.IsNullOrEmpty(line.speaker) ? "" : line.speaker.Substring(0, 1);
        initialLabel.color = color;
        shown = 0f;
        lineComplete = false;
        hint.gameObject.SetActive(false);
    }

    static Color SpeakerColor(string speaker)
    {
        if (speaker != null && speaker.StartsWith("Luc")) return new Color(1f, 0.55f, 0.62f);   // Lucía (salud)
        return new Color(0.75f, 0.9f, 1f);                                                       // Andrés
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (group.alpha < 1f) group.alpha = Mathf.Min(1f, group.alpha + dt * 5f);

        if (!lineComplete)
        {
            shown += CharsPerSecond * dt;
            int visible = Mathf.FloorToInt(shown);
            bodyLabel.maxVisibleCharacters = visible;
            if (visible >= lines[index].text.Length) CompleteLine();
        }

        if (Time.frameCount <= startFrame || !AdvancePressed()) return;

        if (!lineComplete) CompleteLine();
        else if (index + 1 < lines.Length) ShowLine(index + 1);
        else Close();
    }

    void CompleteLine()
    {
        lineComplete = true;
        bodyLabel.maxVisibleCharacters = int.MaxValue;
        hint.gameObject.SetActive(true);
    }

    static bool AdvancePressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
        var pointer = Pointer.current;   // mouse o pantalla táctil
        return pointer != null && pointer.press.wasPressedThisFrame;
    }

    void Close()
    {
        var callback = onDone;
        GameManager.Instance.DialogueOpen = false;
        Destroy(gameObject);
        callback?.Invoke();
    }
}
