using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pone la tira Art/UI/fondo_menu_anim.png (24 cuadros de 320x180) como fondo animado de MainMenu.
/// Sin menús, repetible, y solo toca la escena MainMenu.
/// </summary>
public static class MenuBackgroundSetup
{
    const string Path = "Assets/_Project/Art/UI/fondo_menu_anim.png";
    const int Frames = 24, FrameW = 320, FrameH = 180;

    public static string ImportStrip()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(Path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 48f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 8192;
        imp.SaveAndReimport();

        var rects = new List<SpriteRect>();
        for (int i = 0; i < Frames; i++)
            rects.Add(new SpriteRect
            {
                name = $"fondo_menu_anim_{i}",
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate(),
            });
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects.ToArray());
        provider.Apply();
        imp.SaveAndReimport();
        return $"Tira importada: {Frames} cuadros de {FrameW}x{FrameH}, Point, sin compresión.";
    }

    public static string BuildBackground(bool pingPong, float darkAlpha)
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
        // los sprites se cargan DESPUÉS de abrir la escena
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        var menu = Object.FindAnyObjectByType<MainMenuController>().transform;
        foreach (var old in new[] { "Fondo animado", "Capa oscura" })
        {
            var t = menu.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // Fondo: cubre toda la pantalla sin deformarse (Envelope: mantiene 16:9 y recorta lo que sobre en otras proporciones)
        var bg = PixelUi.NewImage("Fondo animado", menu, Color.white);
        bg.sprite = sprites[0];
        bg.raycastTarget = false;
        bg.preserveAspect = false;
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;
        var anim = bg.gameObject.AddComponent<SpriteStripAnimator>();
        anim.frames = sprites;
        anim.forwardSeconds = 4.0167f;
        anim.pingPong = pingPong;

        // Capa oscura semitransparente para que el título y los botones se lean
        var dark = PixelUi.NewImage("Capa oscura", menu, new Color(0.02f, 0.03f, 0.08f, darkAlpha));
        dark.raycastTarget = false;
        PixelUi.Stretch(dark.rectTransform);

        // Detrás del título y los botones, pero delante del color liso que ya existía
        var solid = menu.Find("Background");
        int baseIndex = solid != null ? solid.GetSiblingIndex() + 1 : 0;
        bg.transform.SetSiblingIndex(baseIndex);
        dark.transform.SetSiblingIndex(baseIndex + 1);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"MainMenu: fondo animado ({sprites.Length} cuadros, pingPong={pingPong}) y capa oscura (alpha {darkAlpha}).";
    }
}
