using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Una fila de bloques de la frase: sujeto, verbo en pasiva, agente, etc.</summary>
public class BuilderSlot
{
    public readonly string Label;
    public readonly string[] Options;
    public readonly bool Optional;

    public BuilderSlot(string label, string[] options, bool optional = false)
    {
        Label = label;
        Options = options;
        Optional = optional;
    }
}

/// <summary>
/// Sistema de bloques de palabras reutilizable: el jugador arma una frase eligiendo un bloque por fila
/// (por ejemplo sujeto + verbo en pasiva + agente) y la envía. Es un panel inferior que deja ver el mundo.
/// Quien lo abre decide qué hacer con la frase (onSubmit): lo usan los retos donde la frase cambia el mundo,
/// la respuesta a Lucía y la confrontación. Con Hide()/Show() se puede ocultar mientras el mundo reacciona.
/// </summary>
public class SentenceBuilder : MonoBehaviour
{
    // ───────────── Estilo ─────────────

    static readonly Color[] RoleColors =
    {
        new Color(1f, 0.78f, 0.35f),   // sujeto
        new Color(0.4f, 0.85f, 1f),    // verbo
        new Color(0.95f, 0.55f, 0.85f),// agente / complemento
        new Color(0.55f, 0.95f, 0.65f),
    };

    static readonly Color Frame = new Color(0.55f, 0.65f, 1f);
    static readonly Color PanelFill = new Color(0.05f, 0.07f, 0.15f, 0.95f);
    static readonly Color ChipFill = new Color(0.08f, 0.1f, 0.19f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.85f);
    static readonly Color Empty = new Color(0.42f, 0.46f, 0.6f);

    const float ChipWidth = 204f, ChipHeight = 40f, ChipGap = 10f, RowStep = 46f;

    // ───────────── Estado ─────────────

    class Row
    {
        public BuilderSlot Slot;
        public int Selected = -1;
        public readonly List<Image> Outlines = new List<Image>();
        public readonly List<Image> Fills = new List<Image>();
    }

    readonly List<Row> rows = new List<Row>();
    Action<string[]> onSubmit;
    Action onCancel;
    CanvasGroup group;
    TextMeshProUGUI previewLabel, feedbackLabel;
    bool locked;
    float inputReadyAt;
    bool closed;
    GameManager.ModalLock modalLock;

    static SentenceBuilder current;

    /// <summary>Avisan cuando el panel se abre o se cierra (la cámara sube un poco para no tapar el mundo).</summary>
    public static event Action Opened;
    public static event Action Closed;

    /// <summary>True mientras haya un constructor abierto (ocupa el control del jugador).</summary>
    public static bool IsOpen => current != null;

    /// <summary>Abre el constructor. onSubmit recibe una cadena por fila ("" si una fila opcional quedó vacía).</summary>
    public static SentenceBuilder Open(string instruction, BuilderSlot[] slots, Action<string[]> onSubmit, Action onCancel = null)
    {
        var canvas = PixelUi.NewCanvas("SentenceBuilder", 450);
        var builder = canvas.gameObject.AddComponent<SentenceBuilder>();
        builder.onSubmit = onSubmit;
        builder.onCancel = onCancel;
        builder.Build(instruction, slots);
        builder.inputReadyAt = Time.unscaledTime + 0.35f;   // ignora la pulsación que cerró el diálogo anterior
        current = builder;
        builder.modalLock = GameManager.Instance.LockInput();   // Andrés queda quieto mientras el panel esté abierto, incluso oculto
        Opened?.Invoke();
        return builder;
    }

    void OnDestroy()
    {
        modalLock?.Release();   // idempotente: si se destruye por un cambio de escena, el jugador no queda bloqueado
        if (current == this)
        {
            current = null;
            Closed?.Invoke();
        }
    }

    // ───────────── Construcción ─────────────

