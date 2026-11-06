using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DigitatonGame ▸ Audio Test: probar cada sonido sin jugar la partida.
///  - En Play usa el AudioManager de verdad (mezclador, grupos, volúmenes de Settings, recorte de silencios...): es el mismo camino que usa el juego.
///  - Fuera de Play reproduce el archivo directo (no hay AudioManager; ignora el mezclador) solo para oír el clip.
/// Muestra si el AudioManager está activo, el volumen guardado de Music y SFX y lo que llega al GameMixer en decibelios.
/// </summary>
public class AudioTestWindow : EditorWindow
{
    const string PrefabPath = "Assets/_Project/Resources/GameManager.prefab";

    bool walking, typingLoop;
    Vector2 scroll;

    [MenuItem("DigitatonGame/Audio Test")]
    static void Open()
    {
        var w = GetWindow<AudioTestWindow>("Audio Test");
        w.minSize = new Vector2(360f, 520f);
    }

    void OnEnable() => EditorApplication.update += Tick;
    void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (Application.isPlaying && typingLoop) AudioManager.StopTyping();
    }

    void Tick()
    {
        if (Application.isPlaying && walking) AudioManager.NotifyWalking();   // simula a Andrés caminando
        if (Application.isPlaying) Repaint();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Status();
        EditorGUILayout.Space();
        Volumes();
        EditorGUILayout.Space();
        Buttons();
        EditorGUILayout.EndScrollView();
    }

    void Status()
    {
        EditorGUILayout.LabelField("Estado", EditorStyles.boldLabel);
        bool playing = Application.isPlaying;
        bool active = playing && AudioManager.Exists;
        var old = GUI.color;
        GUI.color = active ? new Color(0.5f, 1f, 0.55f) : (playing ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.85f, 0.4f));
        EditorGUILayout.LabelField("AudioManager", active ? "ACTIVO" : (playing ? "NO EXISTE (revisa Resources/GameManager.prefab)" : "inactivo (solo existe en Play; aquí se reproduce el archivo directo)"));
        GUI.color = old;
        if (active) EditorGUILayout.LabelField("Música ambiente", AudioManager.MusicPlaying ? "sonando" : "detenida");
        EditorGUILayout.LabelField("Escena", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void Volumes()
    {
        EditorGUILayout.LabelField("Volumen (el mismo que Settings; SettingsData, 0 a 1)", EditorStyles.boldLabel);
        VolumeRow("Music", SettingsData.MusicVolume, AudioManager.MusicParam, v => SettingsData.MusicVolume = v);
        VolumeRow("SFX", SettingsData.SfxVolume, AudioManager.SfxParam, v => SettingsData.SfxVolume = v);
        EditorGUILayout.HelpBox("Curva logarítmica: 0 % = -80 dB, 100 % = 0 dB, 50 % ≈ -6 dB. \"Mixer\" es lo que realmente tiene el GameMixer ahora (solo se lee en Play).", MessageType.None);
    }

    static void VolumeRow(string label, float value, string param, Action<float> set)
    {
        EditorGUI.BeginChangeCheck();
        float v = EditorGUILayout.Slider(label, value, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) set(v);
        string mixer = "—";
        if (Application.isPlaying && AudioManager.TryGetMixerDb(param, out var db)) mixer = db.ToString("F1") + " dB";
        EditorGUILayout.LabelField("   " + Mathf.RoundToInt(v * 100f) + " %  →  esperado " + AudioManager.LinearToDb(v).ToString("F1") + " dB   |   Mixer: " + mixer);
    }

    void Buttons()
    {
        bool live = Application.isPlaying && AudioManager.Exists;
        EditorGUILayout.LabelField("Sonidos", EditorStyles.boldLabel);

        if (Row("Click (botones)", "Click", AudioManager.PlayClick)) { }
        if (Row("Navegar (flechas, más suave)", "Click", AudioManager.PlayNavigate)) { }
        if (Row("Interaction (aparece \"Press E\")", "Interaction", AudioManager.PlayInteraction)) { }
        if (Row("OpenBook (cuaderno y libro)", "OpenBook", AudioManager.PlayOpenBook)) { }
        if (Row("Correct (acierto)", "Correct", AudioManager.PlayCorrect)) { }
        if (Row("Incorrect (fallo)", "Incorrect", AudioManager.PlayIncorrect)) { }
        if (Row("WriteText: tic corto (avanzar diálogo)", "WriteText", AudioManager.PlayTextAdvance)) { }

        using (new EditorGUI.DisabledScope(!live))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bucles y música (solo en Play)", EditorStyles.boldLabel);
            walking = GUILayout.Toggle(walking, "Steps: Andrés caminando (mantiene el loop)", "Button");
            bool typing = GUILayout.Toggle(typingLoop, "WriteText: loop de letras apareciendo", "Button");
            if (typing != typingLoop)
            {
                typingLoop = typing;
                if (typing) AudioManager.StartTyping(); else AudioManager.StopTyping();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Música: empezar (fundido)")) AudioManager.StartMusic();
                if (GUILayout.Button("Música: detener (fundido)")) AudioManager.StopMusic();
            }
        }
        if (!live) EditorGUILayout.HelpBox("Dale Play (cualquier escena, incluso MainMenu o Tutorial) para probar Steps, WriteText en loop y la música con el AudioManager real.", MessageType.Info);
    }

    bool Row(string label, string clipName, Action live)
    {
        if (!GUILayout.Button(label)) return false;
        if (Application.isPlaying && AudioManager.Exists) live();
        else PlayFile(clipName);
        return true;
    }

    // ── Reproducción directa del archivo, fuera de Play ──
    static void PlayFile(string clipName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var am = prefab != null ? prefab.GetComponentInChildren<AudioManager>(true) : null;
        if (am == null) { Debug.LogWarning("Audio Test: no se encontró el AudioManager en " + PrefabPath); return; }
        var field = new SerializedObject(am).FindProperty(char.ToLowerInvariant(clipName[0]) + clipName.Substring(1));
        var clip = field != null ? field.objectReferenceValue as AudioClip : null;
        if (clip == null) { Debug.LogWarning("Audio Test: el clip '" + clipName + "' no está asignado en el AudioManager."); return; }

        var asm = typeof(AudioImporter).Assembly;
        var util = asm.GetType("UnityEditor.AudioUtil");
        var stop = util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
        var play = util?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        if (play == null) { Debug.LogWarning("Audio Test: esta versión de Unity no permite oír el clip fuera de Play."); return; }
        stop?.Invoke(null, null);
        play.Invoke(null, new object[] { clip, 0, false });
    }
}
