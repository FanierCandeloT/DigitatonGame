using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sistema de audio del juego. Vive dentro del prefab del GameManager (no se destruye al cambiar de escena).
///  - Música (grupo Music): Ambient en loop con fundidos. Empieza al presionar Play, sigue entre escenas y se detiene al volver al menú.
///  - Efectos (grupo SFX): Click, Interaction, OpenBook, Correct, Incorrect y los pasos (loop mientras Andrés camina).
/// Los sliders de Settings (SettingsData.MusicVolume / SfxVolume) controlan el volumen de cada grupo en el AudioMixer, al instante.
/// Todos los métodos públicos son estáticos y seguros: si el AudioManager no existe, simplemente no hacen nada.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public const string MusicParam = "MusicVolume";
    public const string SfxParam = "SfxVolume";

    static AudioManager instance;

    [Header("Mezclador")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Audios")]
    [SerializeField] AudioClip ambient;
    [SerializeField] AudioClip click;
    [SerializeField] AudioClip interaction;
    [SerializeField] AudioClip openBook;
    [SerializeField] AudioClip correct;
    [SerializeField] AudioClip incorrect;
    [SerializeField] AudioClip steps;
    [SerializeField] AudioClip writeText;

    [Header("Niveles (relativos; el volumen final lo ponen los sliders de Settings)")]
    [SerializeField, Range(0f, 1f)] float ambientLevel = 0.7f;
    [SerializeField, Range(0f, 1f)] float stepsLevel = 0.6f;
    [SerializeField, Range(0f, 1f)] float writeTextLevel = 0.55f;
    [SerializeField, Min(0.05f)] float writeTextShortSeconds = 0.14f;   // duración del "tic" al avanzar un diálogo
    [SerializeField, Min(0.1f)] float musicFadeIn = 2f;
    [SerializeField, Min(0.1f)] float musicFadeOut = 1.5f;

    const int SfxPoolSize = 6;
    AudioSource musicSource, stepsSource, textSource;
    AudioSource[] sfxPool;
    int sfxNext;
    readonly System.Collections.Generic.Dictionary<AudioClip, Vector2> trims = new System.Collections.Generic.Dictionary<AudioClip, Vector2>();
    GameObject lastSelected;
    bool warnedMixer;
    bool typing;               // hay un texto escribiéndose (loop de WriteText)
    bool textPausedByUs;       // el loop está en pausa porque el juego está en pausa
    Coroutine musicFade;
    int lastWalkFrame = -10;
    float lastPreviewTime = -10f;
    Selectable[] selectableBuffer = new Selectable[64];

    static readonly string[] SilentScenes = { "MainMenu", "Tutorial" };

    // ───────────── API pública (la que usa el resto del juego) ─────────────

    /// <summary>Clic de botón. Ya suena solo en TODOS los botones; llamarlo a mano solo hace falta para elementos que no son Button.</summary>
    public static void PlayClick() => instance?.PlaySfx(instance.click);

    /// <summary>Respuesta correcta de un minijuego / reto.</summary>
    public static void PlayCorrect() => instance?.PlaySfx(instance.correct);

    /// <summary>Respuesta incorrecta de un minijuego / reto.</summary>
    public static void PlayIncorrect() => instance?.PlaySfx(instance.incorrect);

    /// <summary>Andrés se acerca a algo interactivo ("Press E").</summary>
    public static void PlayInteraction() => instance?.PlaySfx(instance.interaction);

    /// <summary>Abrir o cerrar el Cuaderno del profe.</summary>
    public static void PlayOpenBook() => instance?.PlaySfx(instance.openBook);

    /// <summary>Empieza la música ambiente con fundido de entrada (si ya suena, no hace nada).</summary>
    public static void StartMusic() => instance?.BeginMusic();

    /// <summary>Detiene la música ambiente con fundido de salida.</summary>
    public static void StopMusic() => instance?.EndMusic();

    /// <summary>Lo llama PlayerController cada frame que Andrés realmente camina; si deja de llamarse, los pasos se detienen.</summary>
    public static void NotifyWalking()
    {
        if (instance != null) instance.lastWalkFrame = Time.frameCount;
    }

    // ── Texto de diálogos (WriteText) ──

    /// <summary>Empieza el loop de WriteText (letras apareciendo). Si ya sonaba, lo reinicia. Se pausa solo con el juego.</summary>
    public static void StartTyping() => instance?.BeginTyping();

    /// <summary>Detiene el loop de WriteText en el acto (la línea ya apareció completa o el diálogo se cerró). No corta el "tic" corto.</summary>
    public static void StopTyping() => instance?.EndTyping();

    /// <summary>El jugador avanzó o saltó una línea: un "tic" corto de WriteText. Cada clic lo reinicia (nunca se acumula).</summary>
    public static void PlayTextAdvance() => instance?.PlayTextShort();

    /// <summary>Vista previa del volumen de efectos (al soltar el slider o ajustarlo con el teclado): un clic, con límite de frecuencia.</summary>
    public static void PreviewSfx()
    {
        if (instance == null || Time.unscaledTime - instance.lastPreviewTime < 0.12f) return;
        instance.lastPreviewTime = Time.unscaledTime;
        PlayClick();
    }

    /// <summary>Clic suave al moverse entre botones con el teclado (ya suena solo; no hace falta llamarlo).</summary>
    public static void PlayNavigate() => instance?.PlaySfx(instance.click, 0.4f, 1.25f);

    // ── Diagnóstico (lo usa la ventana DigitatonGame ▸ Audio Test) ──

    /// <summary>True si el AudioManager existe (en Play, siempre, en cualquier escena).</summary>
    public static bool Exists => instance != null;

    /// <summary>True si la música ambiente está sonando.</summary>
    public static bool MusicPlaying => instance != null && instance.musicSource != null && instance.musicSource.isPlaying;

    /// <summary>Volumen actual (en dB) de un parámetro expuesto del GameMixer, o false si no se puede leer.</summary>
    public static bool TryGetMixerDb(string param, out float db)
    {
        db = 0f;
        return instance != null && instance.mixer != null && instance.mixer.GetFloat(param, out db);
    }

    /// <summary>Convierte el valor 0..1 de un slider a decibelios: 0 % = -80 dB, 100 % = 0 dB, con curva logarítmica.</summary>
    public static float LinearToDb(float linear) => ToDecibels(linear);

    // ───────────── Ciclo de vida ─────────────

    void Awake()
    {
        // Una sola copia: si por cualquier motivo aparece otra, se destruye ENTERA (con sus AudioSources), no solo el componente
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        SettingsData.MigrateVolumes();   // los volúmenes que dejó el bug de los sliders (0..100) se restablecen una sola vez
        EnsureSources();
        ApplyVolumes();
    }

    /// <summary>Crea los AudioSources que falten (también los vuelve a crear si algo los destruyó): el sonido no depende de que sigan vivos.</summary>
    void EnsureSources()
    {
        if (musicSource == null) { musicSource = NewSource("Música", musicGroup, loop: true, ambient); musicSource.volume = 0f; }
        if (sfxPool == null || sfxPool.Length != SfxPoolSize) sfxPool = new AudioSource[SfxPoolSize];
        for (int i = 0; i < SfxPoolSize; i++) if (sfxPool[i] == null) sfxPool[i] = NewSource("Efecto " + i, sfxGroup, loop: false, null);
        if (stepsSource == null) { stepsSource = NewSource("Pasos", sfxGroup, loop: true, steps); stepsSource.volume = 0f; }
        if (textSource == null) { textSource = NewSource("Texto de diálogos", sfxGroup, loop: true, writeText); textSource.volume = writeTextLevel; }   // un solo canal: el "tic" y el loop nunca se superponen
    }

    AudioSource NewSource(string sourceName, AudioMixerGroup group, bool loop, AudioClip clip)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.clip = clip;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = group;
        src.ignoreListenerPause = true;
        return src;
    }

    void OnEnable()
    {
        SettingsData.Changed += ApplyVolumes;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SettingsData.Changed -= ApplyVolumes;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        ApplyVolumes();   // algunos mezcladores solo aceptan valores a partir de Start

        // Comodidad para desarrollo: si se da Play directo en una escena del juego (no en el menú), la música suena igual.
        var current = SceneManager.GetActiveScene().name;
        if ((Application.isEditor || Debug.isDebugBuild) && System.Array.IndexOf(SilentScenes, current) < 0) BeginMusic();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // En CADA carga de escena (no solo la primera): el audio queda en un estado limpio y los botones nuevos se conectan.
        Time.timeScale = GameManager.Exists && GameManager.Instance.IsPaused ? Time.timeScale : 1f;
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        EnsureSources();
        ResetSnapshot();
        ApplyVolumes();
        lastSelected = null;
        ConnectSelectables();

        // Sin música en el menú ni en el tutorial: al volver al menú se apaga con fundido.
        if (System.Array.IndexOf(SilentScenes, scene.name) >= 0) EndMusic();
    }

    // ───────────── Volumen (sliders de Settings → AudioMixer) ─────────────

    static float ToDecibels(float linear) => linear <= 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear), -80f, 0f);

    /// <summary>Deja activo el snapshot por defecto del GameMixer (ninguno debe quedar con el volumen bajo).</summary>
    void ResetSnapshot()
    {
        if (mixer == null) return;
        var snapshot = mixer.FindSnapshot("Snapshot");
        if (snapshot != null) snapshot.TransitionTo(0f);
    }

    void ApplyVolumes()
    {
        if (mixer == null) return;
        bool a = mixer.SetFloat(MusicParam, ToDecibels(SettingsData.MusicVolume));
        bool b = mixer.SetFloat(SfxParam, ToDecibels(SettingsData.SfxVolume));
        if ((!a || !b) && !warnedMixer)
        {
            warnedMixer = true;   // el GameMixer debe exponer "MusicVolume" y "SfxVolume" (AudioSetup lo crea así)
            Debug.LogWarning("AudioManager: el GameMixer no tiene expuestos los parámetros MusicVolume / SfxVolume; los sliders no cambiarán el volumen.", this);
        }
    }

    // ───────────── Música ─────────────

    void BeginMusic()
    {
        if (ambient == null || musicSource == null) return;
        if (!musicSource.isPlaying) musicSource.Play();
        StartFade(ambientLevel, musicFadeIn, stopAtEnd: false);
    }

    void EndMusic()
    {
        if (musicSource == null || !musicSource.isPlaying) return;
        StartFade(0f, musicFadeOut, stopAtEnd: true);
    }

    void StartFade(float target, float seconds, bool stopAtEnd)
    {
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(target, seconds, stopAtEnd));
    }

    IEnumerator FadeMusic(float target, float seconds, bool stopAtEnd)
    {
        float start = musicSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        musicSource.volume = target;
        if (stopAtEnd) musicSource.Stop();
        musicFade = null;
    }

    // ───────────── Efectos ─────────────

    /// <summary>
    /// Efecto corto por un canal libre del grupo SFX. Salta el silencio del inicio del archivo (OpenBook trae 0,26 s,
    /// WriteText 0,14 s...) y corta la cola silenciosa (Interaction dura 8 s pero solo suena 0,3 s), así el sonido llega
    /// justo cuando pasa la acción.
    /// </summary>
    void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || sfxPool == null) return;
        var trim = Trim(clip);
        var src = sfxPool[sfxNext];
        sfxNext = (sfxNext + 1) % sfxPool.Length;
        src.Stop();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.time = Mathf.Min(trim.x, clip.length * 0.95f);
        src.Play();
        src.SetScheduledEndTime(AudioSettings.dspTime + Mathf.Max(0.05f, (trim.y - trim.x) / pitch));
    }

    /// <summary>(inicio, fin) audibles del clip en segundos. Los clips comprimidos o muy largos se tratan como audibles completos.</summary>
    Vector2 Trim(AudioClip clip)
    {
        if (trims.TryGetValue(clip, out var cached)) return cached;
        var result = new Vector2(0f, clip.length);
        if (clip.length <= 20f && clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.loadState == AudioDataLoadState.Loaded)
        {
            var data = new float[clip.samples * clip.channels];
            if (clip.GetData(data, 0))
            {
                const float threshold = 0.015f;
                int first = -1, last = -1;
                for (int i = 0; i < data.Length; i++)
                    if (Mathf.Abs(data[i]) > threshold) { if (first < 0) first = i; last = i; }
                if (first >= 0)
                {
                    float perSecond = clip.channels * (float)clip.frequency;
                    result = new Vector2(Mathf.Max(0f, first / perSecond - 0.005f), Mathf.Min(clip.length, last / perSecond + 0.06f));
                }
            }
        }
        trims[clip] = result;
        return result;
    }

    /// <summary>Segundo del archivo donde empieza el sonido de verdad (salta el silencio inicial).</summary>
    float LeadIn(AudioClip clip) => clip == null ? 0f : Trim(clip).x;

    void Update()
    {
        EnsureSources();
        UpdateSteps();
        UpdateText();
    }

    // WriteText: loop mientras las letras aparecen. Pausa del juego = pausa del sonido (y continúa al reanudar).
    void BeginTyping()
    {
        if (textSource == null || writeText == null) return;
        typing = true;
        textPausedByUs = false;
        textSource.Stop();
        textSource.loop = true;
        textSource.volume = writeTextLevel;
        textSource.pitch = 1f + Random.Range(-0.03f, 0.03f);
        textSource.time = LeadIn(writeText);   // WriteText.mp3 trae 0,14 s de silencio al inicio: el "tic" corto sonaba mudo
        textSource.Play();
    }

    void EndTyping()
    {
        typing = false;
        textPausedByUs = false;
        // Solo corta el loop; si está sonando el "tic" corto (loop apagado) lo deja terminar.
        if (textSource != null && textSource.loop) textSource.Stop();
    }

    void PlayTextShort()
    {
        if (textSource == null || writeText == null) return;
        typing = false;
        textPausedByUs = false;
        textSource.Stop();              // cada clic reinicia el sonido corto
        textSource.loop = false;
        textSource.volume = writeTextLevel;
        float pitch = 1f + Random.Range(-0.04f, 0.04f);
        textSource.pitch = pitch;
        textSource.time = LeadIn(writeText);   // WriteText.mp3 trae 0,14 s de silencio al inicio: el "tic" corto sonaba mudo
        textSource.Play();
        textSource.SetScheduledEndTime(AudioSettings.dspTime + writeTextShortSeconds / pitch);
    }

    void UpdateText()
    {
        if (textSource == null || !typing) return;

        bool paused = GameManager.Exists && GameManager.Instance.IsPaused;
        if (paused && textSource.isPlaying) { textSource.Pause(); textPausedByUs = true; }
        else if (!paused && textPausedByUs) { textSource.UnPause(); textPausedByUs = false; }

        if (textSource.isPlaying)   // el tono deriva muy poco, para que no suene repetitivo
            textSource.pitch = 1f + (Mathf.PerlinNoise(Time.unscaledTime * 1.3f, 9.1f) - 0.5f) * 0.07f;
    }

    void LateUpdate()
    {
        AddClickToNewButtons();
        NavigationClick();
    }

    // Al moverse entre botones con las flechas del teclado: clic más suave (con el mouse el resaltado no suena, para no saturar).
    void NavigationClick()
    {
        var es = EventSystem.current;
        var selected = es != null ? es.currentSelectedGameObject : null;
        if (selected == lastSelected) return;
        var previous = lastSelected;
        lastSelected = selected;
        if (previous == null || selected == null) return;

        var kb = Keyboard.current;
        bool arrow = kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
                                    || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame);
        if (arrow && selected.GetComponent<Selectable>() != null) PlayNavigate();
    }

    // Pasos: loop mientras Andrés camina (lo avisa PlayerController cada frame); el tono varía suavemente.
    void UpdateSteps()
    {
        if (stepsSource == null || steps == null) return;

        bool walking = Time.frameCount - lastWalkFrame <= 1;
        float targetVolume = walking ? stepsLevel : 0f;
        stepsSource.volume = Mathf.MoveTowards(stepsSource.volume, targetVolume, Time.unscaledDeltaTime * 12f);

        if (walking)
        {
            if (!stepsSource.isPlaying)
            {
                stepsSource.time = Random.Range(0f, steps.length * 0.9f);   // arranca en un punto distinto cada vez
                stepsSource.Play();
            }
            stepsSource.pitch = 1f + (Mathf.PerlinNoise(Time.unscaledTime * 0.9f, 3.7f) - 0.5f) * 0.14f;   // ±7 %
        }
        else if (stepsSource.isPlaying && stepsSource.volume <= 0.001f)
        {
            stepsSource.Stop();
        }
    }

    // Clic automático: cualquier Button que aparezca (menús, pausa, minijuegos, botones nuevos) recibe el sonido sin configurarlo.
    void AddClickToNewButtons() => ConnectSelectables();

    void ConnectSelectables()
    {
        int n = Selectable.allSelectableCount;
        if (selectableBuffer.Length < n) selectableBuffer = new Selectable[n + 32];
        Selectable.AllSelectablesNoAlloc(selectableBuffer);

        for (int i = 0; i < n; i++)
        {
            var selectable = selectableBuffer[i];
            if (selectable == null || selectable.TryGetComponent(out ButtonClickSound _)) continue;
            if (selectable is Button button)
            {
                button.gameObject.AddComponent<ButtonClickSound>();
                button.onClick.AddListener(PlayClick);
            }
            else if (selectable is Toggle toggle)   // checkbox de Fullscreen y cualquier otro Toggle
            {
                toggle.gameObject.AddComponent<ButtonClickSound>();
                toggle.onValueChanged.AddListener(_ => PlayClick());
            }
        }
    }
}

/// <summary>Marca de que el botón ya tiene el sonido de clic (así no se agrega dos veces).</summary>
public class ButtonClickSound : MonoBehaviour { }
