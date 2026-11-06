using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Agrega el botón y el panel de créditos a la escena MainMenu (y crea Data/Credits.asset con los textos).
/// Sin menús, repetible. Solo toca MainMenu. Reutiliza el estilo de MenuRedesign.
/// </summary>
public static class CreditsSetup
{
    const string AssetPath = "Assets/_Project/Data/Credits.asset";
    const string FontPath = "Assets/_Project/Art/UI/Fonts/PixelifySans Raster.asset";

    public static string CreateAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data")) AssetDatabase.CreateFolder("Assets/_Project", "Data");
        var existing = AssetDatabase.LoadAssetAtPath<CreditsAsset>(AssetPath);
        if (existing != null) return "Credits.asset ya existía (no se pisó)";

        var asset = ScriptableObject.CreateInstance<CreditsAsset>();
        asset.title = "Credits";
        asset.blocks = new[]
        {
            new CreditBlock
            {
                entries = new[]
                {
                    new CreditEntry { text = "Passive Amnesia: UAO Edition", big = true },
                    new CreditEntry { text = "A Digitatón project" },
                    new CreditEntry { text = "Universidad Autónoma de Occidente – Cali" },
                },
            },
            new CreditBlock
            {
                heading = "DEVELOPMENT TEAM",
                entries = new[]
                {
                    new CreditEntry { text = "Fanier Andrés Candelo Triana", detail = "2240623" },
                    new CreditEntry { text = "Juan Camilo Trujillo Vargas", detail = "2240209" },
                    new CreditEntry { text = "Sebastián Meneses Molina", detail = "2240311" },
                },
            },
            new CreditBlock
            {
                heading = "TOOLS AND RESOURCES",
                entries = new[]
                {
                    new CreditEntry { text = "Made with Unity" },
                    new CreditEntry { text = "Font: Pixelify Sans (SIL Open Font License)" },
                    new CreditEntry { text = "Sustainable Development Goals: ODS 8 & 12" },
                },
            },
        };
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        return "Credits.asset creado";
    }

    public static string Build()
    {
        MenuRedesign.LoadStyle();
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
        // los assets se cargan DESPUÉS de abrir la escena
        var asset = AssetDatabase.LoadAssetAtPath<CreditsAsset>(AssetPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var ctl = Object.FindAnyObjectByType<MainMenuController>();
        var canvas = ctl.transform;
        var layer = ctl.menuLayer.transform;

        // Limpieza de una corrida anterior
        MenuRedesign.DestroyChild(layer, "Credits Button");
        MenuRedesign.DestroyChild(canvas, "CreditsPanel");

        // ── Botón: esquina inferior izquierda, más pequeño que los del menú ──
        var button = MenuRedesign.MakeMenuButton("Credits Button", (RectTransform)layer, "Credits", Vector2.zero, new Vector2(190f, 52f), 24f, out _);
        var brt = (RectTransform)button.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0f);
        brt.anchoredPosition = new Vector2(64f, 26f);   // margen de la orilla (la flecha sale a la izquierda y necesita sitio)
        var buttonGroup = MenuRedesign.GetOrAdd<CanvasGroup>(button.gameObject);

        // Navegación con teclado: Tutorial → Settings → Credits (y de vuelta)
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = ctl.settingsButton };
        var sn = ctl.settingsButton.navigation;
        sn.mode = Navigation.Mode.Explicit;
        sn.selectOnUp = ctl.tutorialButton;
        sn.selectOnDown = button;
        ctl.settingsButton.navigation = sn;

        // ── Panel ──
        var panelImage = MenuRedesign.NewImage("CreditsPanel", canvas, Color.clear);   // atrapa clics; el difuminado hace lo visual
        var panelRt = panelImage.rectTransform;
        MenuRedesign.Stretch(panelRt);
        panelImage.raycastTarget = true;
        var group = panelImage.gameObject.AddComponent<CanvasGroup>();

        var window = MenuRedesign.NewImage("Ventana", panelRt, MenuRedesign.Terracotta);   // borde de 2 px
        var wrt = window.rectTransform;
        MenuRedesign.Place(wrt, Vector2.zero, new Vector2(700f, 650f));
        var inner = MenuRedesign.NewImage("Relleno", wrt, new Color(MenuRedesign.DarkWarm.r, MenuRedesign.DarkWarm.g, MenuRedesign.DarkWarm.b, 0.93f));
        inner.raycastTarget = false;
        inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(2f, 2f); inner.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var title = MenuRedesign.NewLabel("Titulo", wrt, "Credits", 52f, MenuRedesign.Cream, TextAlignmentOptions.Center);
        MenuRedesign.Place(title.rectTransform, new Vector2(0f, 285f), new Vector2(560f, 64f));
        PixelTextOutline.Build(title, 3f);
        var line = MenuRedesign.NewImage("Linea", wrt, new Color(MenuRedesign.Orange.r, MenuRedesign.Orange.g, MenuRedesign.Orange.b, 0.9f));
        line.raycastTarget = false;
        MenuRedesign.Place(line.rectTransform, new Vector2(0f, 245f), new Vector2(580f, 2f));

        // Zona con desplazamiento
        var scrollRt = MenuRedesign.NewRect("Scroll", wrt);
        MenuRedesign.Place(scrollRt, new Vector2(0f, 15f), new Vector2(640f, 440f));
        var viewport = MenuRedesign.NewRect("Viewport", scrollRt);
        MenuRedesign.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = MenuRedesign.NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0f, 0f);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.spacing = 4f; vlg.padding = new RectOffset(20, 20, 4, 8);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = false;

        var back = MenuRedesign.MakeMenuButton("Back Button", wrt, "Back", new Vector2(0f, -268f), new Vector2(220f, 60f), 28f, out _);

        var animator = panelImage.gameObject.AddComponent<SettingsPanelAnimator>();
        animator.group = group; animator.window = wrt; animator.seconds = 0.22f; animator.fromScale = 0.9f;

        var credits = panelImage.gameObject.AddComponent<CreditsPanel>();
        credits.asset = asset; credits.font = font; credits.titleLabel = title;
        credits.scroll = scroll; credits.content = content; credits.backButton = back;

        // ── Conexión con el menú ──
        ctl.creditsButton = button;
        ctl.creditsPanel = credits;
        var seq = ctl.introSequence.ToList();
        seq.RemoveAll(g => g == null || g == buttonGroup);
        int at = Mathf.Max(0, seq.Count - 2);   // antes de la ayuda y de los créditos de la UAO
        seq.Insert(at, buttonGroup);
        ctl.introSequence = seq.ToArray();
        panelImage.transform.SetAsLastSibling();
        EditorUtility.SetDirty(ctl);

        panelImage.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Botón y panel de créditos agregados a MainMenu.";
    }
}
