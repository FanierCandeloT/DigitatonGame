using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Agrega NPC "de ambiente" a los interiores (estudiantes, profesores, cocinera...) sin reconstruir las escenas.
/// Cada uno dice una frase corta EN PASIVA (el juego entero practica Passive Voice) al hablarle con E. Se colocan solos en
/// un hueco libre de la sala: lejos de muebles, puertas, apariciones y de otros NPC. Si ya existen (mismo nombre), se saltan.
/// Menú: DigitatonGame ▸ Agregar NPC a los interiores.
/// </summary>
public static class InteriorNpcBuilder
{
    const string Prefix = "Extra: ";
    const float Clearance = 1.6f;     // espacio libre alrededor del NPC
    const float SpawnDistance = 3.5f; // distancia mínima a una aparición o puerta
    const float NpcDistance = 4f;     // distancia mínima entre NPC

    // escena, nombre, hoja del personaje (NpcSpriteSlicer), frase en pasiva
    static readonly (string scene, string npc, int sheet, string line)[] Npcs =
    {
        ("Aulas1", "Estudiante A", 3, "Student: The board is cleaned every morning by Don Jairo."),
        ("Aulas1", "Estudiante B", 10, "Student: My notes were lost... By whom? Nobody knows!"),
        ("Aulas3", "Profesora", 15, "Teacher: Your essays have been graded by me. They will be returned tomorrow."),
        ("Aulas3", "Estudiante C", 19, "Student: Plastic cups are not allowed in this classroom."),
        ("Aulas4", "Estudiante D", 21, "Student: This room was painted by volunteers last year."),
        ("Aulas4", "Estudiante E", 22, "Student: A new lab is being built by the engineering students."),
        ("Bienestar", "Paciente", 1, "Patient: I was brought here by a friend. I am being treated very well."),
        ("Bienestar", "Auxiliar", 13, "Assistant: Water must be drunk every hour. That is what is recommended by doctors."),
        ("Cafeteria", "Comensal A", 3, "Diner: Lunch is being eaten by me. Please do not disturb."),
        ("Cafeteria", "Comensal B", 15, "Diner: The leftovers are given to a shelter every Friday."),
        ("Cafeteria", "Cocinero", 19, "Cook: The soup was made by me, and the bottles are recycled by the whole team."),
        ("SalaProfesores", "Profesor A", 10, "Teacher: Energy is saved when the lights are turned off."),
        ("MuseoLili", "Guía", 25, "Guide: The paintings are cleaned every week. Photos may not be taken."),
    };

    [MenuItem("DigitatonGame/Agregar NPC a los interiores")]
    public static void AddAll()
    {
        CampusBuilder.EnsureSprites();
        if (NpcSpriteSlicer.Load(1).Length < 12) NpcSpriteSlicer.SliceAll();

        int added = 0, skipped = 0;
        var scenes = new List<string>();
        foreach (var n in Npcs) if (!scenes.Contains(n.scene)) scenes.Add(n.scene);

        foreach (var sceneName in scenes)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{sceneName}.unity", OpenSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            Physics2D.SyncTransforms();

            var placed = new List<Vector2>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var toast in root.GetComponentsInChildren<ToastInteractable>(true))
                    placed.Add(toast.transform.position);

            foreach (var (s, name, sheet, line) in Npcs)
            {
                if (s != sceneName) continue;
                if (Exists(scene, Prefix + name)) { skipped++; continue; }

                if (!TryFindSpot(scene, placed, out var pos))
                {
                    Debug.LogWarning($"{sceneName}: no hay hueco libre para '{name}'.");
                    continue;
                }

                var go = InteriorsBuilder.Npc(Prefix + name, pos, line, "Sprite de un personaje de ambiente (NPC, vista 3/4, 20x32 px).");
                go.GetComponent<ToastInteractable>().prompt = "Press E to talk";
                NpcSpriteSetup.Apply(go, sheet);
                placed.Add(pos);
                added++;
            }

            SceneManager.SetActiveScene(previous);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log($"NPC de interiores: {added} agregados, {skipped} ya existían.");
    }

    static bool Exists(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return true;
        return false;
    }

    /// <summary>Busca en una cuadrícula un punto dentro de la sala sin colisionadores cerca, lejos de puertas, apariciones y NPC.</summary>
    static bool TryFindSpot(Scene scene, List<Vector2> placed, out Vector2 spot)
    {
        var bounds = new Bounds();
        var avoid = new List<Vector2>();
        foreach (var root in scene.GetRootGameObjects())
        {
            var map = root.GetComponentInChildren<MapBounds>(true);
            if (map != null && map.TryGetComponent(out Collider2D col)) bounds = col.bounds;
            foreach (var sp in root.GetComponentsInChildren<SpawnPoint>(true)) avoid.Add(sp.transform.position);
            foreach (var door in root.GetComponentsInChildren<Door>(true)) avoid.Add(door.transform.position);
        }

        // Las posiciones se recorren en un orden repartido (no de una esquina a la otra) para que los NPC no queden juntos
        var candidates = new List<Vector2>();
        for (float x = bounds.min.x + 2.5f; x <= bounds.max.x - 2.5f; x += 1.5f)
            for (float y = bounds.min.y + 2.5f; y <= bounds.max.y - 3f; y += 1.5f)
                candidates.Add(new Vector2(x, y));

        // En salas pequeñas se relajan las distancias (primero holgado, luego a la mitad) para que quepan todos
        int step = Mathf.Max(1, candidates.Count / 7);
        foreach (float relax in new[] { 1f, 0.6f })
        {
            for (int offset = 0; offset < step; offset++)
            {
                for (int i = offset; i < candidates.Count; i += step)
                {
                    if (!IsFree(candidates[i], avoid, placed, relax)) continue;
                    spot = candidates[i];
                    return true;
                }
            }
        }
        spot = default;
        return false;
    }

    static bool IsFree(Vector2 c, List<Vector2> avoid, List<Vector2> placed, float relax)
    {
        // El colisionador de MapBounds cubre toda la sala: no cuenta como obstáculo
        foreach (var hit in Physics2D.OverlapBoxAll(c, Vector2.one * Clearance, 0f))
            if (hit.GetComponent<MapBounds>() == null) return false;
        foreach (var p in avoid) if (Vector2.Distance(p, c) < SpawnDistance * relax) return false;
        foreach (var p in placed) if (Vector2.Distance(p, c) < NpcDistance * relax) return false;
        return true;
    }
}
