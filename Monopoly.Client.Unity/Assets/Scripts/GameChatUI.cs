using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameChatUI : MonoBehaviour
{
    private const int MaxVisibleMessages = 8;
    private const string ChatPanelPrefabPath = "UI/ChatPanel";

    private RectTransform panelRect;
    private Button toggleButton;
    private Button closeButton;
    private TextMeshProUGUI chatLogText;
    private TMP_InputField inputField;
    private Button sendButton;
    private RectTransform bubbleLayer;
    private readonly List<GameObject> chatWindowObjects = new List<GameObject>();
    private bool usingScenePanel;

    public static GameChatUI EnsureExists()
    {
        GameChatUI existing = FindObjectOfType<GameChatUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("GameChatUI");
        return host.AddComponent<GameChatUI>();
    }

    private void Start()
    {
        if (!TryBindPrefabPanel() && !TryBindScenePanel())
            BuildRuntimeUi();

        RegisterNetworkEvents();
        RefreshChatLog();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.GameChatMessageReceived -= OnChatMessageReceived;
    }

    private void BuildRuntimeUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[GameChatUI] Canvas not found.");
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        toggleButton = CreateToggleButton(canvasRect);

        GameObject panelObject = new GameObject("Panel_GameChat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(canvasRect, false);
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(420f, 210f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);
        panelImage.raycastTarget = true;

        TextMeshProUGUI title = CreateText("Txt_ChatTitle", panelRect, "Chat", 18f, FontStyles.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);
        titleRect.sizeDelta = new Vector2(-72f, 28f);

        closeButton = CreateCloseButton(panelRect);
        closeButton.onClick.AddListener(ClosePanel);

        chatLogText = CreateText("Txt_ChatLog", panelRect, "", 15f, FontStyles.Normal);
        RectTransform logRect = chatLogText.rectTransform;
        logRect.anchorMin = new Vector2(0f, 0f);
        logRect.anchorMax = new Vector2(1f, 1f);
        logRect.offsetMin = new Vector2(12f, 50f);
        logRect.offsetMax = new Vector2(-12f, -38f);
        chatLogText.enableWordWrapping = true;
        chatLogText.overflowMode = TextOverflowModes.Ellipsis;

        inputField = CreateInputField(panelRect);
        sendButton = CreateSendButton(panelRect);
        bubbleLayer = CreateBubbleLayer(canvasRect);

        inputField.onSubmit.AddListener(OnSubmitMessage);
        sendButton.onClick.AddListener(SendCurrentMessage);
        AddChatWindowObject(panelRect.gameObject);
        HideChatWindow();
    }

    private bool TryBindScenePanel()
    {
        GameObject chatPanel = FindSceneObjectByName("ChatPanel");

        if (chatPanel == null)
        {
            Debug.Log("[GameChatUI] Scene ChatPanel not found. Trying prefab chat UI.");
            return false;
        }

        return TryBindChatPanel(chatPanel, "scene ChatPanel");
    }

    private bool TryBindPrefabPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[GameChatUI] Canvas not found.");
            return false;
        }

        GameObject prefab = Resources.Load<GameObject>(ChatPanelPrefabPath);

        if (prefab == null)
        {
            Debug.Log($"[GameChatUI] Prefab Resources/{ChatPanelPrefabPath}.prefab not found. Trying scene ChatPanel.");
            return false;
        }

        DisableSceneChatPanels();

        GameObject chatPanel = Instantiate(prefab, canvas.transform, false);
        chatPanel.name = "ChatPanel";
        chatPanel.SetActive(true);

        if (TryBindChatPanel(chatPanel, "prefab ChatPanel"))
            return true;

        Destroy(chatPanel);
        return false;
    }

    private void DisableSceneChatPanels()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject candidate in objects)
        {
            if (candidate == null ||
                candidate.name != "ChatPanel" ||
                !candidate.scene.IsValid() ||
                !candidate.scene.isLoaded)
            {
                continue;
            }

            candidate.SetActive(false);
        }
    }

    private bool TryBindChatPanel(GameObject chatPanel, string sourceName)
    {
        RectTransform chatPanelRect = chatPanel.transform as RectTransform;

        if (chatPanelRect == null)
        {
            Debug.LogWarning($"[GameChatUI] {sourceName} has no RectTransform.");
            return false;
        }

        Button sceneToggleButton = FindChildComponent<Button>(chatPanel.transform, "Btn_ChatToggle");
        RectTransform sceneWindow = FindChildRect(chatPanel.transform, "Panel_ChatWindow");
        TMP_InputField sceneInput = FindChildComponent<TMP_InputField>(chatPanel.transform, "Input_Message");
        Button sceneSendButton = FindChildComponent<Button>(chatPanel.transform, "Btn_Send");
        Button sceneCloseButton = FindChildComponent<Button>(chatPanel.transform, "Btn_Close");
        TextMeshProUGUI sceneChatLog = FindChatLogText(chatPanel.transform);
        List<string> missingObjects = new List<string>();

        if (sceneToggleButton == null)
            missingObjects.Add("Btn_ChatToggle Button");

        if (sceneWindow == null)
            missingObjects.Add("Panel_ChatWindow RectTransform");

        if (sceneInput == null)
            missingObjects.Add("Input_Message TMP_InputField");

        if (sceneSendButton == null)
            missingObjects.Add("Btn_Send Button");

        if (sceneCloseButton == null)
            missingObjects.Add("Btn_Close Button");

        if (sceneChatLog == null)
            missingObjects.Add("ScrollView_Messages text or Txt_ChatLog TextMeshProUGUI");

        if (missingObjects.Count > 0)
        {
            Debug.LogWarning($"[GameChatUI] {sourceName} found but required children/components are missing: {string.Join(", ", missingObjects)}.");
            return false;
        }

        Canvas canvas = chatPanel.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning($"[GameChatUI] {sourceName} found but Canvas is missing.");
            return false;
        }

        panelRect = sceneWindow;
        toggleButton = sceneToggleButton;
        closeButton = sceneCloseButton;
        inputField = sceneInput;
        sendButton = sceneSendButton;
        chatLogText = sceneChatLog;
        bubbleLayer = FindChildRect(chatPanel.transform, "Runtime_ChatBubbles");

        if (bubbleLayer == null)
            bubbleLayer = CreateBubbleLayer(canvas.transform as RectTransform);

        chatWindowObjects.Clear();
        AddChatWindowObject(panelRect.gameObject);
        AddChatWindowObject(FindChatLogRoot(chatPanel.transform, chatLogText));
        AddChatWindowObject(inputField.gameObject);
        AddChatWindowObject(sendButton.gameObject);
        AddChatWindowObject(closeButton.gameObject);

        toggleButton.onClick.RemoveListener(OpenPanel);
        closeButton.onClick.RemoveListener(ClosePanel);
        sendButton.onClick.RemoveListener(SendCurrentMessage);
        inputField.onSubmit.RemoveListener(OnSubmitMessage);

        toggleButton.onClick.AddListener(OpenPanel);
        closeButton.onClick.AddListener(ClosePanel);
        sendButton.onClick.AddListener(SendCurrentMessage);
        inputField.onSubmit.AddListener(OnSubmitMessage);

        HideChatWindow();
        toggleButton.gameObject.SetActive(true);
        usingScenePanel = true;
        Debug.Log($"[GameChatUI] Bound {sourceName}.");
        return true;
    }

    private Button CreateToggleButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("Btn_ToggleChat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-20f, 20f);
        buttonRect.sizeDelta = new Vector2(110f, 42f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.62f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(OpenPanel);

        TextMeshProUGUI label = CreateText("Text", buttonRect, "Chat", 18f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        SetStretchWithPadding(label.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private Button CreateCloseButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("Btn_CloseChat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-10f, -8f);
        buttonRect.sizeDelta = new Vector2(42f, 32f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.78f, 0.12f, 0.1f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = CreateText("Text", buttonRect, "X", 16f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        SetStretchWithPadding(label.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private RectTransform CreateBubbleLayer(RectTransform parent)
    {
        GameObject layerObject = new GameObject("Runtime_ChatBubbles", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.SetAsLastSibling();
        return rect;
    }

    private TextMeshProUGUI FindChatLogText(Transform root)
    {
        RectTransform messagesRoot = FindChildRect(root, "ScrollView_Messages");

        if (messagesRoot == null)
            messagesRoot = FindChildRect(root, "ScrollView_Message");

        if (messagesRoot != null)
        {
            TextMeshProUGUI textInScroll = messagesRoot.GetComponentInChildren<TextMeshProUGUI>(true);

            if (textInScroll != null)
                return textInScroll;
        }

        TextMeshProUGUI namedText = FindChildComponent<TextMeshProUGUI>(root, "Txt_ChatLog");

        if (namedText != null)
            return namedText;

        return root.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private GameObject FindChatLogRoot(Transform root, TextMeshProUGUI text)
    {
        RectTransform messagesRoot = FindChildRect(root, "ScrollView_Messages");

        if (messagesRoot == null)
            messagesRoot = FindChildRect(root, "ScrollView_Message");

        if (messagesRoot != null)
            return messagesRoot.gameObject;

        return text == null ? null : text.gameObject;
    }

    private RectTransform FindChildRect(Transform root, string childName)
    {
        Transform child = FindChildByName(root, childName);
        return child as RectTransform;
    }

    private T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        Transform child = FindChildByName(root, childName);
        return child == null ? null : child.GetComponent<T>();
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);

            if (result != null)
                return result;
        }

        return null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject candidate in objects)
        {
            if (candidate == null ||
                candidate.name != objectName ||
                !candidate.scene.IsValid() ||
                !candidate.scene.isLoaded)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    private TMP_InputField CreateInputField(Transform parent)
    {
        GameObject inputObject = new GameObject("Input_ChatMessage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.SetParent(parent, false);
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(12f, 10f);
        inputRect.offsetMax = new Vector2(-92f, 44f);

        Image inputImage = inputObject.GetComponent<Image>();
        inputImage.color = new Color(1f, 1f, 1f, 0.92f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.targetGraphic = inputImage;
        input.characterLimit = 160;
        input.lineType = TMP_InputField.LineType.SingleLine;

        TextMeshProUGUI text = CreateText("Text", inputRect, "", 15f, FontStyles.Normal);
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        SetStretchWithPadding(text.rectTransform, 8f, 6f, 8f, 6f);

        TextMeshProUGUI placeholder = CreateText("Placeholder", inputRect, "Type message...", 15f, FontStyles.Italic);
        placeholder.color = new Color(0f, 0f, 0f, 0.45f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.enableWordWrapping = false;
        SetStretchWithPadding(placeholder.rectTransform, 8f, 6f, 8f, 6f);

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private Button CreateSendButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("Btn_SendChat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-12f, 10f);
        buttonRect.sizeDelta = new Vector2(72f, 34f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.55f, 0.95f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = CreateText("Text", buttonRect, "Send", 15f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        SetStretchWithPadding(label.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private void SetStretchWithPadding(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void RegisterNetworkEvents()
    {
        if (NetworkManager.Instance == null)
            return;

        NetworkManager.Instance.GameChatMessageReceived -= OnChatMessageReceived;
        NetworkManager.Instance.GameChatMessageReceived += OnChatMessageReceived;
    }

    private void OpenPanel()
    {
        ShowChatWindow();

        if (panelRect != null && !usingScenePanel)
        {
            panelRect.SetAsLastSibling();
        }

        if (toggleButton != null)
            toggleButton.gameObject.SetActive(false);

        RefreshChatLog();

        if (inputField != null)
            inputField.ActivateInputField();
    }

    private void ClosePanel()
    {
        HideChatWindow();

        if (toggleButton != null)
            toggleButton.gameObject.SetActive(true);
    }

    private void ShowChatWindow()
    {
        foreach (GameObject windowObject in chatWindowObjects)
        {
            if (windowObject != null)
                windowObject.SetActive(true);
        }
    }

    private void HideChatWindow()
    {
        foreach (GameObject windowObject in chatWindowObjects)
        {
            if (windowObject != null)
                windowObject.SetActive(false);
        }
    }

    private void AddChatWindowObject(GameObject windowObject)
    {
        if (windowObject == null)
            return;

        for (int i = chatWindowObjects.Count - 1; i >= 0; i--)
        {
            GameObject existing = chatWindowObjects[i];

            if (existing == null)
            {
                chatWindowObjects.RemoveAt(i);
                continue;
            }

            if (windowObject.transform.IsChildOf(existing.transform))
                return;

            if (existing.transform.IsChildOf(windowObject.transform))
                chatWindowObjects.RemoveAt(i);
        }

        chatWindowObjects.Add(windowObject);
    }

    private void SendCurrentMessage()
    {
        if (inputField == null || NetworkManager.Instance == null)
            return;

        string message = inputField.text;

        if (string.IsNullOrWhiteSpace(message))
            return;

        NetworkManager.Instance.SendGameChatMessage(message);
        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void OnSubmitMessage(string _)
    {
        SendCurrentMessage();
    }

    private void OnChatMessageReceived(ChatMessageData message)
    {
        RefreshChatLog();
        ShowChatBubble(message);
    }

    private void ShowChatBubble(ChatMessageData message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Message) || bubbleLayer == null)
            return;

        Canvas canvas = bubbleLayer.GetComponentInParent<Canvas>();

        if (canvas == null)
            return;

        Vector2 anchoredPosition = new Vector2(0f, 150f);
        BoardTokenManager tokenManager = FindObjectOfType<BoardTokenManager>();

        if (tokenManager != null && tokenManager.TryGetPlayerTokenWorldPosition(message.Username, out Vector3 tokenWorldPosition))
        {
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, tokenWorldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bubbleLayer, screenPoint, uiCamera, out anchoredPosition);
            anchoredPosition += new Vector2(0f, 64f);
        }

        GameObject bubbleObject = new GameObject("Bubble_Chat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform bubbleRect = bubbleObject.GetComponent<RectTransform>();
        bubbleRect.SetParent(bubbleLayer, false);
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.anchoredPosition = anchoredPosition;
        bubbleRect.sizeDelta = new Vector2(260f, 58f);

        Image bubbleImage = bubbleObject.GetComponent<Image>();
        bubbleImage.color = new Color(1f, 1f, 1f, 0.92f);
        bubbleImage.raycastTarget = false;

        TextMeshProUGUI text = CreateText("Txt_BubbleMessage", bubbleRect, BuildBubbleText(message), 16f, FontStyles.Bold);
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetStretchWithPadding(text.rectTransform, 10f, 7f, 10f, 7f);

        StartCoroutine(AnimateBubble(bubbleRect, bubbleObject.GetComponent<CanvasGroup>()));
    }

    private IEnumerator AnimateBubble(RectTransform bubbleRect, CanvasGroup canvasGroup)
    {
        const float visibleSeconds = 2.4f;
        const float fadeSeconds = 0.35f;
        Vector2 startPosition = bubbleRect.anchoredPosition;
        float elapsed = 0f;

        canvasGroup.alpha = 1f;

        while (elapsed < visibleSeconds)
        {
            bubbleRect.anchoredPosition = startPosition + new Vector2(0f, elapsed * 10f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            float t = Mathf.Clamp01(elapsed / fadeSeconds);
            canvasGroup.alpha = 1f - t;
            bubbleRect.anchoredPosition = startPosition + new Vector2(0f, visibleSeconds * 10f + t * 12f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(bubbleRect.gameObject);
    }

    private string BuildBubbleText(ChatMessageData message)
    {
        string text = message.Message.Trim();

        if (text.Length > 70)
            text = text.Substring(0, 67) + "...";

        return $"{ShortName(message.Username)}: {text}";
    }

    private void RefreshChatLog()
    {
        if (chatLogText == null || NetworkManager.Instance == null)
            return;

        IReadOnlyList<ChatMessageData> messages = NetworkManager.Instance.GetGameChatMessages();
        int startIndex = Math.Max(0, messages.Count - MaxVisibleMessages);
        StringBuilder builder = new StringBuilder();

        for (int i = startIndex; i < messages.Count; i++)
        {
            ChatMessageData message = messages[i];
            builder.AppendLine($"{ShortName(message.Username)}: {message.Message}");
        }

        chatLogText.text = builder.ToString();
    }

    private string ShortName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Player";

        int atIndex = username.IndexOf("@", StringComparison.Ordinal);

        if (atIndex > 0)
            username = username.Substring(0, atIndex);

        return username.Length > 12 ? username.Substring(0, 12) : username;
    }
}
