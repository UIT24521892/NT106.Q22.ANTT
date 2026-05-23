using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameChatUI : MonoBehaviour
{
    private const int MaxVisibleMessages = 8;

    private TextMeshProUGUI chatLogText;
    private TMP_InputField inputField;
    private Button sendButton;
    private RectTransform bubbleLayer;

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

        GameObject panelObject = new GameObject("Panel_GameChat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
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
        titleRect.sizeDelta = new Vector2(-20f, 28f);

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

        inputField.onSubmit.AddListener(_ => SendCurrentMessage());
        sendButton.onClick.AddListener(SendCurrentMessage);
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
