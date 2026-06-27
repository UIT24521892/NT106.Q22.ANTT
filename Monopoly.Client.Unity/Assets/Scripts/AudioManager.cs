using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private const string MasterKey = "audio.master";
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";
    private const string MuteKey = "audio.mute";
    private static AudioManager instance;

    // Cache clip nạp theo tên từ Resources/ để khỏi load lại nhiều lần.
    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> warnedMissingClips = new HashSet<string>();

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    public static AudioManager EnsureExists()
    {
        if (instance != null)
            return instance;

        AudioManager existing = FindObjectOfType<AudioManager>();
        if (existing != null)
            return existing;

        return new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    public float MasterVolume => PlayerPrefs.GetFloat(MasterKey, 1f);
    public float MusicVolume => PlayerPrefs.GetFloat(MusicKey, 0.8f);
    public float SfxVolume => PlayerPrefs.GetFloat(SfxKey, 1f);
    public bool IsMuted => PlayerPrefs.GetInt(MuteKey, 0) == 1;

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
    }

    public void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value));
        ApplySavedVolumes();
    }

    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        ApplySavedVolumes();
    }

    public void SetSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        ApplySavedVolumes();
    }

    public void SetMuted(bool muted)
    {
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        ApplySavedVolumes();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip)
            return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    // Overload tiện dụng: phát hiệu ứng theo tên file trong Resources/Audio/.
    // Thiếu file thì tự bỏ qua (no-op), không gây lỗi.
    public void PlaySfx(string key)
    {
        PlaySfx(LoadClip(key));
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

        PlayerPrefs.Save();
    }

    private static float ToDecibels(float value)
    {
        return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
    }
}
