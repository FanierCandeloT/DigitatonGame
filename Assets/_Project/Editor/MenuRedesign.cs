using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Rediseño de la escena MainMenu (título, botones, fuente Pixelify Sans, degradado, créditos, panel Settings y desenfoque).
/// Sin menús: se llama desde código y es repetible. Solo toca la escena MainMenu y sus assets en Art/UI.
/// El fondo animado ("Fondo animado") no se modifica.
/// </summary>
public static class MenuRedesign
{
    const string UiDir = "Assets/_Project/Art/UI";
    const string FontDir = UiDir + "/Fonts";

    // Paleta tomada del fondo animado, del título y de BotonMenu
    public static readonly Color Terracotta = new Color(0.72f, 0.29f, 0.15f);
    public static readonly Color Orange = new Color(0.96f, 0.52f, 0.12f);
    public static readonly Color Turquoise = new Color(0.22f, 0.78f, 0.72f);
    public static readonly Color Cream = new Color(1f, 0.93f, 0.76f);
    public static readonly Color DarkWarm = new Color(0.09f, 0.05f, 0.04f);

    // ───────────── 1 y 2. Assets y fuente ─────────────

    public static string PrepareAssets()
    {
        var log = new List<string>();
        foreach (var n in new[] { "BotonMenu", "TITULO" })
        {
            var from = $"Assets/_Project/Docs/{n}.png";
            var to = $"{UiDir}/{n}.png";
            if (File.Exists(from) && !File.Exists(to)) log.Add(n + ": " + (AssetDatabase.MoveAsset(from, to) == "" ? "movido a Art/UI" : "ERROR al mover"));
        }

        // TITULO: un solo sprite recortado al título (la hoja original traía recortes sueltos y mucho margen negro transparente)
        ImportOne("TITULO", new Rect(29, 350, 1484, 324), Vector4.zero, FilterMode.Point);
        // BotonMenu: un sprite recortado al botón, con bordes 9-slice (izquierda, abajo, derecha, arriba)
        ImportOne("BotonMenu", new Rect(93, 197, 1986, 408), new Vector4(150, 90, 150, 90), FilterMode.Point);
        log.Add("TITULO y BotonMenu importados como Sprite (Point, sin compresión, 1 rect recortado; BotonMenu con 9-slice)");

        MakeArrow();
        MakeGradient();
        log.Add(CreateFont());
        log.Add(EnsureBlurMaterial());
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    static void ImportOne(string name, Rect rect, Vector4 border, FilterMode filter)
    {
        var path = $"{UiDir}/{name}.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 100f;
        imp.filterMode = filter;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 4096;
        imp.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(new[]
        {
            new SpriteRect { name = name, rect = rect, alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), border = border, spriteID = GUID.Generate() }
        });
        provider.Apply();
        imp.SaveAndReimport();
    }

    static void SaveSinglePng(string name, Texture2D tex, FilterMode filter, float ppu)
    {
        var path = $"{UiDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = filter;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
    }

