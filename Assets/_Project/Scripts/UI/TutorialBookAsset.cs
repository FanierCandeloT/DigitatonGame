using System;
using UnityEngine;

/// <summary>Una hoja doble del libro: título y texto de la página izquierda y de la derecha.</summary>
[Serializable]
public class BookPage
{
    [Tooltip("Título de la página izquierda (opcional, sale en grande y en negrita).")]
    public string leftTitle;
    [TextArea(3, 10)] public string leftBody;

    [Tooltip("Título de la página derecha (opcional).")]
    public string rightTitle;
    [TextArea(3, 10)] public string rightBody;
}

/// <summary>
/// Contenido del libro de la Profe Dalia (escena Tutorial). Se edita sin tocar código: cada elemento de "pages"
/// es una hoja doble (página izquierda | página derecha). El texto acepta etiquetas de TextMeshPro (&lt;b&gt;, &lt;i&gt;...).
/// También guarda los sprites del libro y de sus botones. Se crea con Assets ▸ Create ▸ DigitatonGame ▸ Tutorial Book.
/// </summary>
[CreateAssetMenu(fileName = "TutorialBook", menuName = "DigitatonGame/Tutorial Book")]
public class TutorialBookAsset : ScriptableObject
{
    public BookPage[] pages;

    [Header("Sprites (Art/SpriteDalia)")]
    public Sprite book;            // LibroTuto1: libro abierto
    public Sprite turnNext;        // LibroTutoDrch: pasar la página hacia la derecha
    public Sprite turnPrevious;    // LibroTutoIzq: pasar la página hacia la izquierda
    public Sprite nextButton;      // BotonTutoDrch
    public Sprite previousButton;  // BotonTutoIzq
    public Sprite closeButton;     // BotonCerrarTuto

    public int PageCount => pages == null ? 0 : pages.Length;
}
