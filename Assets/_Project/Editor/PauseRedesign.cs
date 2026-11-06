using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rehace el menú de pausa DENTRO del prefab original (Resources/GameManager.prefab, PauseCanvas), con el diseño del MainMenu.
/// Como solo existe ahí, el cambio vale en todas las escenas. Sin menús; repetible (borra el menú anterior y lo vuelve a armar).
/// </summary>
public static class PauseRedesign
{
    const string PrefabPath = "Assets/_Project/Resources/GameManager.prefab";
    const string BlurMat = "Assets/_Project/Art/UI/MenuBlur.mat";
    const string FontPath = "Assets/_Project/Art/UI/Fonts/PixelifySans Raster.asset";

    public static string Build()
    {
        MenuRedesign.LoadStyle();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var pm = root.GetComponentInChildren<PauseMenu>(true);
            var canvas = (RectTransform)pm.transform;   // PauseCanvas

            // El menú viejo (fondo azul, "Paused", botones azules y el SettingsPanel viejo) se elimina
            foreach (var old in new[] { "PauseRoot", "SettingsPanel" }) MenuRedesign.DestroyChild(canvas, old);

            // ── PauseRoot: fondo difuminado + ventana ──
            var pauseRoot = MenuRedesign.NewRect("PauseRoot", canvas);
            MenuRedesign.Stretch(pauseRoot);
            pauseRoot.SetSiblingIndex(0);
            var rootGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();

            var backdropImage = new GameObject("Difuminado", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            backdropImage.transform.SetParent(pauseRoot, false);
            MenuRedesign.Stretch(backdropImage.rectTransform);
            backdropImage.raycastTarget = true;   // bloquea los clics hacia el juego
            var backdrop = pauseRoot.gameObject.AddComponent<PauseBackdrop>();
            backdrop.image = backdropImage;
            backdrop.blurMaterial = AssetDatabase.LoadAssetAtPath<Material>(BlurMat);

            // ── Ventana: borde terracota de 2 px, relleno oscuro cálido ──
            var window = MenuRedesign.NewImage("Ventana", pauseRoot, MenuRedesign.Terracotta);
            var wrt = window.rectTransform;
            MenuRedesign.Place(wrt, Vector2.zero, new Vector2(520f, 390f));
            var windowGroup = window.gameObject.AddComponent<CanvasGroup>();
            var inner = MenuRedesign.NewImage("Relleno", wrt, new Color(MenuRedesign.DarkWarm.r, MenuRedesign.DarkWarm.g, MenuRedesign.DarkWarm.b, 0.93f));
            inner.raycastTarget = false;
            inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(2f, 2f); inner.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var title = MenuRedesign.NewLabel("Titulo", wrt, "Paused", 52f, MenuRedesign.Cream, TextAlignmentOptions.Center);
            MenuRedesign.Place(title.rectTransform, new Vector2(0f, 155f), new Vector2(440f, 64f));
            PixelTextOutline.Build(title, 3f);
            var line = MenuRedesign.NewImage("Linea", wrt, new Color(MenuRedesign.Orange.r, MenuRedesign.Orange.g, MenuRedesign.Orange.b, 0.9f));
            line.raycastTarget = false;
            MenuRedesign.Place(line.rectTransform, new Vector2(0f, 115f), new Vector2(420f, 2f));

            var resume = MenuRedesign.MakeMenuButton("Resume Button", wrt, "Resume", new Vector2(0f, 50f), new Vector2(380f, 72f), 32f, out _);
            var settings = MenuRedesign.MakeMenuButton("Settings Button", wrt, "Settings", new Vector2(0f, -38f), new Vector2(380f, 72f), 32f, out _);
            var mainMenu = MenuRedesign.MakeMenuButton("Main Menu Button", wrt, "Main Menu", new Vector2(0f, -126f), new Vector2(380f, 72f), 32f, out _);

            // ── Settings (el mismo panel rediseñado del MainMenu), encima de la pausa ──
            var spGo = MenuRedesign.NewImage("SettingsPanel", canvas, Color.clear);
            MenuRedesign.Stretch(spGo.rectTransform);
            spGo.transform.SetSiblingIndex(1);
            var sp = spGo.gameObject.AddComponent<SettingsPanel>();
            MenuRedesign.BuildSettings(sp, canvas, null);

            // ── Conexiones ──
            pm.pauseRoot = pauseRoot.gameObject;
            pm.rootGroup = rootGroup;
            pm.window = wrt;
            pm.windowGroup = windowGroup;
            pm.backdrop = backdrop;
            pm.buttonsRoot = window.gameObject;
            pm.resumeButton = resume;
            pm.settingsButton = settings;
            pm.mainMenuButton = mainMenu;
            pm.settingsPanel = sp;
            EditorUtility.SetDirty(pm);

            pauseRoot.gameObject.SetActive(false);
            sp.gameObject.SetActive(false);

            // El panel de progreso usa la misma fuente
            var hud = root.GetComponentInChildren<MemoryHud>(true);
            if (hud != null)
            {
                hud.pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                EditorUtility.SetDirty(hud);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Menú de pausa rehecho dentro de " + PrefabPath + " (PauseCanvas). Fuente del panel de progreso asignada.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
