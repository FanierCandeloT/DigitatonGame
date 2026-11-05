using UnityEngine;

/// <summary>Responsable del asset final que reemplazará al placeholder.</summary>
public enum PlaceholderOwner
{
    Integrante1,
    Integrante2,
    Integrante3
}

/// <summary>Tipos de placeholder. Cada tipo tiene un color fijo (ver PlaceholderPalette).</summary>
public enum PlaceholderKind
{
    Player,             // Blanco
    ClassroomBuilding,  // Azul
    AdminBuilding,      // Morado
    Health,             // Rojo
    Food,               // Naranja
    Green,              // Verde
    Walkway,            // Beige
    Street,             // Gris oscuro
    Wall,               // Negro
    Door,               // Amarillo
    NPC,                // Rosado
    Interactive         // Cian
}

/// <summary>Leyenda de colores FIJA para todos los placeholders del greybox.</summary>
public static class PlaceholderPalette
{
    public static Color GetColor(PlaceholderKind kind)
    {
        switch (kind)
        {
            case PlaceholderKind.Player:            return Color.white;
            case PlaceholderKind.ClassroomBuilding: return new Color(0.20f, 0.40f, 0.90f);
            case PlaceholderKind.AdminBuilding:     return new Color(0.55f, 0.30f, 0.80f);
            case PlaceholderKind.Health:            return new Color(0.90f, 0.15f, 0.15f);
            case PlaceholderKind.Food:              return new Color(1.00f, 0.55f, 0.10f);
            case PlaceholderKind.Green:             return new Color(0.25f, 0.70f, 0.30f);
            case PlaceholderKind.Walkway:           return new Color(0.90f, 0.82f, 0.65f);
            case PlaceholderKind.Street:            return new Color(0.25f, 0.25f, 0.27f);
            case PlaceholderKind.Wall:              return new Color(0.02f, 0.02f, 0.02f);
            case PlaceholderKind.Door:              return new Color(1.00f, 0.92f, 0.10f);
            case PlaceholderKind.NPC:               return new Color(1.00f, 0.55f, 0.80f);
            case PlaceholderKind.Interactive:       return new Color(0.10f, 0.90f, 0.95f);
            default:                                return Color.magenta;
        }
    }

    public static string GetDescription(PlaceholderKind kind)
    {
        switch (kind)
        {
            case PlaceholderKind.Player:            return "Blanco: jugador (Andrés)";
            case PlaceholderKind.ClassroomBuilding: return "Azul: edificios de aulas";
            case PlaceholderKind.AdminBuilding:     return "Morado: edificios administrativos y de servicios";
            case PlaceholderKind.Health:            return "Rojo: salud (Bienestar, enfermería)";
            case PlaceholderKind.Food:              return "Naranja: comida (cafetería)";
            case PlaceholderKind.Green:             return "Verde: zonas verdes y jardines";
            case PlaceholderKind.Walkway:           return "Beige: caminos peatonales";
            case PlaceholderKind.Street:            return "Gris oscuro: calles y parqueaderos";
            case PlaceholderKind.Wall:              return "Negro: muros y bordes que no se pueden atravesar";
            case PlaceholderKind.Door:              return "Amarillo: puertas y entradas a otras escenas";
            case PlaceholderKind.NPC:               return "Rosado: personajes (NPC)";
            case PlaceholderKind.Interactive:       return "Cian: objetos interactivos y retos";
            default:                                return "";
        }
    }
}