    /// <summary>Flecha pixel art (crema con borde oscuro) que sale a la izquierda del botón resaltado.</summary>
    static void MakeArrow()
    {
        const int W = 9, H = 13;
        var fill = new bool[W, H];
        for (int y = 0; y < H; y++)
        {
            int half = y <= H / 2 ? y : H - 1 - y;   // 0..6..0
            for (int x = 1; x <= 1 + Mathf.Min(half, W - 3); x++) fill[x, y] = true;
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (fill[x, y]) { tex.SetPixel(x, y, y > H / 2 ? Cream : Color.Lerp(Cream, Orange, 0.45f)); continue; }
                bool edge = false;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < W && ny >= 0 && ny < H && fill[nx, ny]) edge = true;
                }
                tex.SetPixel(x, y, edge ? new Color(0.08f, 0.03f, 0.02f) : clear);
            }
        tex.Apply();
        SaveSinglePng("flecha_menu", tex, FilterMode.Point, 1f);
    }

    /// <summary>Degradado oscuro cálido: transparente arriba y abajo, más oscuro al centro.</summary>
    static void MakeGradient()
    {
        const int H = 256;
        var tex = new Texture2D(4, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++)
        {
            float t = (y + 0.5f) / H;
            float a = 0.78f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 1.3f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0.05f, 0.02f, 0.015f, a));
        }
        tex.Apply();
        SaveSinglePng("gradiente_menu", tex, FilterMode.Bilinear, 100f);
    }

    static string CreateFont()
    {
        var ttf = $"{FontDir}/PixelifySans.ttf";
        var assetPath = $"{FontDir}/PixelifySans Raster.asset";
        AssetDatabase.ImportAsset(ttf, ImportAssetOptions.ForceSynchronousImport);
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (font == null) return "ERROR: no se pudo cargar PixelifySans.ttf";
        AssetDatabase.DeleteAsset(assetPath);

        // RASTER = sin suavizado (mapa de bits, sin SDF): las letras quedan nítidas, cuadradas
        var fa = TMP_FontAsset.CreateFontAsset(font, 32, 4, GlyphRenderMode.RASTER, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c < 127; c++) sb.Append((char)c);
        for (int c = 0xA0; c <= 0xFF; c++) sb.Append((char)c);       // acentos, ñ, ¿ ¡
        sb.Append('–').Append('—').Append('…').Append('•').Append('▶');
        fa.TryAddCharacters(sb.ToString(), out var missing);
        fa.atlasPopulationMode = AtlasPopulationMode.Static;   // no depende de generar glifos en tiempo de ejecución (WebGL)
        fa.name = "PixelifySans Raster";

        AssetDatabase.CreateAsset(fa, assetPath);
        foreach (var tex in fa.atlasTextures)
        {
            tex.name = "PixelifySans Raster Atlas";
            tex.filterMode = FilterMode.Point;   // se ve el pixel, sin suavizar al escalar
            AssetDatabase.AddObjectToAsset(tex, fa);
        }
        fa.material.name = "PixelifySans Raster Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return "Fuente Pixelify Sans (OFL) + Font Asset de TMP en modo RASTER sin suavizado, atlas Point. Shader: " + fa.material.shader.name +
               (string.IsNullOrEmpty(missing) ? "" : " | sin glifo: " + missing);
    }

    static string EnsureBlurMaterial()
    {
        var path = UiDir + "/MenuBlur.mat";
        var shader = Shader.Find("Hidden/DigitatonGame/MenuBlur");
        if (shader == null) return "ERROR: no se encontró el shader MenuBlur";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return "Material MenuBlur listo";
    }

    // ───────────── 3 a 5. Menú y Settings ─────────────

    static TMP_FontAsset font;
    static Sprite buttonSprite, titleSprite, arrowSprite, gradientSprite;

    /// <summary>Carga la fuente y los sprites del estilo (para armar el menú de pausa en el prefab, fuera de MainMenu).</summary>
    public static void LoadStyle()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PixelifySans Raster.asset");
        buttonSprite = LoadSprite("BotonMenu");
        titleSprite = LoadSprite("TITULO");
        arrowSprite = LoadSprite("flecha_menu");
        gradientSprite = LoadSprite("gradiente_menu");
    }

    public static string BuildMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
        // los assets se cargan DESPUÉS de abrir la escena
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PixelifySans Raster.asset");
        buttonSprite = LoadSprite("BotonMenu");
        titleSprite = LoadSprite("TITULO");
        arrowSprite = LoadSprite("flecha_menu");
        gradientSprite = LoadSprite("gradiente_menu");
        var blurMat = AssetDatabase.LoadAssetAtPath<Material>(UiDir + "/MenuBlur.mat");

        var ctl = Object.FindAnyObjectByType<MainMenuController>();
        var root = ctl.transform;

        // Limpieza de una corrida anterior y de lo que se reemplaza
        foreach (var n in new[] { "Capa oscura", "Title", "Difuminado" }) DestroyChild(root, n);
        for (int i = root.childCount - 1; i >= 0; i--) if (root.GetChild(i).name.StartsWith("Creditos")) Object.DestroyImmediate(root.GetChild(i).gameObject);
        var layer = root.Find("MenuLayer");
        if (layer != null)
        {   // devuelve lo que hay que conservar a la raíz antes de borrar la capa
            foreach (var keep in new[] { "Buttons", "Hint" }) { var k = layer.Find(keep); if (k != null) k.SetParent(root, false); }
            Object.DestroyImmediate(layer.gameObject);
        }

        var animatedBg = root.Find("Fondo animado").GetComponent<Image>();   // intacto
        int order = animatedBg.transform.GetSiblingIndex() + 1;

        // ── Difuminado (apagado hasta abrir Settings) ──
        var blurGo = NewRect("Difuminado", root); Stretch(blurGo);
        var blurGroup = blurGo.gameObject.AddComponent<CanvasGroup>();
        blurGroup.interactable = false; blurGroup.blocksRaycasts = false;
        var blurImg = new GameObject("Imagen", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        blurImg.transform.SetParent(blurGo, false);
        blurImg.raycastTarget = false;
        blurImg.color = new Color(0.62f, 0.46f, 0.36f, 1f);   // tono cálido y un poco más oscuro
        var brt = blurImg.rectTransform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        var fit = blurImg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9f;
        var blur = blurGo.gameObject.AddComponent<BackdropBlur>();
        blur.source = animatedBg; blur.target = blurImg; blur.blurMaterial = blurMat; blur.group = blurGroup;
        blurGo.SetSiblingIndex(order++);

        // ── Capa del menú (se oculta con Settings) ──
        var menuLayer = NewRect("MenuLayer", root); Stretch(menuLayer);
        var layerGroup = menuLayer.gameObject.AddComponent<CanvasGroup>();
        menuLayer.SetSiblingIndex(order++);

        // Degradado oscuro solo en la franja central de los botones
        var grad = NewImage("Degradado", menuLayer, Color.white);
        grad.sprite = gradientSprite; grad.raycastTarget = false;
        var grt = grad.rectTransform;
        grt.anchorMin = new Vector2(0f, 0.03f); grt.anchorMax = new Vector2(1f, 0.67f);
        grt.offsetMin = grt.offsetMax = Vector2.zero;

        // Título con sombra, pixel perfect, flotando
        var titleGroup = NewRect("Titulo", menuLayer);
        titleGroup.anchorMin = titleGroup.anchorMax = titleGroup.pivot = new Vector2(0.5f, 0.5f);
        titleGroup.anchoredPosition = new Vector2(0f, 190f);
        titleGroup.sizeDelta = new Vector2(742f, 162f);
        var titleCg = titleGroup.gameObject.AddComponent<CanvasGroup>();
        var shadow = NewImage("Sombra", titleGroup, new Color(0f, 0f, 0f, 0.6f));
        shadow.sprite = titleSprite; shadow.raycastTarget = false;
        shadow.rectTransform.anchorMin = Vector2.zero; shadow.rectTransform.anchorMax = Vector2.one;
        shadow.rectTransform.offsetMin = shadow.rectTransform.offsetMax = new Vector2(7f, -8f);
        var titleImg = NewImage("Sprite", titleGroup, Color.white);
        titleImg.sprite = titleSprite; titleImg.raycastTarget = false; Stretch(titleImg.rectTransform);
        var tf = titleGroup.gameObject.AddComponent<MenuTitleFit>();
        tf.spriteSize = new Vector2(1484f, 324f); tf.blockPixels = 6f; tf.widthFraction = 0.5f;
        var fl = titleGroup.gameObject.AddComponent<UiFloat>();
        fl.amplitude = 7f; fl.period = 4.5f;

        // Botones
        var buttons = root.Find("Buttons");
        buttons.SetParent(menuLayer, false);
        var brect = (RectTransform)buttons;
        brect.anchorMin = brect.anchorMax = brect.pivot = new Vector2(0.5f, 0.5f);
        brect.anchoredPosition = new Vector2(0f, -70f);
        brect.sizeDelta = new Vector2(420f, 256f);
        var vlg = buttons.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 16f; vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        var buttonGroups = new List<CanvasGroup>();
        foreach (var b in new[] { ctl.playButton, ctl.tutorialButton, ctl.settingsButton })
        {
            StyleButton(b, 380f, 72f, 32f);
            buttonGroups.Add(GetOrAdd<CanvasGroup>(b.gameObject));
        }

        // Texto de ayuda: abajo, centrado, pequeño y semitransparente
        var hint = root.Find("Hint").GetComponent<TMP_Text>();
        hint.transform.SetParent(menuLayer, false);
        hint.font = font; hint.fontSize = 20f; hint.text = "Use the arrow keys and Enter, or the mouse";
        hint.alignment = TextAlignmentOptions.Center; hint.color = new Color(Cream.r, Cream.g, Cream.b, 0.88f);
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0.2f, 0f); hrt.anchorMax = new Vector2(0.8f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(0f, 54f); hrt.sizeDelta = new Vector2(0f, 32f);
        PixelTextOutline.Build(hint, 1f);
        var hintCg = GetOrAdd<CanvasGroup>(hint.gameObject);

        // Créditos: esquina inferior derecha
        var credits = NewLabel("Creditos", root, "Universidad Autónoma de Occidente – ODS 8 & 12", 17f, new Color(Cream.r, Cream.g, Cream.b, 0.9f), TextAlignmentOptions.BottomRight);
        var crt = credits.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-22f, 16f); crt.sizeDelta = new Vector2(720f, 28f);
        // Orden: ... Fondo, Difuminado, MenuLayer, Creditos, SettingsPanel (primero se ubica y DESPUÉS se crea el contorno, que va detrás)
        credits.transform.SetSiblingIndex(order++);
        PixelTextOutline.Build(credits, 1f);
        var creditsCg = GetOrAdd<CanvasGroup>(credits.gameObject);

        // ── Settings ──
        BuildSettings(ctl.settingsPanel, root, animatedBg);

        ctl.menuLayer = layerGroup;
        ctl.blur = blur;
        ctl.introSequence = new[] { titleCg, buttonGroups[0], buttonGroups[1], buttonGroups[2], hintCg, creditsCg };
        ctl.settingsPanel.transform.SetAsLastSibling();
        EditorUtility.SetDirty(ctl);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "MainMenu rediseñado.";
    }

    static Sprite LoadSprite(string name) => AssetDatabase.LoadAllAssetsAtPath($"{UiDir}/{name}.png").OfType<Sprite>().First();

    public static void StyleButton(Button b, float w, float h, float fontSize)
    {
        var img = b.GetComponent<Image>();
        img.sprite = buttonSprite; img.type = Image.Type.Sliced; img.color = Color.white;
        img.pixelsPerUnitMultiplier = 408f / h;   // el borde del arte se reduce en la misma proporción que el botón
        img.raycastTarget = true;
        var le = GetOrAdd<LayoutElement>(b.gameObject);
        le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;

        // limpia capas de una corrida anterior
        foreach (var n in new[] { "Brillo", "Flecha" }) DestroyChild(b.transform, n);

        var glow = NewImage("Brillo", b.transform, new Color(1f, 0.86f, 0.5f, 0f));
        glow.sprite = buttonSprite; glow.type = Image.Type.Sliced; glow.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier; glow.raycastTarget = false;
        Stretch(glow.rectTransform);
        glow.transform.SetSiblingIndex(0);

        var arrow = NewImage("Flecha", b.transform, Color.white);
        arrow.sprite = arrowSprite; arrow.raycastTarget = false;
        var art = arrow.rectTransform;
        art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f); art.pivot = new Vector2(1f, 0.5f);
        art.anchoredPosition = new Vector2(-8f, 0f); art.sizeDelta = new Vector2(27f, 39f);
        var arrowCg = arrow.gameObject.AddComponent<CanvasGroup>(); arrowCg.alpha = 0f;

        var label = b.GetComponentsInChildren<TMP_Text>(true).First(t => !t.name.Contains("contorno"));
        label.font = font; label.fontSize = fontSize; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(label.rectTransform);
        // el texto debe quedar sobre el brillo
        label.transform.SetAsLastSibling();
        PixelTextOutline.Build(label, 2f);
        arrow.transform.SetAsLastSibling();

        var style = GetOrAdd<MenuButtonStyle>(b.gameObject);
        style.body = img; style.glow = glow; style.arrow = art; style.arrowGroup = arrowCg;
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
    }

    // ───────────── Settings ─────────────

    public static void BuildSettings(SettingsPanel sp, Transform root, Image animatedBg)
    {
        // La instancia del prefab SettingsPanel se desempaqueta en ESTA escena: así los cambios de color y de estructura se guardan sin overrides perdidos
        if (PrefabUtility.IsPartOfPrefabInstance(sp.gameObject)) PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(sp.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var panelRt = (RectTransform)sp.transform;
        bool wasActive = sp.gameObject.activeSelf;
        sp.gameObject.SetActive(true);
        foreach (var n in new[] { "Content", "Ventana" }) DestroyChild(panelRt, n);

        // El fondo del panel pasa a ser solo un "atrapa clics" (el difuminado hace el trabajo visual)
        var catcher = sp.GetComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f); catcher.raycastTarget = true;
        var group = GetOrAdd<CanvasGroup>(sp.gameObject);

        // Ventana: borde terracota de 2 px y relleno oscuro cálido semitransparente, sin bordes suaves
        var window = NewImage("Ventana", panelRt, Terracotta);
        window.raycastTarget = true;
        var wrt = window.rectTransform;
        wrt.anchorMin = wrt.anchorMax = wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.anchoredPosition = Vector2.zero; wrt.sizeDelta = new Vector2(700f, 520f);
        var inner = NewImage("Relleno", wrt, new Color(DarkWarm.r, DarkWarm.g, DarkWarm.b, 0.93f));
        inner.raycastTarget = false;
        inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(2f, 2f); inner.rectTransform.offsetMax = new Vector2(-2f, -2f);
        // línea naranja fina bajo el título
        var line = NewImage("Linea", wrt, new Color(Orange.r, Orange.g, Orange.b, 0.9f));
        line.raycastTarget = false;
        Place(line.rectTransform, new Vector2(0f, 168f), new Vector2(560f, 2f));

        var title = NewLabel("Titulo", wrt, "Settings", 52f, Cream, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0f, 208f), new Vector2(560f, 64f));
        PixelTextOutline.Build(title, 3f);

        // Filas: etiqueta | control | porcentaje, en columnas parejas
        sp.musicSlider = MakeSlider("Slider musica", wrt, new Vector2(45f, 115f));
        sp.musicValue = RowLabels(wrt, "Music Volume", "80%", 115f);
        sp.sfxSlider = MakeSlider("Slider efectos", wrt, new Vector2(45f, 45f));
        sp.sfxValue = RowLabels(wrt, "Effects Volume", "80%", 45f);
        sp.fullscreenToggle = MakeToggle(wrt, new Vector2(-62f, -25f));
        RowLabels(wrt, "Fullscreen", null, -25f);

        sp.textSpeedButton = MakeMenuButton("Text Speed Button", wrt, "Text Speed: Normal", new Vector2(0f, -105f), new Vector2(400f, 64f), 28f, out sp.textSpeedLabel);
        sp.backButton = MakeMenuButton("Back Button", wrt, "Back", new Vector2(0f, -190f), new Vector2(260f, 60f), 28f, out _);

        var anim = GetOrAdd<SettingsPanelAnimator>(sp.gameObject);
        anim.group = group; anim.window = wrt; anim.seconds = 0.22f; anim.fromScale = 0.9f;

        EditorUtility.SetDirty(sp);
        sp.gameObject.SetActive(wasActive);
    }

    static TMP_Text RowLabels(RectTransform parent, string label, string value, float y)
    {
        var l = NewLabel("Etiqueta " + label, parent, label, 30f, Cream, TextAlignmentOptions.MidlineLeft);
        Place(l.rectTransform, new Vector2(-215f, y), new Vector2(230f, 40f));
        PixelTextOutline.Build(l, 2f);
        if (value == null) return null;
        var v = NewLabel("Valor " + label, parent, value, 30f, Cream, TextAlignmentOptions.MidlineRight);
        Place(v.rectTransform, new Vector2(265f, y), new Vector2(110f, 40f));
        PixelTextOutline.Build(v, 2f);
        return v;
    }

    static Slider MakeSlider(string name, RectTransform parent, Vector2 pos)
    {
        var bg = NewImage(name, parent, Terracotta);   // el borde de 2 px
        Place(bg.rectTransform, pos, new Vector2(260f, 26f));
        var inner = NewImage("Interior", bg.rectTransform, DarkWarm);
        inner.raycastTarget = false;
        inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(2f, 2f); inner.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var fillArea = NewRect("Fill Area", bg.rectTransform);
        fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(8f, 4f); fillArea.offsetMax = new Vector2(-8f, -4f);
        var fill = NewImage("Fill", fillArea, Orange);
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(8f, 0f);

        var handleArea = NewRect("Handle Slide Area", bg.rectTransform);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(8f, 0f); handleArea.offsetMax = new Vector2(-8f, 0f);
        var handle = NewImage("Handle", handleArea, Terracotta);   // cuadrada, no redonda
        handle.rectTransform.sizeDelta = new Vector2(18f, 38f);
        var handleIn = NewImage("Interior", handle.rectTransform, Cream);
        handleIn.raycastTarget = false;
        handleIn.rectTransform.anchorMin = Vector2.zero; handleIn.rectTransform.anchorMax = Vector2.one;
        handleIn.rectTransform.offsetMin = new Vector2(2f, 2f); handleIn.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var s = bg.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f; s.wholeNumbers = false; s.value = 0.8f;
        s.transition = Selectable.Transition.ColorTint;
        s.colors = SelectColors();
        s.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        return s;
    }

    static Toggle MakeToggle(RectTransform parent, Vector2 pos)
    {
        var bg = NewImage("Checkbox", parent, Terracotta);
        Place(bg.rectTransform, pos, new Vector2(40f, 40f));
        var inner = NewImage("Interior", bg.rectTransform, DarkWarm);
        inner.raycastTarget = false;
        inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(3f, 3f); inner.rectTransform.offsetMax = new Vector2(-3f, -3f);
        var mark = NewImage("Marca", bg.rectTransform, Turquoise);
        mark.raycastTarget = false;
        mark.rectTransform.anchorMin = Vector2.zero; mark.rectTransform.anchorMax = Vector2.one;
        mark.rectTransform.offsetMin = new Vector2(10f, 10f); mark.rectTransform.offsetMax = new Vector2(-10f, -10f);
        var t = bg.gameObject.AddComponent<Toggle>();
        t.targetGraphic = bg; t.graphic = mark; t.isOn = false;
        t.transition = Selectable.Transition.ColorTint;
        t.colors = SelectColors();
        t.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        return t;
    }

    static ColorBlock SelectColors() => new ColorBlock
    {
        normalColor = Color.white,
        highlightedColor = new Color(1f, 0.82f, 0.55f),
        selectedColor = new Color(1f, 0.74f, 0.38f),
        pressedColor = new Color(0.7f, 0.5f, 0.35f),
        disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f),
        colorMultiplier = 1f,
        fadeDuration = 0.08f,
    };

    public static Button MakeMenuButton(string name, RectTransform parent, string text, Vector2 pos, Vector2 size, float fontSize, out TMP_Text label)
    {
        var img = NewImage(name, parent, Color.white);
        Place(img.rectTransform, pos, size);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var t = NewLabel("Texto", img.rectTransform, text, fontSize, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        label = t;
        // StyleButton busca el TMP hijo y arma brillo, flecha y contorno
        StyleButton(b, size.x, size.y, fontSize);
        // fuera de un LayoutGroup el tamaño lo manda el RectTransform
        Place(img.rectTransform, pos, size);
        return b;
    }

    // ───────────── Utilidades ─────────────

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static TMP_Text NewLabel(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    public static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    public static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
