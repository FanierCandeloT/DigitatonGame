using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Configura el audio del juego (sin menús, se llama desde código y es repetible):
///  1) importación de cada clip según su uso, 2) AudioMixer con los grupos Music y SFX (volúmenes expuestos),
///  3) alta del AudioManager dentro del prefab del GameManager.
/// </summary>
public static class AudioSetup
{
    const string Dir = "Assets/_Project/Audio";
    const string MixerPath = Dir + "/GameMixer.mixer";
    const string PrefabPath = "Assets/_Project/Resources/GameManager.prefab";

    // ───────────────────────── 1. Importación ─────────────────────────

    public static string ImportClips()
    {
        var log = new List<string>();

        // Música: comprimida en memoria (no se descomprime entera) y carga en segundo plano. El loop lo hace el AudioSource.
        Configure("Ambient", AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.6f, mono: false, preload: false, background: true, log);

        // Efectos cortos: descomprimidos al cargar y precargados, así suenan al instante sin retraso.
        foreach (var n in new[] { "Click", "Correct", "Incorrect", "Interaction", "OpenBook" })
            Configure(n, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.ADPCM, 1f, mono: true, preload: true, background: false, log);

        // Pasos: es un loop largo; comprimido en memoria y en mono, precargado.
        Configure("Steps", AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.7f, mono: true, preload: true, background: false, log);

        return string.Join(" | ", log);
    }

    static void Configure(string name, AudioClipLoadType load, AudioCompressionFormat format, float quality, bool mono, bool preload, bool background, List<string> log)
    {
        var path = $"{Dir}/{name}.mp3";
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) { log.Add($"{name}: no existe"); return; }

        var settings = importer.defaultSampleSettings;
        settings.loadType = load;
        settings.compressionFormat = format;
        settings.quality = quality;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = preload;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = mono;
        importer.loadInBackground = background;
        importer.SaveAndReimport();

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        log.Add($"{name}: {load}, {format}, {clip.length:F1}s");
    }

    // ───────────────────────── 2. AudioMixer ─────────────────────────

    public static string CreateMixer()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null) return "ya existe " + MixerPath;

        // Las clases del editor de mezcladores son internas: se usan por reflexión (este código solo corre en el Editor).
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var controllerType = FindType("UnityEditor.Audio.AudioMixerController");
        var groupType = FindType("UnityEditor.Audio.AudioMixerGroupController");
        var paramType = FindType("UnityEditor.Audio.ExposedAudioParameter");

        var controller = controllerType.GetMethod("CreateMixerControllerAtPath", All).Invoke(null, new object[] { MixerPath });
        var createGroup = controllerType.GetMethod("CreateNewGroup", All, null, new[] { typeof(string), typeof(bool) }, null);
        var addChild = controllerType.GetMethod("AddChildToParent", All);
        var master = controllerType.GetProperty("masterGroup", All).GetValue(controller);

        var music = createGroup.Invoke(controller, new object[] { "Music", true });
        var sfx = createGroup.Invoke(controller, new object[] { "SFX", true });
        addChild.Invoke(controller, new[] { music, master });
        addChild.Invoke(controller, new[] { sfx, master });

        // Expone el volumen de cada grupo para poder cambiarlo por código (AudioMixer.SetFloat)
        var getGuid = groupType.GetMethod("GetGUIDForVolume", All);
        var exposed = System.Array.CreateInstance(paramType, 2);
        exposed.SetValue(MakeParam(paramType, getGuid.Invoke(music, null), AudioManager.MusicParam), 0);
        exposed.SetValue(MakeParam(paramType, getGuid.Invoke(sfx, null), AudioManager.SfxParam), 1);
        controllerType.GetProperty("exposedParameters", All).SetValue(controller, exposed);

        EditorUtility.SetDirty((Object)controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
        return "mixer creado con grupos Music y SFX";
    }

    static System.Type FindType(string fullName) =>
        System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .First(t => t.FullName == fullName);

    static object MakeParam(System.Type paramType, object guid, string name)
    {
        var p = System.Activator.CreateInstance(paramType);
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var guidField = paramType.GetField("guid", F);
        var nameField = paramType.GetField("name", F);
        if (guidField != null) guidField.SetValue(p, guid); else paramType.GetProperty("guid", F).SetValue(p, guid);
        if (nameField != null) nameField.SetValue(p, name); else paramType.GetProperty("name", F).SetValue(p, name);
        return p;
    }

    // ───────────────────────── 3. AudioManager en el prefab ─────────────────────────

    public static string AddToGameManager()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var groups = AssetDatabase.LoadAllAssetsAtPath(MixerPath).OfType<AudioMixerGroup>().ToList();
        var music = groups.FirstOrDefault(g => g.name == "Music");
        var sfx = groups.FirstOrDefault(g => g.name == "SFX");
        if (mixer == null || music == null || sfx == null) return "falta el mixer o sus grupos";

        AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/{n}.mp3");

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var am = root.GetComponentInChildren<AudioManager>(true);
        if (am == null)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(root.transform, false);
            am = go.AddComponent<AudioManager>();
        }

        var so = new SerializedObject(am);
        void Set(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
        Set("mixer", mixer);
        Set("musicGroup", music);
        Set("sfxGroup", sfx);
        Set("ambient", Clip("Ambient"));
        Set("click", Clip("Click"));
        Set("interaction", Clip("Interaction"));
        Set("openBook", Clip("OpenBook"));
        Set("correct", Clip("Correct"));
        Set("incorrect", Clip("Incorrect"));
        Set("steps", Clip("Steps"));
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return "AudioManager agregado al prefab del GameManager";
    }
}