    void Build(string instruction, BuilderSlot[] slots)
    {
        group = gameObject.AddComponent<CanvasGroup>();

        int n = slots.Length;
        float height = 158f + RowStep * n;   // más compacto: deja ver el mundo
        var panel = PixelUi.NewPanel("Caja", transform, Frame, PanelFill, out _);
        PixelUi.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1240f, height));

        float top = height * 0.5f;
        var instructionLabel = PixelUi.NewLabel("Indicacion", panel.transform, instruction, 22, new Vector2(0f, top - 22f), new Vector2(1160f, 28f), FontStyles.Italic);
        instructionLabel.color = Muted;

        previewLabel = PixelUi.NewLabel("Frase", panel.transform, "", 34, new Vector2(0f, top - 62f), new Vector2(1180f, 46f), FontStyles.Bold);

        for (int r = 0; r < n; r++)
        {
            var row = new Row { Slot = slots[r] };
            rows.Add(row);
            float y = top - 107f - r * RowStep;
            var role = RoleColors[r % RoleColors.Length];

            var caption = PixelUi.NewLabel("Fila", panel.transform, slots[r].Label.ToUpper(), 14, new Vector2(-566f, y), new Vector2(92f, ChipHeight), FontStyles.Bold);
            caption.color = role;
            caption.textWrappingMode = TextWrappingModes.Normal;

            for (int i = 0; i < slots[r].Options.Length; i++)
            {
                var outline = PixelUi.NewPanel("Bloque", panel.transform, role, ChipFill, out var fill);
                outline.rectTransform.anchoredPosition = new Vector2(-418f + i * (ChipWidth + ChipGap), y);
                outline.rectTransform.sizeDelta = new Vector2(ChipWidth, ChipHeight);
                PixelUi.NewLabel("Texto", outline.transform, slots[r].Options[i], 23, Vector2.zero, new Vector2(ChipWidth - 8f, ChipHeight), FontStyles.Bold);

                var button = outline.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                int rowIndex = r, optionIndex = i;
                button.onClick.AddListener(() => Pick(rowIndex, optionIndex));
                row.Outlines.Add(outline);
                row.Fills.Add(fill);
            }
            RefreshRow(r);
        }

        // Zona inferior: guía en el centro y los dos botones a los lados, en la misma línea
        float bottomY = top - 115f - RowStep * n;
        feedbackLabel = PixelUi.NewLabel("Guia", panel.transform, "", 21, new Vector2(0f, bottomY), new Vector2(760f, 54f), FontStyles.Normal);
        feedbackLabel.textWrappingMode = TextWrappingModes.Normal;

        MakeButton(panel.transform, "LEAVE", Muted, new Vector2(-500f, bottomY), Cancel);
        MakeButton(panel.transform, "SAY IT", new Color(0.4f, 0.95f, 0.62f), new Vector2(500f, bottomY), Submit);

        RefreshPreview();
    }

    void MakeButton(Transform parent, string text, Color color, Vector2 pos, Action onClick)
    {
        var button = PixelUi.NewPanel(text, parent, color, new Color(0.06f, 0.1f, 0.14f), out _);
        button.rectTransform.anchoredPosition = pos;
        button.rectTransform.sizeDelta = new Vector2(220f, 46f);
        var label = PixelUi.NewLabel("Texto", button.transform, text, 26, Vector2.zero, new Vector2(210f, 44f), FontStyles.Bold);
        label.color = color;
        var b = button.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
    }

    // ───────────── Interacción ─────────────

    void Pick(int row, int option)
    {
        if (locked) return;
        var r = rows[row];
        r.Selected = r.Selected == option ? -1 : option;   // volver a tocar el bloque lo quita
        RefreshRow(row);
        RefreshPreview();
    }

    void RefreshRow(int index)
    {
        var row = rows[index];
        var role = RoleColors[index % RoleColors.Length];
        for (int i = 0; i < row.Outlines.Count; i++)
        {
            bool on = row.Selected == i;
            row.Outlines[i].color = on ? Color.white : role;
            row.Fills[i].color = on ? Color.Lerp(ChipFill, role, 0.5f) : ChipFill;
        }
    }

    void RefreshPreview()
    {
        var parts = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            var role = RoleColors[i % RoleColors.Length];
            string hex = ColorUtility.ToHtmlStringRGB(role);
            var row = rows[i];
            if (row.Selected >= 0) parts.Add($"<color=#{hex}>{row.Slot.Options[row.Selected]}</color>");
            else if (!row.Slot.Optional) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Empty)}>_____</color>");
        }
        previewLabel.text = string.Join(" ", parts);
    }

    void Submit()
    {
        if (locked || closed) return;

        var values = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Selected < 0 && !row.Slot.Optional)
            {
                SetFeedback("Choose one block in every row first.", Muted);
                return;
            }
            values[i] = row.Selected >= 0 ? row.Slot.Options[row.Selected] : "";
        }
        onSubmit?.Invoke(values);
    }

    void Cancel()
    {
        if (closed) return;
        var callback = onCancel;
        Close();
        callback?.Invoke();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || Time.unscaledTime < inputReadyAt || locked) return;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Submit();
        else if (kb.escapeKey.wasPressedThisFrame) Cancel();
    }

    // ───────────── Control desde fuera ─────────────

    public void SetFeedback(string text, Color color)
    {
        feedbackLabel.text = text;
        feedbackLabel.color = color;
    }

    /// <summary>Oculta el panel mientras el mundo reacciona (no se puede enviar nada).</summary>
    public void Hide()
    {
        locked = true;
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    /// <summary>Vuelve a mostrar el panel, dejando las elecciones anteriores para poder ajustarlas.</summary>
    public void Show()
    {
        locked = false;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        inputReadyAt = Time.unscaledTime + 0.2f;
    }

    public void Close()
    {
        if (closed) return;
        closed = true;
        modalLock?.Release();
        Destroy(gameObject);
    }
}
