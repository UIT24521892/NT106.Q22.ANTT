using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSettingsUI : MonoBehaviour
{
    private const string PrefabResourcePath = "UI/GameSettingsPanel";
    private static readonly Color Blue = new Color(0.08f, 0.48f, 0.76f, 1f);
    private static readonly Color LightPanel = new Color(0.96f, 0.95f, 0.91f, 1f);
    private static readonly Color DarkText = new Color(0.08f, 0.11f, 0.14f, 1f);
    private static readonly Color MutedText = new Color(0.34f, 0.39f, 0.43f, 1f);

    private static GameSettingsUI instance;
    private GameObject overlay;
    private GameObject panel;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI pauseStatusText;
    private Button pauseButton;
    private Button voteButton;
    private Button surrenderButton;
    private GameStateData state;
    private bool surrenderConfirmationPending;

    public static GameSettingsUI EnsureExists()
    {
        if (instance != null)
            return instance;

        return new GameObject("GameSettingsUI").AddComponent<GameSettingsUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
    }

    private void Update()
    {
        UpdateTimer();
    }

    public void Refresh(GameStateData gameState)
    {
        state = gameState;
        if (pauseStatusText == null || pauseButton == null || voteButton == null)
            return;

        string username = PlayerSession.Instance?.Username ?? "";
        bool pending = !string.IsNullOrWhiteSpace(state?.PauseRequestedBy) && !(state?.IsPaused ?? false);
        bool voted = state?.PauseVotes != null && state.PauseVotes.Contains(username);

        pauseStatusText.text = state == null
            ? "Chua co thong tin tran dau"
            : state.IsPaused
                ? "Tran dau dang tam dung"
                : pending
                    ? $"{state.PauseRequestedBy} de nghi tam dung"
                    : "Tran dau dang dien ra";

        SetButtonLabel(pauseButton, state != null && state.IsPaused ? "TIEP TUC TRAN" : "YEU CAU TAM DUNG");
        voteButton.gameObject.SetActive(pending && !voted);
    }

    private void TogglePanel()
    {
        if (overlay == null)
            return;

        bool show = !overlay.activeSelf;
        overlay.SetActive(show);
        surrenderConfirmationPending = false;
        SetButtonLabel(surrenderButton, "DAU HANG");

        if (show)
            overlay.transform.SetAsLastSibling();
    }

    private void HandlePause()
    {
        if (state != null && state.IsPaused)
            NetworkManager.Instance?.SendResumeGameplayRequest();
        else
            NetworkManager.Instance?.SendPauseRequest();
    }

    private void HandleSurrender()
    {
        if (!surrenderConfirmationPending)
        {
            surrenderConfirmationPending = true;
            SetButtonLabel(surrenderButton, "XAC NHAN DAU HANG");
            return;
        }

        NetworkManager.Instance?.SendSurrenderRequest();
        overlay.SetActive(false);
    }

    private void UpdateTimer()
    {
        if (timerText == null || state == null || state.MatchEndsAtUtcTicks <= 0)
            return;

        long referenceTicks = state.IsPaused && state.PauseStartedAtUtcTicks > 0
            ? state.PauseStartedAtUtcTicks
            : NetworkManager.Instance != null ? NetworkManager.Instance.EstimatedServerNowTicks : DateTime.UtcNow.Ticks;
        int seconds = Mathf.Max(0, (int)((state.MatchEndsAtUtcTicks - referenceTicks) / TimeSpan.TicksPerSecond));
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        timerText.color = seconds <= 60 ? new Color(0.95f, 0.22f, 0.16f) : new Color(0.1f, 0.18f, 0.22f);
    }

    private void BuildUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        BuildTimer(canvas.transform);
        BuildSettingsLauncher(canvas.transform);

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            overlay = Instantiate(prefab, canvas.transform, false);
            overlay.name = "Panel_SettingsOverlay";
            panel = FindChild(overlay.transform, "Panel_Settings")?.gameObject ?? overlay;
            if (BindPrefabControls())
            {
                overlay.SetActive(false);
                return;
            }

            Debug.LogWarning("[GameSettingsUI] GameSettingsPanel prefab is missing required named controls. Using runtime fallback.");
            Destroy(overlay);
        }

        BuildRuntimePanel(canvas.transform);
    }

    private void BuildTimer(Transform canvas)
    {
        Transform dicePanel = FindSceneObjectByName("DicePanel")?.transform;
        Transform parent = dicePanel != null ? dicePanel : canvas;

        Image timerBackground = CreateImage(parent, "Img_MatchTimerBackground", new Color(1f, 1f, 1f, 0.9f));
        timerBackground.raycastTarget = false;
        SetRect(
            timerBackground.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(205f, 90f),
            new Vector2(142f, 48f));

        timerText = CreateText(timerBackground.transform, "Txt_MatchTimer", "20:00", 29f, FontStyles.Bold, DarkText);
        SetStretch(timerText.rectTransform, 8f, 4f, 8f, 4f);
    }

    private void BuildSettingsLauncher(Transform canvas)
    {
        Button settingsButton = CreateButton(canvas, "Btn_Settings", "SET", Blue, Color.white, 18f);
        SetRect(
            settingsButton.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-145f, 24f),
            new Vector2(76f, 48f));
        settingsButton.onClick.AddListener(TogglePanel);
    }

    private bool BindPrefabControls()
    {
        pauseStatusText = FindComponent<TextMeshProUGUI>(panel.transform, "Txt_PauseStatus");
        pauseButton = FindComponent<Button>(panel.transform, "Btn_Pause");
        voteButton = FindComponent<Button>(panel.transform, "Btn_AcceptPause");
        surrenderButton = FindComponent<Button>(panel.transform, "Btn_Surrender");
        Button closeButton = FindComponent<Button>(panel.transform, "Btn_CloseSettings");
        Slider masterSlider = FindComponent<Slider>(panel.transform, "Slider_Master");
        Slider musicSlider = FindComponent<Slider>(panel.transform, "Slider_Music");
        Slider sfxSlider = FindComponent<Slider>(panel.transform, "Slider_Sfx");
        Toggle muteToggle = FindComponent<Toggle>(panel.transform, "Toggle_Mute");

        if (pauseStatusText == null || pauseButton == null || voteButton == null ||
            surrenderButton == null || closeButton == null || masterSlider == null ||
            musicSlider == null || sfxSlider == null || muteToggle == null)
        {
            return false;
        }

        AudioManager audio = AudioManager.EnsureExists();
        ConfigureSlider(masterSlider, audio.MasterVolume, audio.SetMasterVolume);
        ConfigureSlider(musicSlider, audio.MusicVolume, audio.SetMusicVolume);
        ConfigureSlider(sfxSlider, audio.SfxVolume, audio.SetSfxVolume);
        muteToggle.isOn = audio.IsMuted;
        muteToggle.onValueChanged.AddListener(audio.SetMuted);
        pauseButton.onClick.AddListener(HandlePause);
        voteButton.onClick.AddListener(() => NetworkManager.Instance?.SendPauseVote(true));
        surrenderButton.onClick.AddListener(HandleSurrender);
        closeButton.onClick.AddListener(TogglePanel);
        voteButton.gameObject.SetActive(false);
        return true;
    }

    private void BuildRuntimePanel(Transform canvas)
    {
        overlay = new GameObject("Panel_SettingsOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(canvas, false);
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0.02f, 0.05f, 0.08f, 0.58f);
        overlayImage.raycastTarget = true;
        SetStretch(overlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        panel = new GameObject("Panel_Settings", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        panel.GetComponent<Image>().color = LightPanel;
        SetRect(
            panel.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(610f, 610f));

        Image header = CreateImage(panel.transform, "Img_Header", Blue);
        SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 94f));

        TextMeshProUGUI title = CreateText(header.transform, "Txt_Title", "CAI DAT", 32f, FontStyles.Bold, Color.white);
        SetStretch(title.rectTransform, 80f, 10f, 80f, 10f);

        Button close = CreateButton(header.transform, "Btn_CloseSettings", "X", new Color(0.03f, 0.34f, 0.57f), Color.white, 24f);
        SetRect(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one, new Vector2(-15f, -17f), new Vector2(58f, 58f));
        close.onClick.AddListener(TogglePanel);

        TextMeshProUGUI audioTitle = CreateText(panel.transform, "Txt_AudioSection", "AM THANH", 18f, FontStyles.Bold, Blue);
        audioTitle.alignment = TextAlignmentOptions.Left;
        SetRect(audioTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(-76f, 32f));

        Image audioPanel = CreateImage(panel.transform, "Panel_Audio", Color.white);
        SetRect(audioPanel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -157f), new Vector2(-76f, 220f));

        AudioManager audio = AudioManager.EnsureExists();
        CreateVolumeRow(audioPanel.transform, "Am luong tong", "Slider_Master", 44f, audio.MasterVolume, audio.SetMasterVolume);
        CreateVolumeRow(audioPanel.transform, "Nhac nen", "Slider_Music", 104f, audio.MusicVolume, audio.SetMusicVolume);
        CreateVolumeRow(audioPanel.transform, "Hieu ung", "Slider_Sfx", 164f, audio.SfxVolume, audio.SetSfxVolume);

        Toggle mute = CreateToggle(audioPanel.transform, "Toggle_Mute", "Tat tat ca am thanh");
        SetRect(mute.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(250f, 38f));
        mute.isOn = audio.IsMuted;
        mute.onValueChanged.AddListener(audio.SetMuted);

        TextMeshProUGUI gameTitle = CreateText(panel.transform, "Txt_GameSection", "DIEU KHIEN TRAN DAU", 18f, FontStyles.Bold, Blue);
        gameTitle.alignment = TextAlignmentOptions.Left;
        SetRect(gameTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -405f), new Vector2(-76f, 32f));

        pauseStatusText = CreateText(panel.transform, "Txt_PauseStatus", "Tran dau dang dien ra", 17f, FontStyles.Normal, MutedText);
        pauseStatusText.alignment = TextAlignmentOptions.Left;
        SetRect(pauseStatusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -444f), new Vector2(-76f, 34f));

        pauseButton = CreateButton(panel.transform, "Btn_Pause", "YEU CAU TAM DUNG", Blue, Color.white, 18f);
        SetRect(pauseButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(38f, 38f), new Vector2(330f, 54f));
        pauseButton.onClick.AddListener(HandlePause);

        voteButton = CreateButton(panel.transform, "Btn_AcceptPause", "DONG Y", new Color(0.16f, 0.58f, 0.35f), Color.white, 17f);
        SetRect(voteButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(382f, 38f), new Vector2(170f, 54f));
        voteButton.onClick.AddListener(() => NetworkManager.Instance?.SendPauseVote(true));
        voteButton.gameObject.SetActive(false);

        surrenderButton = CreateButton(panel.transform, "Btn_Surrender", "DAU HANG", new Color(0.78f, 0.17f, 0.15f), Color.white, 18f);
        SetRect(surrenderButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-38f, 38f), new Vector2(190f, 54f));
        surrenderButton.onClick.AddListener(HandleSurrender);

        overlay.SetActive(false);
    }

    private void CreateVolumeRow(
        Transform parent,
        string label,
        string sliderName,
        float top,
        float value,
        UnityEngine.Events.UnityAction<float> callback)
    {
        TextMeshProUGUI text = CreateText(parent, "Txt_" + sliderName, label, 18f, FontStyles.Normal, DarkText);
        text.alignment = TextAlignmentOptions.Left;
        SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -top), new Vector2(180f, 32f));

        Slider slider = CreateSlider(parent, sliderName);
        SetRect(slider.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -top), new Vector2(300f, 32f));
        ConfigureSlider(slider, value, callback);
    }

    private void ConfigureSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> callback)
    {
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = value;
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(callback);
    }

    private Slider CreateSlider(Transform parent, string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        Slider slider = root.GetComponent<Slider>();

        Image background = CreateImage(root.transform, "Background", new Color(0.72f, 0.76f, 0.79f));
        SetRect(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 8f));

        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(root.transform, false);
        SetStretch(fillArea, 8f, 10f, 8f, 10f);

        Image fill = CreateImage(fillArea, "Fill", Blue);
        SetStretch(fill.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(root.transform, false);
        SetStretch(handleArea, 12f, 0f, 12f, 0f);

        Image handle = CreateImage(handleArea, "Handle", new Color(0.04f, 0.22f, 0.34f));
        SetRect(handle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
        Outline outline = handle.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2f, -2f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private Toggle CreateToggle(Transform parent, string name, string label)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        Toggle toggle = root.GetComponent<Toggle>();

        Image background = CreateImage(root.transform, "Background", new Color(0.86f, 0.87f, 0.88f));
        SetRect(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(15f, 0f), new Vector2(30f, 30f));
        Image checkmark = CreateImage(background.transform, "Checkmark", Blue);
        SetRect(checkmark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
        TextMeshProUGUI text = CreateText(root.transform, "Label", label, 17f, FontStyles.Normal, DarkText);
        text.alignment = TextAlignmentOptions.Left;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(48f, 0f), new Vector2(-80f, 0f));

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private Button CreateButton(Transform parent, string name, string label, Color background, Color foreground, float fontSize)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = background;
        TextMeshProUGUI text = CreateText(root.transform, "Text", label, fontSize, FontStyles.Bold, foreground);
        SetStretch(text.rectTransform, 8f, 4f, 8f, 4f);
        return root.GetComponent<Button>();
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        FontStyles style,
        Color color)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private T FindComponent<T>(Transform root, string name) where T : Component
    {
        Transform target = FindChild(root, name);
        return target != null ? target.GetComponent<T>() : null;
    }

    private Transform FindChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), name);
            if (result != null)
                return result;
        }

        return null;
    }

    private GameObject FindSceneObjectByName(string name)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        return objects.FirstOrDefault(item =>
            item != null &&
            item.name == name &&
            item.scene.IsValid() &&
            item.scene.isLoaded);
    }

    private void SetButtonLabel(Button button, string value)
    {
        TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (text != null)
            text.text = value;
    }

    private void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
