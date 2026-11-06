using System;
using UnityEngine;

/// <summary>Una línea de los créditos: el texto y, opcionalmente, un detalle en tono más tenue (por ejemplo el código estudiantil).</summary>
[Serializable]
public class CreditEntry
{
    public string text;
    [Tooltip("Se muestra después del texto, separado por un guion y en un crema más tenue (vacío = sin detalle).")]
    public string detail;
    [Tooltip("Línea destacada (más grande), por ejemplo el nombre del juego.")]
    public bool big;
}

/// <summary>Un bloque de créditos: un título de sección (en naranja) y sus líneas. Sin título = bloque de introducción.</summary>
[Serializable]
public class CreditBlock
{
    [Tooltip("Título de la sección, en naranja (por ejemplo DEVELOPMENT TEAM). Vacío = sin título.")]
    public string heading;
    public CreditEntry[] entries;
}

/// <summary>
/// Contenido de la pantalla de créditos del menú principal. Se edita sin tocar código: cambia los textos aquí y se ven
/// la próxima vez que se abra el panel. Se crea con Assets ▸ Create ▸ DigitatonGame ▸ Credits.
/// </summary>
[CreateAssetMenu(fileName = "Credits", menuName = "DigitatonGame/Credits")]
public class CreditsAsset : ScriptableObject
{
    public string title = "Credits";
    public CreditBlock[] blocks;
}
