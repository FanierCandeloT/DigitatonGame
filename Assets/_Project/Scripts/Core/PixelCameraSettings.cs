using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Única fuente de verdad del formato de pantalla de pixel art. Si el zoom se siente muy cerca o muy lejos,
/// se cambia SOLO aquí: lo usan el prefab CameraRig (al iniciar cada escena), los constructores del greybox
/// y las cámaras de los menús. Más resolución de referencia = se ve más mapa (menos zoom).
/// </summary>
public static class PixelCameraSettings
{
    public const int Ppu = 48;           // 1 casilla = 48x48 px = 1 unidad de mundo
    // Con 640x360 (13,3 x 7,5 casillas) Andrés ocupaba casi un cuarto de la altura de la pantalla: demasiado zoom,
    // sobre todo desde que el mapa se redujo y los objetos se ajustaron a su tamaño. 960x540 muestra 20 x 11,25 casillas
    // (a 1080p cada pixel se ve de 2x2) y Andrés queda en ~20 % de la altura.
    public const int ViewWidth = 960;    // 20 casillas de ancho
    public const int ViewHeight = 540;   // 11,25 casillas de alto (16:9)

    /// <summary>Mitad de la altura visible en unidades de mundo.</summary>
    public const float OrthoSize = ViewHeight / 2f / Ppu;

    /// <summary>
    /// Cuánto mundo se ve (en unidades) con una pantalla de ese tamaño. El Pixel Perfect Camera usa un zoom ENTERO
    /// (floor de pantalla / resolución de referencia, mínimo 1), así que en ventanas más grandes o más chicas que la
    /// referencia se ve distinto: 1080p = 20 x 11,25 · 1440p = 26,7 x 15 · 1366x768 (zoom 1) = 28,5 x 16.
    /// </summary>
    public static Vector2 VisibleSize(int screenWidth, int screenHeight)
    {
        int zoom = Mathf.Max(1, Mathf.Min(screenWidth / ViewWidth, screenHeight / ViewHeight));
        return new Vector2(screenWidth / (float)(zoom * Ppu), screenHeight / (float)(zoom * Ppu));
    }

    public static void Apply(Camera cam, PixelPerfectCamera ppc)
    {
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;   // fuera del mapa se ve negro
        }

        if (ppc == null) return;
        ppc.assetsPPU = Ppu;
        ppc.refResolutionX = ViewWidth;
        ppc.refResolutionY = ViewHeight;
        // PixelSnapping mantiene nítidos los rótulos de texto del greybox; con arte final se puede probar UpscaleRenderTexture.
        ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
    }
}
