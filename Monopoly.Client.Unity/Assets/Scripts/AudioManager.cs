using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private const string MasterKey = "audio.master";
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";
    private const string MuteKey = "audio.mute";
    private static AudioManager instance;

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
