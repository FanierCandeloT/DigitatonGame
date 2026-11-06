using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de un solo uso (sin menú, repetible): importa las imágenes de Art/Cinematica como sprites a pantalla completa
/// y crea/actualiza Resources/Cinematics/Intro.asset con las 10 tomas de la introducción.
/// Para cambiar textos, tiempos o movimientos basta editar ese asset (no hay que volver a ejecutarla; si se ejecuta,
/// SOBRESCRIBE las tomas).
/// </summary>
public static class CinematicSetup
{
    const string ArtFolder = "Assets/_Project/Art/Cinematica";
    const string AssetFolder = "Assets/_Project/Resources/Cinematics";
    const string AssetPath = AssetFolder + "/Intro.asset";

    struct ShotDef
    {
        public string file, name, caption;
        public float hold;
        public CinematicAsset.Move move;
        public Color tint;
        public ShotDef(string file, string name, string caption, float hold, CinematicAsset.Move move, Color? tint = null)
        {
            this.file = file; this.name = name; this.caption = caption; this.hold = hold; this.move = move;
            this.tint = tint ?? Color.white;
        }
    }

    // Los textos son los de StoryFlow.IntroCaptions repartidos en las tomas (todo el texto del juego va en inglés).
    static readonly ShotDef[] Shots =
    {
        new ShotDef("C01_clase",      "La clase",        "Andrés taught English at the Universidad Autónoma de Occidente.", 1.8f, CinematicAsset.Move.ZoomIn),
        new ShotDef("C02_campus",     "El campus",       "In this country, everybody speaks in the passive voice. Things are done. Nobody says who did them.", 1.8f, CinematicAsset.Move.PanRight),
        new ShotDef("C03_pensando",   "Andrés piensa",   "Andrés believed that every action has a doer.", 2.0f, CinematicAsset.Move.ZoomIn),
        new ShotDef("C04_obra",       "La obra",         "Then, one day, an accident happened.", 1.6f, CinematicAsset.Move.PanLeft, new Color(0.9f, 0.88f, 0.9f)),
        new ShotDef("C05_caida",      "El andamio cae",  "A scaffold was left unsecured. By whom? Nobody knows.", 2.0f, CinematicAsset.Move.ZoomIn),
        new ShotDef("C06_suelo",      "En el suelo",     "Your memory has been lost.", 2.0f, CinematicAsset.Move.ZoomIn, new Color(0.85f, 0.85f, 0.9f)),
        new ShotDef("C07_ayuda",      "Piden ayuda",     "And with it, his English. Only his mother tongue, Spanish, remains.", 1.8f, CinematicAsset.Move.PanRight),
        new ShotDef("C08_ambulancia", "Ambulancia",      "Every memory he recovers will bring his English back.", 1.8f, CinematicAsset.Move.PanLeft),
        new ShotDef("C09_habitacion", "Bienestar",       "", 2.6f, CinematicAsset.Move.ZoomOut),
        new ShotDef("C10_despertar",  "Despierta",       "Andrés opened his eyes.", 2.4f, CinematicAsset.Move.ZoomIn),
    };

    public static void Run()
    {
        // 1) Importación: sprites de pantalla completa. Son ilustraciones de alta resolución, así que van con filtro bilineal
        // (con Point se verían dentados al reducirlas) y sin compresión para que el pixel art no se ensucie.
        foreach (var def in Shots)
        {
            string path = $"{ArtFolder}/{def.file}.png";
            if (!File.Exists(path)) { Debug.LogWarning($"[Cinemática] Falta {path}"); continue; }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        // 2) Asset de la cinemática
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Cinematics");

        var asset = AssetDatabase.LoadAssetAtPath<CinematicAsset>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CinematicAsset>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        var list = new System.Collections.Generic.List<CinematicAsset.Shot>();
        foreach (var def in Shots)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{def.file}.png");
            if (sprite == null) continue;
            list.Add(new CinematicAsset.Shot
            {
                name = def.name, image = sprite, caption = def.caption,
                holdSeconds = def.hold, move = def.move, tint = def.tint,
            });
        }
        asset.shots = list.ToArray();
        asset.crossFadeSeconds = 0.7f;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cinemática] {list.Count} tomas en {AssetPath}.");
    }
}
