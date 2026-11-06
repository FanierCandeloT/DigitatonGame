using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Genera los clips y el AnimatorController de Andrés a partir de la hoja de 32 sprites
/// y los conecta al prefab. Menú: Tools > Andres > Configurar animaciones.
/// Es seguro volver a ejecutarlo: regenera los assets y deja el prefab con un solo Animator.
/// </summary>
public static class AndresAnimationSetup
{
    const string SheetPath = "Assets/_Project/Art/Sprite Andres/Sprite Andres.png";
    const string PrefabPath = "Assets/_Project/Prefabs/Andres.prefab";
    const string OutDir = "Assets/_Project/Animations/Andres";

    // nombre, primer sprite, cantidad de cuadros, fps
    static readonly (string name, int start, int count, float fps)[] Clips =
    {
        ("Idle_Down", 0, 4, 4), ("Idle_Up", 4, 4, 4),
        ("Walk_Down", 8, 4, 8), ("Walk_Up", 12, 4, 8),
        ("Walk_Left", 16, 4, 8), ("Walk_Right", 20, 4, 8),
        ("Talk", 24, 4, 6), ("Think", 28, 4, 4),
        // La hoja no trae idle lateral: se usa el primer cuadro de caminar.
        ("Idle_Left", 16, 1, 1), ("Idle_Right", 20, 1, 1),
    };

    [MenuItem("Tools/Andres/Configurar animaciones")]
    public static void Run()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
        if (sprites.Length != 32)
        {
            Debug.LogError($"[Andres] Se esperaban 32 sprites en '{SheetPath}' y hay {sprites.Length}.");
            return;
        }

        var controller = BuildController(sprites);
        WirePrefab(controller, sprites[0]);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Andres] Animaciones creadas en {OutDir} y conectadas a {PrefabPath}.");
    }

    static AnimatorController BuildController(Sprite[] sprites)
    {
        Directory.CreateDirectory(OutDir);
        AssetDatabase.DeleteAsset(OutDir + "/Andres.controller");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutDir + "/Andres.controller");
        var machine = controller.layers[0].stateMachine;

        foreach (var def in Clips)
        {
            var clip = CreateClip(def.name, def.fps, sprites.Skip(def.start).Take(def.count).ToArray());
            var state = machine.AddState(def.name);
            state.motion = clip;
            if (def.name == "Idle_Down") machine.defaultState = state;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip CreateClip(string name, float fps, Sprite[] frames)
    {
        var clip = new AnimationClip { frameRate = fps };
        var keys = frames
            .Select((sprite, i) => new ObjectReferenceKeyframe { time = i / fps, value = sprite })
            .ToArray();
        AnimationUtility.SetObjectReferenceCurve(
            clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var path = $"{OutDir}/Andres_{name}.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void WirePrefab(AnimatorController controller, Sprite idleFrame)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // No usar '??': los componentes de Unity sobrecargan == y '??' lo ignora.
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            if (root.GetComponent<AndresAnimator>() == null)
                root.AddComponent<AndresAnimator>();

            // Con Sliced el sprite se estiraría a 1x2 en cada cuadro; Simple respeta el tamaño real.
            var sr = root.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sprite = idleFrame;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
