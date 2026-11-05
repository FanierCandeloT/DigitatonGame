using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Una neurona del minijuego: guarda un fragmento de frase y recibe los gestos de arrastre.
/// Usa los eventos de UI, así que funciona igual con mouse y con pantalla táctil.
/// </summary>
public class MemoryNeuron : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public const float Width = 250f;
    public const float Height = 66f;

    public string Text { get; private set; }
    public int Column { get; private set; }
    public bool IsPlural { get; private set; }
    public bool Locked { get; set; }

    public RectTransform Rect { get; private set; }

    /// <summary>Relleno interior (cambia al recuperar la frase).</summary>
    public Image Body { get; private set; }

    /// <summary>Borde de color según el rol: sujeto, verbo o complemento.</summary>
    public Image Outline { get; private set; }

    public Color RoleColor { get; private set; }
    public TextMeshProUGUI Label { get; private set; }

    MemoryReconstructionGame game;

    public void Setup(MemoryReconstructionGame owner, string text, int column, bool isPlural,
        Image outline, Image body, TextMeshProUGUI label, Color roleColor)
    {
        game = owner;
        Text = text;
        Column = column;
        IsPlural = isPlural;
        Rect = (RectTransform)transform;
        Outline = outline;
        Body = body;
        Label = label;
        RoleColor = roleColor;
    }

    /// <summary>Punto del borde desde donde sale el cable, hacia la derecha o la izquierda.</summary>
    public Vector2 Port(bool right) => Rect.anchoredPosition + new Vector2(right ? Width * 0.5f : -Width * 0.5f, 0f);

    public void OnBeginDrag(PointerEventData eventData) => game.BeginWire(this, eventData);
    public void OnDrag(PointerEventData eventData) => game.DragWire(eventData);
    public void OnEndDrag(PointerEventData eventData) => game.EndWire();
    public void OnDrop(PointerEventData eventData) => game.DropWire(this);
}
