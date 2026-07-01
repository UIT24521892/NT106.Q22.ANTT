using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    private const string MasterKey = "audio.master";
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";
    private const string MuteKey = "audio.mute";
    private const string DefaultsKey = "audio.defaults.v2";
    private const string DefaultMusicKey = "bgm";
    private static AudioManager instance;

    // Cache clip nạp theo tên từ Resources/ để khỏi load lại nhiều lần.
    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> warnedMissingClips = new HashSet<string>();
    private readonly Dictionary<string, SoundTuning> soundTunings = new Dictionary<string, SoundTuning>
    {
        { "dice", new SoundTuning(1f, 1.6f) }
    };
    private float nextButtonBindTime;
    private float masterVolume = 1f;
    private float musicVolume = 0.6f;
    private float sfxVolume = 1f;
    private bool isMuted;

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static AudioManager EnsureExists()
    {
        if (instance != null)
            return instance;

        AudioManager existing = FindObjectOfType<AudioManager>();
        if (existing != null)
            return existing;

        return new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
    public bool IsMuted => isMuted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = musicSource != null ? musicSource : gameObject.AddComponent<AudioSource>();
        sfxSource = sfxSource != null ? sfxSource : gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        ApplySavedVolumes();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[AudioManager] Ready. master={MasterVolume:0.00}, music={MusicVolume:0.00}, sfx={SfxVolume:0.00}, muted={IsMuted}.");
    }

    private void Start()
    {
        PlayMusic(DefaultMusicKey);
        BindButtonClickSounds();
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextButtonBindTime)
            return;

        nextButtonBindTime = Time.unscaledTime + 1f;
        BindButtonClickSounds();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindButtonClickSounds();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplySavedVolumes();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplySavedVolumes();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplySavedVolumes();
    }

    public void SetMuted(bool muted)
    {
        isMuted = muted;
        ApplySavedVolumes();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
            return;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.clip = clip;
        musicSource.Play();
        Debug.Log($"[AudioManager] Playing music '{clip.name}', volume={musicSource.volume:0.00}, muted={IsMuted}.");
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(clip);
        Debug.Log($"[AudioManager] Playing sfx '{clip.name}', volume={sfxSource.volume:0.00}, muted={IsMuted}.");
    }

    // Overload tiện dụng: phát hiệu ứng theo tên file trong Resources/Audio/.
    // Thiếu file thì tự bỏ qua (no-op), không gây lỗi.
    public void PlaySfx(string key)
    {
        AudioClip clip = LoadClip(key);

        if (clip == null)
            return;

        SoundTuning tuning = GetSoundTuning(key);
        float originalPitch = sfxSource.pitch;

        sfxSource.pitch = tuning.Pitch;
        sfxSource.PlayOneShot(clip, tuning.Volume);
        sfxSource.pitch = originalPitch;
        Debug.Log($"[AudioManager] Playing sfx '{clip.name}', volume={sfxSource.volume * tuning.Volume:0.00}, pitch={tuning.Pitch:0.00}, muted={IsMuted}.");
    }

    public void PlayUiClick()
    {
        PlaySfx("click");
    }

    // Phát nhạc nền theo tên file trong Resources/Audio/.
    public void PlayMusic(string key)
    {
        PlayMusic(LoadClip(key));
    }

    private AudioClip LoadClip(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (clipCache.TryGetValue(key, out AudioClip cached))
            return cached;

        AudioClip clip = Resources.Load<AudioClip>($"Audio/{key}");
        clipCache[key] = clip; // cache cả null để khỏi load lại file thiếu
        if (clip == null && warnedMissingClips.Add(key))
            Debug.LogWarning($"[AudioManager] Missing audio clip Resources/Audio/{key}. Add {key}.wav/.mp3/.ogg to enable this sound.");
        return clip;
    }

    private SoundTuning GetSoundTuning(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) && soundTunings.TryGetValue(key, out SoundTuning tuning))
            return tuning;

        return SoundTuning.Default;
    }

    private void BindButtonClickSounds()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null || button.GetComponent<AudioClickBinding>() != null)
                continue;

            button.gameObject.AddComponent<AudioClickBinding>();
            button.onClick.AddListener(PlayUiClick);
        }
    }

    private void ApplySavedVolumes()
    {
        float master = IsMuted ? 0f : MasterVolume;
        musicSource.volume = master * MusicVolume;
        sfxSource.volume = master * SfxVolume;

        if (audioMixer != null)
        {
            audioMixer.SetFloat("MasterVolume", ToDecibels(master));
            audioMixer.SetFloat("MusicVolume", ToDecibels(MusicVolume));
            audioMixer.SetFloat("SfxVolume", ToDecibels(SfxVolume));
        }

    }

    private static float ToDecibels(float value)
    {
        return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
    }
}

public class AudioClickBinding : MonoBehaviour
{
}

public readonly struct SoundTuning
{
    public static readonly SoundTuning Default = new SoundTuning(1f, 1f);

    public readonly float Volume;
    public readonly float Pitch;

    public SoundTuning(float volume, float pitch)
    {
        Volume = Mathf.Clamp01(volume);
        Pitch = Mathf.Clamp(pitch, -3f, 3f);
    }
}
