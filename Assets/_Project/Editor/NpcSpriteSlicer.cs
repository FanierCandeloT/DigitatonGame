using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Recorta las 40 hojas de personajes ("2D Top Down Pixel Art Characters") en una cuadrícula exacta:
/// 3 columnas x 4 filas de 20x32 px. Filas: 0 abajo, 1 izquierda, 2 derecha, 3 arriba. Columnas: 0 y 2 = paso, 1 = quieto.
/// Cada cuadro se llama "NNN_i" (i = fila*3 + columna), con el pivote en los pies (abajo al centro), 16 píxeles por unidad
/// (el mismo que la cámara: 1 píxel del arte = 1 píxel de pantalla, así Andrés y los NPC miden lo mismo: 2 unidades de alto),
/// filtro Point y sin compresión para que el pixel art se vea nítido.
/// </summary>
public static class NpcSpriteSlicer
{
    public const string Folder = "Assets/_Project/Art/Sprites Personajes/2D Top Down Pixel Art Characters/";
    public const int CellWidth = 20, CellHeight = 32, Columns = 3, Rows = 4;
    public const float Ppu = 16f;

    [MenuItem("DigitatonGame/Recortar sprites de NPC")]
    public static void SliceAll()
    {
        int done = 0;
        var factory = new SpriteDataProviderFactories();
        factory.Init();

        foreach (var path in Directory.GetFiles(Folder, "*.png"))
        {
            string assetPath = path.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name == "Reference") continue;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var old = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int index = row * Columns + col;
                    string spriteName = $"{name}_{index}";
                    rects.Add(new SpriteRect
                    {
                        name = spriteName,
                        spriteID = old.TryGetValue(spriteName, out var id) ? id : GUID.Generate(),
                        rect = new Rect(col * CellWidth, (Rows - 1 - row) * CellHeight, CellWidth, CellHeight),
                        alignment = SpriteAlignment.BottomCenter,
                        pivot = new Vector2(0.5f, 0f),
                    });
                }
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
            importer.SaveAndReimport();
            done++;
        }
        Debug.Log($"NPC: {done} hojas recortadas en {Columns}x{Rows} cuadros de {CellWidth}x{CellHeight} px (pivote en los pies, {Ppu} PPU).");
    }

    /// <summary>Los 12 cuadros de una hoja, en orden (fila*3 + columna). Vacío si la hoja no existe o no está recortada.</summary>
    public static Sprite[] Load(int sheet)
    {
        string path = $"{Folder}{sheet:D3}.png";
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
    }
}
