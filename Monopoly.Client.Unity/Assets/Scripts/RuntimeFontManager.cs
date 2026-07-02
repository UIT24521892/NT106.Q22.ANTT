using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the project UI font to TextMeshPro objects created in scenes or at runtime.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class RuntimeFontManager : MonoBehaviour
{
    private const string FontResourcePath = "Fonts/PaytoneOne-Regular SDF";
    private const float ScanIntervalSeconds = 0.25f;

    private static TMP_FontAsset cachedFont;
    private float nextScanTime;

    public static TMP_FontAsset Font
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<TMP_FontAsset>(FontResourcePath);
            }

            return cachedFont;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<RuntimeFontManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(RuntimeFontManager));
        DontDestroyOnLoad(managerObject);
        managerObject.AddComponent<RuntimeFontManager>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyToLoadedText();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
        ApplyToLoadedText();
    }

    public static void Apply(TMP_Text text)
    {
        TMP_FontAsset font = Font;
        if (text == null || font == null)
        {
            return;
        }

        if (text.font == font)
        {
            return;
        }

        text.font = font;
        text.SetAllDirty();
    }

    private static void ApplyToLoadedText()
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Apply(texts[i]);
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToLoadedText();
    }
}
