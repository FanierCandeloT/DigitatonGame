using System;
using UnityEngine;

public enum TextSpeed { Slow = 0, Normal = 1, Fast = 2 }

/// <summary>
/// Opciones del jugador guardadas con PlayerPrefs (se mantienen al cerrar el juego).
/// El audio todavía no existe: cuando haya, debe leer MusicVolume y SfxVolume de aquí.
/// </summary>
public static class SettingsData
{
    const string MusicKey = "settings.musicVolume";
    const string SfxKey = "settings.sfxVolume";
    const string FullscreenKey = "settings.fullscreen";
    const string TextSpeedKey = "settings.textSpeed";

    /// <summary>Se dispara cuando cambia cualquier opción.</summary>
    public static event Action Changed;

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Save(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
        set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); Save(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 0) == 1;
        set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); ApplyScreen(); Save(); }
    }

    public static TextSpeed TextSpeed
    {
        get => (TextSpeed)Mathf.Clamp(PlayerPrefs.GetInt(TextSpeedKey, (int)TextSpeed.Normal), 0, 2);
        set { PlayerPrefs.SetInt(TextSpeedKey, (int)value); Save(); }
    }

    /// <summary>Caracteres por segundo para el texto con efecto de escritura.</summary>
    public static float CharsPerSecond
    {
        get
        {
            switch (TextSpeed)
            {
                case TextSpeed.Slow: return 20f;
                case TextSpeed.Fast: return 90f;
                default: return 45f;
            }
        }
    }

    public static void ApplyAll()
    {
        MigrateVolumes();
        ApplyScreen();
    }

    const string VolumesFixedKey = "settings.volumesFixed1";

    /// <summary>
    /// Una sola vez: restablece los volúmenes a 80 %. El bug de los sliders (rango 0..100 contra valores 0..1) dejaba guardados
    /// 0 o 100 %, y con 0 no se oía nada. Después los volúmenes ya son siempre 0..1.
    /// </summary>
    public static void MigrateVolumes()
    {
        if (PlayerPrefs.GetInt(VolumesFixedKey, 0) == 1) return;
        PlayerPrefs.SetFloat(MusicKey, 0.8f);
        PlayerPrefs.SetFloat(SfxKey, 0.8f);
        PlayerPrefs.SetInt(VolumesFixedKey, 1);
        PlayerPrefs.Save();
    }

    static void ApplyScreen()
    {
        Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
