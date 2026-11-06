using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Cuaderno del profe: se abre y cierra con C dentro del juego. Por ahora es solo un panel vacío
/// que dice "Professor's Notebook". Mientras está abierto Andrés no se mueve.
/// </summary>
public class NotebookPanel : MonoBehaviour
{
    public GameObject panel;

    public static event Action Opened;
    public static event Action Closed;

    public bool IsOpen => panel != null && panel.activeSelf;

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Close();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.cKey.wasPressedThisFrame) return;

        // Suena cada vez que se presiona C para abrir o cerrar el cuaderno
        if (IsOpen) { Close(); AudioManager.PlayOpenBook(); }
        else if (CanOpen()) { Open(); AudioManager.PlayOpenBook(); }
    }

    static bool CanOpen()
    {
        var gm = GameManager.Instance;
        // Solo dentro del juego: hace falta que haya jugador, y no en pausa ni en un fundido.
        return !gm.InputLocked && FindAnyObjectByType<PlayerController>() != null;
    }

    public void Open()
    {
        if (IsOpen) return;
        panel.SetActive(true);
        GameManager.Instance.NotebookOpen = true;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        GameManager.Instance.NotebookOpen = false;
        Closed?.Invoke();
    }
}
