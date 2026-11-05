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
    public const int ViewWidth = 640;    // 13,3 casillas de ancho
    public const int ViewHeight = 360;   // 7,5 casillas de alto (16:9; a 1080p cada pixel se ve de 3x3)

    /// <summary>Mitad de la altura visible en unidades de mundo.</summary>
    public const float OrthoSize = ViewHeight / 2f / Ppu;

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
