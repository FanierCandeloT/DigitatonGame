using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Genera el flujo de menús: prefab de Settings, prefab GameManager (Resources), escena MainMenu y escena Tutorial.
/// Menú: DigitatonGame ▸ Construir flujo de menús. Recrea esos prefabs y las escenas MainMenu y Tutorial.
/// </summary>
public static class MenuFlowBuilder
{
    const string Root = "Assets/_Project";
    const string SettingsPrefabPath = Root + "/Prefabs/UI/SettingsPanel.prefab";
    const string GameManagerPrefabPath = Root + "/Resources/GameManager.prefab";

    static readonly Color Dim = new Color(0f, 0f, 0f, 0.85f);
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.13f, 0.98f);
    static readonly Color Accent = new Color(1f, 0.85f, 0.30f);

    [MenuItem("DigitatonGame/Construir flujo de menús")]
    public static void BuildAll()
    {
        foreach (var f in new[] { "Prefabs/UI", "Resources", "Scripts/UI" })
        {
            var p = Root + "/" + f;
            if (!AssetDatabase.IsValidFolder(p))
            {
                var parent = Path.GetDirectoryName(p).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, Path.GetFileName(p));
            }
        }

        var settingsPrefab = BuildSettingsPanelPrefab();
        BuildGameManagerPrefab(settingsPrefab);
        BuildMainMenuScene(settingsPrefab);
        BuildTutorialScene();
        GreyboxBuilder.RegisterScenesInBuild();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Flujo de menús construido.");
    }

    // ───────────────────────── Helpers de UI ─────────────────────────

    static TMP_DefaultControls.Resources Res() => new TMP_DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
    };

    static DefaultControls.Resources UiRes() => new DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
    };

    public static GameObject MakeCanvas(string name, int sortingOrder, bool raycaster = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (raycaster) go.AddComponent<GraphicRaycaster>();
        return go;
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static TMP_Text NewText(string name, Transform parent, string text, float size,
        TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = style;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static void LayoutSize(GameObject go, float width, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (width > 0) { le.preferredWidth = width; le.minWidth = width; }
        if (height > 0) { le.preferredHeight = height; le.minHeight = height; }
    }

    /// <summary>Panel centrado con layout vertical.</summary>
    static RectTransform VerticalPanel(string name, Transform parent, Vector2 size, Color bg, float spacing = 16f, int padding = 30)
    {
        var img = NewImage(name, parent, bg);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = new RectOffset(padding, padding, padding, padding);
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        return rt;
    }

    static void StyleSelectable(Selectable s, Color normal, Color selected)
    {
        var c = s.colors;
        c.normalColor = normal;
        c.highlightedColor = selected;
        c.selectedColor = selected;
        c.pressedColor = selected * 0.8f;
        c.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.05f;
        s.colors = c;
    }

    static Button MakeButton(Transform parent, string text, float width = 360f, float height = 56f)
    {
        var go = TMP_DefaultControls.CreateButton(Res());
        go.name = text + " Button";
        go.transform.SetParent(parent, false);
        var label = go.GetComponentInChildren<TMP_Text>();
        label.text = text;
        label.fontSize = 30;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        var btn = go.GetComponent<Button>();
        go.GetComponent<Image>().color = Color.white;
        StyleSelectable(btn, new Color(0.17f, 0.19f, 0.28f), new Color(0.30f, 0.45f, 0.90f));
        LayoutSize(go, width, height);
        return btn;
    }

    static Slider MakeSlider(Transform parent)
    {
        var go = DefaultControls.CreateSlider(UiRes());
        go.transform.SetParent(parent, false);
        var s = go.GetComponent<Slider>();
        s.minValue = 0f;
        s.maxValue = 1f;
        go.transform.Find("Background").GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.3f);
        s.fillRect.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.95f);
        StyleSelectable(s, Color.white, Accent);
        return s;
    }

    static Toggle MakeToggle(Transform parent, string text)
    {
        var go = DefaultControls.CreateToggle(UiRes());
        go.name = text + " Toggle";
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Toggle>();
        var bg = go.transform.Find("Background").GetComponent<RectTransform>();
        bg.sizeDelta = new Vector2(34, 34);
        bg.anchoredPosition = new Vector2(19, -22);

        // El toggle base trae un texto antiguo (UI.Text): se cambia por uno de TextMeshPro.
        Object.DestroyImmediate(go.transform.Find("Label").gameObject);
        var label = NewText("Label", go.transform, text, 30, TextAlignmentOptions.MidlineLeft);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(48, 2);
        lrt.offsetMax = new Vector2(-5, -2);
        StyleSelectable(t, Color.white, Accent);
        LayoutSize(go, 360, 44);
        return t;
    }

    /// <summary>Fila: etiqueta + slider + valor en %.</summary>
    static Slider MakeSliderRow(Transform parent, string label, out TMP_Text valueText)
    {
        var row = NewRect(label + " Row", parent);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        LayoutSize(row.gameObject, 620, 44);

        var l = NewText("Label", row, label, 30, TextAlignmentOptions.MidlineLeft);
        LayoutSize(l.gameObject, 250, 44);

        var slider = MakeSlider(row);
        LayoutSize(slider.gameObject, 260, 28);

        valueText = NewText("Value", row, "80%", 28, TextAlignmentOptions.MidlineRight);
        LayoutSize(valueText.gameObject, 70, 44);
        return slider;
    }

    // ───────────────────────── Prefab de Settings ─────────────────────────

    static GameObject BuildSettingsPanelPrefab()
    {
        var root = new GameObject("SettingsPanel", typeof(RectTransform));
        Stretch((RectTransform)root.transform);
        var dim = root.AddComponent<Image>();
        dim.color = Dim;

        var content = VerticalPanel("Content", root.transform, new Vector2(720, 540), PanelColor, 20f, 32);

        var title = NewText("Title", content, "Settings", 48, TextAlignmentOptions.Center, FontStyles.Bold);
        LayoutSize(title.gameObject, 0, 64);

        var music = MakeSliderRow(content, "Music Volume", out var musicValue);
        var sfx = MakeSliderRow(content, "Effects Volume", out var sfxValue);
        var fullscreen = MakeToggle(content, "Fullscreen");
        var speed = MakeButton(content, "Text Speed: Normal", 420, 56);
        var back = MakeButton(content, "Back", 240, 56);

        var panel = root.AddComponent<SettingsPanel>();
        panel.musicSlider = music;
        panel.musicValue = musicValue;
        panel.sfxSlider = sfx;
        panel.sfxValue = sfxValue;
        panel.fullscreenToggle = fullscreen;
        panel.textSpeedButton = speed;
        panel.textSpeedLabel = speed.GetComponentInChildren<TMP_Text>();
        panel.backButton = back;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ───────────────────────── Prefab GameManager ─────────────────────────

    static void BuildGameManagerPrefab(GameObject settingsPrefab)
    {
        var root = new GameObject("GameManager");
        var gm = root.AddComponent<GameManager>();

        // EventSystem con el módulo del nuevo Input System
        var es = new GameObject("EventSystem");
        es.transform.SetParent(root.transform, false);
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // Fundido a negro (encima de todo)
        var faderCanvas = MakeCanvas("FaderCanvas", 1000);
        faderCanvas.transform.SetParent(root.transform, false);
        faderCanvas.AddComponent<CanvasGroup>();
        var fader = faderCanvas.AddComponent<ScreenFader>();
        var black = NewImage("Black", faderCanvas.transform, Color.black);
        Stretch(black.rectTransform);
        black.raycastTarget = true;

        var so = new SerializedObject(gm);
        so.FindProperty("fader").objectReferenceValue = fader;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Pausa + Settings + Cuaderno
        var pauseCanvas = MakeCanvas("PauseCanvas", 500);
        pauseCanvas.transform.SetParent(root.transform, false);

        var pauseRoot = NewImage("PauseRoot", pauseCanvas.transform, Dim);
        Stretch(pauseRoot.rectTransform);
        var buttons = VerticalPanel("Buttons", pauseRoot.transform, new Vector2(520, 420), PanelColor, 20f, 32);
        var pauseTitle = NewText("Title", buttons, "Paused", 52, TextAlignmentOptions.Center, FontStyles.Bold);
        LayoutSize(pauseTitle.gameObject, 0, 72);
        var resume = MakeButton(buttons, "Resume");
        var settings = MakeButton(buttons, "Settings");
        var mainMenu = MakeButton(buttons, "Main Menu");

        var settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, pauseCanvas.transform);
        settingsInstance.SetActive(false);

        var notebookPanel = NewImage("NotebookPanel", pauseCanvas.transform, Dim);
        Stretch(notebookPanel.rectTransform);
        var book = VerticalPanel("Book", notebookPanel.transform, new Vector2(900, 560), new Color(0.93f, 0.88f, 0.74f), 20f, 40);
        var bookTitle = NewText("Title", book, "Professor's Notebook", 52, TextAlignmentOptions.Center, FontStyles.Bold);
        bookTitle.color = new Color(0.25f, 0.15f, 0.05f);
        LayoutSize(bookTitle.gameObject, 0, 72);
        var hint = NewText("Hint", book, "Press C or Esc to close", 24, TextAlignmentOptions.Center, FontStyles.Italic);
        hint.color = new Color(0.4f, 0.3f, 0.2f);
        LayoutSize(hint.gameObject, 0, 36);
        notebookPanel.gameObject.SetActive(false);

        var notebook = pauseCanvas.AddComponent<NotebookPanel>();
        notebook.panel = notebookPanel.gameObject;

        var pause = pauseCanvas.AddComponent<PauseMenu>();
        pause.pauseRoot = pauseRoot.gameObject;
        pause.buttonsRoot = buttons.gameObject;
        pause.resumeButton = resume;
        pause.settingsButton = settings;
        pause.mainMenuButton = mainMenu;
        pause.settingsPanel = settingsInstance.GetComponent<SettingsPanel>();
        pause.notebook = notebook;

        pauseRoot.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        Object.DestroyImmediate(root);
        CampusBuilder.EnsureToast();
    }

    // ───────────────────────── Escena MainMenu ─────────────────────────

    static void BuildMainMenuScene(GameObject settingsPrefab)
    {
        GreyboxBuilder.BuildMenuScene("MainMenu");   // cámara + luz, y deja la escena abierta
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("MainMenu.unity"))
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/MainMenu.unity");

        var canvasGo = MakeCanvas("MenuCanvas", 0);
        var bg = NewImage("Background", canvasGo.transform, new Color(0.07f, 0.08f, 0.13f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;

        var title = NewText("Title", canvasGo.transform, "Passive Amnesia: UAO Edition", 68, TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(1150, 180);
        trt.anchoredPosition = new Vector2(0, 190);
        title.color = Accent;

        var buttonsRt = NewRect("Buttons", canvasGo.transform);
        buttonsRt.anchorMin = buttonsRt.anchorMax = buttonsRt.pivot = new Vector2(0.5f, 0.5f);
        buttonsRt.sizeDelta = new Vector2(400, 260);
        buttonsRt.anchoredPosition = new Vector2(0, -60);
        var v = buttonsRt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 20;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;

        var play = MakeButton(buttonsRt, "Play", 360, 64);
        var tutorial = MakeButton(buttonsRt, "Tutorial", 360, 64);
        var settings = MakeButton(buttonsRt, "Settings", 360, 64);

        var hint = NewText("Hint", canvasGo.transform, "Use the arrow keys and Enter, or the mouse", 24, TextAlignmentOptions.Center);
        hint.color = new Color(0.7f, 0.72f, 0.8f);
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0, 0);
        hrt.anchorMax = new Vector2(1, 0);
        hrt.pivot = new Vector2(0.5f, 0);
        hrt.sizeDelta = new Vector2(0, 50);
        hrt.anchoredPosition = new Vector2(0, 20);

        var settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, canvasGo.transform);
        settingsInstance.SetActive(false);

        var menu = canvasGo.AddComponent<MainMenuController>();
        menu.playButton = play;
        menu.tutorialButton = tutorial;
        menu.settingsButton = settings;
        menu.buttonsRoot = buttonsRt.gameObject;
        menu.settingsPanel = settingsInstance.GetComponent<SettingsPanel>();
        menu.playScene = "Campus";
        menu.tutorialScene = "Tutorial";

        GreyboxBuilder.SaveScene(scene, "MainMenu");
    }

    // ───────────────────────── Escena Tutorial ─────────────────────────

    static void BuildTutorialScene()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Placeholders/Square.png");
        var scene = GreyboxBuilder.NewScene();
        GreyboxBuilder.AddGlobalLight();

        var inner = new Vector2(20f, 10f);
        GreyboxBuilder.AddBounds(inner + Vector2.one * 2f);
        GreyboxBuilder.MakeBox("Tutorial Floor", PlaceholderKind.Walkway, Vector2.zero, inner, sprite,
            "Piso de la sala de práctica.", new Vector2Int(320, 160), order: -100, label: false);
        GreyboxBuilder.AddBorderWalls(inner, sprite);

        GreyboxBuilder.MakeBox("Practice Wall", PlaceholderKind.Wall, new Vector2(-2f, -0.5f), new Vector2(0.5f, 6f), sprite,
            "Muro de práctica: Andrés no puede atravesarlo.", null, solid: true);

        var practice = GreyboxBuilder.MakeBox("Practice Object", PlaceholderKind.Interactive, new Vector2(4f, 2f), new Vector2(1f, 1f), sprite,
            "Objeto interactivo de práctica.", null, trigger: true, order: 1);
        var practiceInteractable = practice.AddComponent<Interactable>();

        GreyboxBuilder.MakeDoor("Exit to Main Menu", new Vector2(7f, -5.25f), new Vector2(1.5f, 1f), sprite, "MainMenu", "");
        GreyboxBuilder.MakeSpawn("Tutorial_Default", new Vector2(-7f, 0f), true);
        GreyboxBuilder.AddPlayerAndCamera(new Vector2(-7f, 0f));

        // Mensajes en pantalla
        var canvasGo = MakeCanvas("TutorialCanvas", 100, raycaster: false);
        var panel = NewImage("MessagePanel", canvasGo.transform, new Color(0f, 0f, 0f, 0.75f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(1000, 110);
        prt.anchoredPosition = new Vector2(0, 24);
        panel.raycastTarget = false;
        var msg = NewText("Message", panel.transform, "", 32, TextAlignmentOptions.Center);
        Stretch(msg.rectTransform);
        msg.rectTransform.offsetMin = new Vector2(24, 8);
        msg.rectTransform.offsetMax = new Vector2(-24, -8);

        var tm = canvasGo.AddComponent<TutorialManager>();
        tm.messageText = msg;
        tm.practiceObject = practiceInteractable;

        GreyboxBuilder.SaveScene(scene, "Tutorial");
    }
}
