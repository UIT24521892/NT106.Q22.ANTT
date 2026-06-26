using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSettingsUI : MonoBehaviour
{
    private static GameSettingsUI instance;
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
            ? ""
            : state.IsPaused
                ? "Tran dau dang tam dung"
                : pending
                    ? $"{state.PauseRequestedBy} de nghi tam dung"
                    : "";

        SetButtonLabel(pauseButton, state != null && state.IsPaused ? "Tiep tuc" : "Yeu cau tam dung");
        voteButton.gameObject.SetActive(pending && !voted);
    }

    private void TogglePanel()
    {
        panel.SetActive(!panel.activeSelf);
        surrenderConfirmationPending = false;
        SetButtonLabel(surrenderButton, "Dau hang");
        if (panel.activeSelf)
            panel.transform.SetAsLastSibling();
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
            SetButtonLabel(surrenderButton, "Xac nhan dau hang");
            return;
        }

        NetworkManager.Instance?.SendSurrenderRequest();
        panel.SetActive(false);
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
        timerText.color = seconds <= 60 ? new Color(0.95f, 0.25f, 0.15f) : Color.white;
    }

    private void BuildUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        timerText = CreateText(canvas.transform, "Txt_MatchTimer", "20:00", 28f);
        SetRect(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(180f, 44f));

        Button settingsButton = CreateButton(canvas.transform, "Btn_Settings", "SET");
        SetRect(settingsButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one, new Vector2(-18f, -18f), new Vector2(62f, 52f));
        settingsButton.onClick.AddListener(TogglePanel);

        panel = new GameObject("Panel_Settings", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.11f, 0.97f);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 590f));

        TextMeshProUGUI title = CreateText(panel.transform, "Txt_Title", "CAI DAT", 32f);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(360f, 46f));

        AudioManager audio = AudioManager.EnsureExists();
        CreateVolumeRow("Am luong tong", 105f, audio.MasterVolume, audio.SetMasterVolume);
        CreateVolumeRow("Nhac nen", 175f, audio.MusicVolume, audio.SetMusicVolume);
        CreateVolumeRow("Hieu ung", 245f, audio.SfxVolume, audio.SetSfxVolume);

        Toggle mute = CreateToggle(panel.transform, "Toggle_Mute", "Tat tieng");
        SetRect(mute.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(260f, 42f));
        mute.isOn = audio.IsMuted;
        mute.onValueChanged.AddListener(audio.SetMuted);

        pauseStatusText = CreateText(panel.transform, "Txt_PauseStatus", "", 18f);
        SetRect(pauseStatusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -365f), new Vector2(430f, 48f));

        pauseButton = CreateButton(panel.transform, "Btn_Pause", "Yeu cau tam dung");
        SetRect(pauseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(320f, 48f));
        pauseButton.onClick.AddListener(HandlePause);

        voteButton = CreateButton(panel.transform, "Btn_AcceptPause", "Dong y tam dung");
        SetRect(voteButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-112f, -485f), new Vector2(220f, 46f));
        voteButton.onClick.AddListener(() => NetworkManager.Instance?.SendPauseVote(true));
        voteButton.gameObject.SetActive(false);

        surrenderButton = CreateButton(panel.transform, "Btn_Surrender", "Dau hang");
        surrenderButton.GetComponent<Image>().color = new Color(0.72f, 0.16f, 0.14f);
        SetRect(surrenderButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(122f, -485f), new Vector2(210f, 46f));
        surrenderButton.onClick.AddListener(HandleSurrender);

        Button close = CreateButton(panel.transform, "Btn_CloseSettings", "X");
        SetRect(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one, new Vector2(-12f, -12f), new Vector2(48f, 44f));
        close.onClick.AddListener(TogglePanel);

        panel.SetActive(false);
    }

    private void CreateVolumeRow(string label, float top, float value, UnityEngine.Events.UnityAction<float> callback)
    {
        TextMeshProUGUI text = CreateText(panel.transform, "Txt_" + label, label, 19f);
        text.alignment = TextAlignmentOptions.Left;
        SetRect(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-115f, -top), new Vector2(190f, 34f));

        Slider slider = CreateSlider(panel.transform, "Slider_" + label);
        SetRect(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(100f, -top), new Vector2(190f, 30f));
        slider.value = value;
        slider.onValueChanged.AddListener(callback);
    }

    private Slider CreateSlider(Transform parent, string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        Slider slider = root.GetComponent<Slider>();

        Image background = CreateImage(root.transform, "Background", new Color(0.2f, 0.24f, 0.28f));
        SetRect(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 8f));

        Image fill = CreateImage(root.transform, "Fill", new Color(0.15f, 0.62f, 0.86f));
        SetRect(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, 8f));

        Image handle = CreateImage(root.transform, "Handle", Color.white);
        SetRect(handle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    private Toggle CreateToggle(Transform parent, string name, string label)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        Toggle toggle = root.GetComponent<Toggle>();

        Image background = CreateImage(root.transform, "Background", new Color(0.2f, 0.24f, 0.28f));
        SetRect(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(30f, 30f));
        Image checkmark = CreateImage(background.transform, "Checkmark", new Color(0.15f, 0.75f, 0.4f));
        SetRect(checkmark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
        TextMeshProUGUI text = CreateText(root.transform, "Label", label, 20f);
        text.alignment = TextAlignmentOptions.Left;
        SetRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(35f, 0f), new Vector2(-70f, 0f));

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private Button CreateButton(Transform parent, string name, string label)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = new Color(0.12f, 0.48f, 0.72f);
        TextMeshProUGUI text = CreateText(root.transform, "Text", label, 19f);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return root.GetComponent<Button>();
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string value, float size)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
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
}
