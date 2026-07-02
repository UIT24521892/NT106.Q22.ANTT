using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
    private const string LoadingSceneName = "LoadingScene";
    private const string TargetSceneName = "LobbyScene";
    private const float LoadingSeconds = 5f;
    private static readonly Color Cyan = new Color(0.05f, 0.92f, 1f, 1f);

    private Image progressFill;
    private TextMeshProUGUI percentText;
    private TextMeshProUGUI statusText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForActiveScene();
    }

    private static void TryCreateForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != LoadingSceneName)
            return;

        if (FindObjectOfType<LoadingSceneController>() != null)
            return;

        new GameObject("LoadingSceneController").AddComponent<LoadingSceneController>();
    }

    private void Start()
    {
        BuildUi();
        StartCoroutine(LoadAfterDelay());
    }

    private IEnumerator LoadAfterDelay()
    {
        float elapsed = 0f;

        while (elapsed < LoadingSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetProgress(Mathf.Clamp01(elapsed / LoadingSeconds));
            yield return null;
        }

        SetProgress(1f);
        SceneManager.LoadScene(TargetSceneName);
    }

    private void SetProgress(float value)
    {
        if (progressFill != null)
            SetProgressFillRect(value);

        if (percentText != null)
            percentText.text = $"{Mathf.RoundToInt(value * 100f)}%";

        if (statusText != null)
        {
            int dots = 4 + Mathf.FloorToInt(Time.unscaledTime * 5f) % 7;
            statusText.text = "LOADING " + new string('.', dots).Replace(".", ". ");
        }
    }

    private void SetProgressFillRect(float value)
    {
        if (progressFill == null)
            return;

        RectTransform rect = progressFill.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void BuildUi()
    {
        Canvas canvas = CreateCanvas();

        Image background = CreateImage(canvas.transform, "Img_LoadingBackground", Color.white);
        background.sprite = LoadResourceSprite("background");
        background.preserveAspect = false;
        SetStretch(background.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI titleShadow = CreateText(canvas.transform, "Txt_LoadingTitleShadow", "MONOPOLY", 112f, FontStyles.Bold, new Color(0.04f, 0.16f, 0.28f, 0.72f));
        ConfigureTitleText(titleShadow);
        titleShadow.outlineWidth = 0f;
        SetRect(titleShadow.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(7f, -185f), new Vector2(-70f, 140f));

        TextMeshProUGUI title = CreateText(canvas.transform, "Txt_LoadingTitle", "MONOPOLY", 112f, FontStyles.Bold, new Color(1f, 0.66f, 0.02f, 1f));
        ConfigureTitleText(title);
        title.outlineWidth = 0.16f;
        title.outlineColor = new Color(0.05f, 0.18f, 0.3f, 1f);
        SetRect(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -188f), new Vector2(-70f, 140f));

        statusText = CreateText(canvas.transform, "Txt_LoadingStatus", "LOADING . . . . . . .", 31f, FontStyles.Bold, Color.white);
        statusText.alignment = TextAlignmentOptions.Left;
        statusText.characterSpacing = 2f;
        SetRect(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-340f, -258f), new Vector2(680f, 46f));

        Image barOuterGlow = CreateImage(canvas.transform, "Img_LoadingBarGlow", new Color(0f, 0.65f, 1f, 0.32f));
        SetRect(barOuterGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(704f, 58f));

        Image barFrame = CreateImage(canvas.transform, "Img_LoadingBarFrame", Cyan);
        SetRect(barFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(688f, 46f));

        Image barBackground = CreateImage(barFrame.transform, "Img_LoadingBarBackground", new Color(0.01f, 0.05f, 0.09f, 0.96f));
        SetStretch(barBackground.rectTransform, 4f, 4f, 4f, 4f);

        progressFill = CreateImage(barBackground.transform, "Img_LoadingBarFill", Cyan);
        progressFill.type = Image.Type.Simple;
        SetProgressFillRect(0f);

        Image barShine = CreateImage(progressFill.transform, "Img_LoadingBarShine", new Color(1f, 1f, 1f, 0.2f));
        SetStretch(barShine.rectTransform, 0f, 0f, 0f, 20f);

        percentText = CreateText(canvas.transform, "Txt_LoadingPercent", "0%", 18f, FontStyles.Bold, new Color(0.75f, 0.98f, 1f, 1f));
        percentText.alignment = TextAlignmentOptions.Right;
        SetRect(percentText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(340f, -356f), new Vector2(120f, 30f));
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas_Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, FontStyles style, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private void ConfigureTitleText(TextMeshProUGUI text)
    {
        text.font = RuntimeFontManager.Font;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 7f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 82f;
        text.fontSizeMax = 112f;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private Sprite LoadResourceSprite(string resourceKey)
    {
        Sprite sprite = Resources.Load<Sprite>(resourceKey);

        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourceKey);

        if (texture == null)
        {
            Debug.LogWarning($"[LoadingSceneController] Missing Resources/{resourceKey} image.");
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
