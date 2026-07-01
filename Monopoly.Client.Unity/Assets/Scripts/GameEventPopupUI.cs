using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameEventPopupUI : MonoBehaviour
{
    private static GameEventPopupUI instance;

    private readonly Queue<EventPopupData> pendingPopups = new Queue<EventPopupData>();
    private readonly HashSet<string> shownEventKeys = new HashSet<string>();

    private RectTransform rootRect;
    private RectTransform cardRect;
    private Image overlayImage;
    private Image cardImage;
    private Image headerImage;
    private TextMeshProUGUI eyebrowText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI detailText;
    private Button closeButton;
    private Button primaryButton;

    private bool isShowing;
    private EventPopupData currentPopup;

    public static GameEventPopupUI EnsureExists()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("GameEventPopupUI");
        instance = host.AddComponent<GameEventPopupUI>();
        return instance;
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
        HideImmediate();
    }

    public void ResetEvents()
    {
        shownEventKeys.Clear();
        pendingPopups.Clear();
        HideImmediate();
    }

    public void ProcessGameStateUpdate(GameStateData state, string serverMessage)
    {
        if (state == null)
            return;

        ShowBankruptcyEvents(state, serverMessage);
        ShowDebtWarningIfNeeded(state, serverMessage);
    }

    public void ShowCardDrawn(string drawnByUsername, string cardId, string cardName, string cardType, string detailEffect)
    {
        if (!string.Equals(cardType, "Wooden", StringComparison.OrdinalIgnoreCase))
            return;

        GameStateData state = GameSession.CurrentState;
        string roomId = state?.RoomId ?? GameSession.RoomId ?? "";
        string key = $"wooden-card:{roomId}:{state?.TurnNumber ?? 0}:{drawnByUsername}:{cardId}";
        bool isLocal = IsLocalUsername(drawnByUsername);
        string title = isLocal ? "Bạn dính thẻ Gỗ" : $"{ShortName(drawnByUsername)} dính thẻ Gỗ";
        string message = string.IsNullOrWhiteSpace(cardName)
            ? "Một thẻ Cơ Hội bất lợi vừa được rút."
            : cardName;
        string details = string.IsNullOrWhiteSpace(detailEffect)
            ? "Hiệu ứng thẻ đã được server áp dụng vào trạng thái trận."
            : detailEffect;

        EnqueueOnce(key, EventPopupStyle.WoodenCard, "THẺ BẤT LỢI", title, message, details);
    }

    public void ShowGameOver(GameOverData gameOver, Action onClosed = null)
    {
        string matchId = string.IsNullOrWhiteSpace(gameOver?.MatchId) ? "unknown" : gameOver.MatchId;
        string key = $"game-over-packet:{matchId}";
        RankingEntryData localRanking = FindLocalRanking(gameOver);
        GameStateData state = GameSession.CurrentState;
        bool localWon = (localRanking != null && localRanking.Rank == 1) ||
            (state != null && IsLocalUsername(state.WinnerUsername));
        string title = localWon ? "VICTORY" : "DEFEAT";
        string message = localWon
            ? "Ban la nguoi chien thang"
            : "Ban da thua tran nay";
        string details = "An man hinh de tiep tuc";

        EnqueueOnce(key, localWon ? EventPopupStyle.Victory : EventPopupStyle.Defeat, "KẾT THÚC TRẬN", title, message, details, onClosed);
    }

    public void ShowActionFailed(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string normalized = NormalizeForSearch(message);
        bool mentionsMoney =
            normalized.Contains("tien") ||
            normalized.Contains("$") ||
            normalized.Contains("gia") ||
            normalized.Contains("phi");
        bool isMoneyProblem =
            normalized.Contains("khong du tien") ||
            normalized.Contains("khong co chi phi") ||
            normalized.Contains("ban tai san") ||
            normalized.Contains("tra no") ||
            (mentionsMoney && normalized.Contains("khong du")) ||
            (mentionsMoney && normalized.Contains("can "));

        if (!isMoneyProblem)
            return;

        string roomId = GameSession.RoomId ?? "";
        string key = $"money-action-failed:{roomId}:{message}";
        EnqueueOnce(
            key,
            EventPopupStyle.Debt,
            "THIẾU TIỀN",
            "Không đủ tiền để thực hiện",
            message,
            "Nếu gameplay bán tài sản được bật, đây là điểm nên chuyển sang màn chọn tài sản để bán."
        );
    }

    private void ShowBankruptcyEvents(GameStateData state, string serverMessage)
    {
        if (state.Players == null)
            return;

        foreach (GamePlayerStateData player in state.Players)
        {
            if (player == null || !player.IsBankrupt)
                continue;

            string orderPart = player.BankruptcyOrder > 0 ? player.BankruptcyOrder.ToString(CultureInfo.InvariantCulture) : "x";
            string key = $"bankrupt:{state.RoomId}:{player.PlayerIndex}:{orderPart}";
            bool isLocal = IsLocalPlayer(player);
            string title = isLocal ? "Bạn đã phá sản" : $"{ShortName(player.Username)} đã phá sản";
            string message = FindActionMessage(state, player.Username, "phá sản");

            if (string.IsNullOrWhiteSpace(message))
                message = ContainsIgnoreDiacritics(serverMessage, "phá sản") ? serverMessage : $"{ShortName(player.Username)} không còn đủ tiền để tiếp tục.";

            string details = isLocal
                ? "Bạn không còn tham gia lượt chơi. Các tài sản đã được giải phóng theo logic server."
                : "Người chơi này bị loại khỏi lượt chơi. Các tài sản đã được trả về trạng thái chưa có chủ.";

            EnqueueOnce(key, EventPopupStyle.Bankruptcy, "PHÁ SẢN", title, message, details);
        }
    }

    private void ShowDebtWarningIfNeeded(GameStateData state, string serverMessage)
    {
        GamePlayerStateData localPlayer = GetLocalPlayer(state);

        if (localPlayer == null || localPlayer.IsBankrupt)
            return;

        if (state.IsWaitingForPropertySale &&
            string.Equals(state.PendingSalePlayerUsername, localPlayer.Username, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int ownedProperties = CountOwnedProperties(state, localPlayer.PlayerIndex);

        if (localPlayer.Money < 0 && ownedProperties > 0)
        {
            string key = $"sell-required:{state.RoomId}:{state.TurnNumber}:{localPlayer.PlayerIndex}:{localPlayer.Money}";
            EnqueueOnce(
                key,
                EventPopupStyle.Debt,
                "CẦN BÁN TÀI SẢN",
                "Bạn đang thiếu tiền để trả phí",
                $"Số dư hiện tại: {FormatMoney(localPlayer.Money)}",
                $"Bạn đang sở hữu {ownedProperties} tài sản. Cần bán hoặc xử lý tài sản trước khi tiếp tục nếu server bật luật bán tài sản."
            );
            return;
        }

        if (TryBuildPartialPaymentWarning(state, localPlayer, serverMessage, out string warning))
        {
            string key = $"partial-payment:{state.RoomId}:{state.TurnNumber}:{warning}";
            EnqueueOnce(
                key,
                EventPopupStyle.Debt,
                "KHÔNG ĐỦ TIỀN TRẢ PHÍ",
                "Bạn không trả đủ khoản phí vừa phát sinh",
                warning,
                ownedProperties > 0
                    ? $"Bạn còn {ownedProperties} tài sản. Đây là điểm phù hợp để mở flow bán tài sản nếu server hỗ trợ."
                    : "Bạn không còn tài sản để xử lý khoản thiếu."
            );
        }
    }

    private void ShowFinishedStateEvent(GameStateData state)
    {
        if (!state.IsFinished)
            return;

        GamePlayerStateData localPlayer = GetLocalPlayer(state);

        if (localPlayer == null)
            return;

        bool localWon = string.Equals(localPlayer.Username, state.WinnerUsername, StringComparison.OrdinalIgnoreCase);
        string key = $"finished-state:{state.RoomId}:{state.WinnerUsername}:{localPlayer.PlayerIndex}";

        EnqueueOnce(
            key,
            localWon ? EventPopupStyle.Victory : EventPopupStyle.Defeat,
            "KẾT THÚC TRẬN",
            localWon ? "Bạn thắng trận" : "Bạn đã thua",
            $"Người thắng: {ShortName(state.WinnerUsername)}",
            localWon
                ? "Bạn là người còn lại hoặc có thứ hạng cao nhất khi trận kết thúc."
                : "Bạn không phải người thắng trận này. Bảng xếp hạng sẽ hiển thị sau khi server gửi kết quả."
        );
    }

    private bool TryBuildPartialPaymentWarning(
        GameStateData state,
        GamePlayerStateData localPlayer,
        string message,
        out string warning)
    {
        warning = "";

        if (string.IsNullOrWhiteSpace(message) || localPlayer == null)
            return false;

        if (!ContainsUsername(message, localPlayer.Username))
            return false;

        string normalized = NormalizeForSearch(message);

        if (!normalized.Contains("tien thue") && !normalized.Contains("tra"))
            return false;

        Match match = Regex.Match(message, @"trả\s+([\d,\.]+)\s*/\s*([\d,\.]+)", RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;

        long paid = ParseMoney(match.Groups[1].Value);
        long due = ParseMoney(match.Groups[2].Value);

        if (due <= 0 || paid >= due)
            return false;

        warning = $"Đã trả {FormatMoney(paid)} / {FormatMoney(due)}. Còn thiếu {FormatMoney(due - paid)}.";
        return true;
    }

    private void EnqueueOnce(
        string key,
        EventPopupStyle style,
        string eyebrow,
        string title,
        string message,
        string details,
        Action onClosed = null)
    {
        if (string.IsNullOrWhiteSpace(key) || shownEventKeys.Contains(key))
            return;

        shownEventKeys.Add(key);
        pendingPopups.Enqueue(new EventPopupData(style, eyebrow, title, message, details, onClosed));

        if (!isShowing)
            ShowNext();
    }

    private void ShowNext()
    {
        BuildUi();

        if (rootRect == null)
            return;

        if (pendingPopups.Count == 0)
        {
            HideImmediate();
            return;
        }

        EventPopupData data = pendingPopups.Dequeue();
        currentPopup = data;
        ApplyStyle(data.Style);
        ApplyLayout(data.Style);

        eyebrowText.text = data.Eyebrow;
        titleText.text = data.Title;
        messageText.text = data.Message;
        detailText.text = data.Details;

        rootRect.SetAsLastSibling();
        rootRect.gameObject.SetActive(true);
        isShowing = true;
    }

    private void HideCurrent()
    {
        Action onClosed = currentPopup?.OnClosed;
        currentPopup = null;
        onClosed?.Invoke();

        if (pendingPopups.Count > 0)
        {
            ShowNext();
            return;
        }

        HideImmediate();
    }

    private void HideImmediate()
    {
        isShowing = false;
        currentPopup = null;

        if (rootRect != null)
            rootRect.gameObject.SetActive(false);
    }

    private void BuildUi()
    {
        if (rootRect != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[GameEventPopupUI] Canvas not found.");
            return;
        }

        GameObject rootObject = new GameObject("Panel_GameEventPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        overlayImage = rootObject.GetComponent<Image>();
        overlayImage.color = new Color(0.02f, 0.04f, 0.05f, 0.66f);
        overlayImage.raycastTarget = true;

        GameObject cardObject = new GameObject("Card_GameEvent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.SetParent(rootRect, false);
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(860f, 500f);

        cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.96f, 0.94f, 0.9f, 0.99f);
        cardImage.raycastTarget = true;

        headerImage = CreatePanelImage("Img_EventHeader", cardRect, new Color(0.85f, 0.05f, 0.24f, 1f));
        SetRect(headerImage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 108f));

        eyebrowText = CreateText("Txt_EventEyebrow", cardRect, "", 18f, FontStyles.Bold);
        eyebrowText.alignment = TextAlignmentOptions.Center;
        SetRect(eyebrowText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-36f, -24f), new Vector2(-170f, 26f));

        titleText = CreateText("Txt_EventTitle", cardRect, "", 38f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 26f;
        titleText.fontSizeMax = 40f;
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-36f, -64f), new Vector2(-170f, 50f));

        messageText = CreateText("Txt_EventMessage", cardRect, "", 28f, FontStyles.Normal);
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableWordWrapping = true;
        messageText.lineSpacing = 3f;
        SetStretch(messageText.rectTransform, 76f, 148f, 76f, 238f);

        detailText = CreateText("Txt_EventDetails", cardRect, "", 20f, FontStyles.Normal);
        detailText.alignment = TextAlignmentOptions.TopLeft;
        detailText.enableWordWrapping = true;
        detailText.lineSpacing = 3f;
        SetStretch(detailText.rectTransform, 86f, 292f, 86f, 96f);

        closeButton = CreateButton("Btn_CloseEventPopup", cardRect, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -24f), new Vector2(64f, 56f));
        closeButton.onClick.AddListener(HideCurrent);

        TextMeshProUGUI closeText = closeButton.GetComponentInChildren<TextMeshProUGUI>();

        if (closeText != null)
            closeText.fontSize = 34f;

        primaryButton = CreateButton("Btn_ContinueEventPopup", cardRect, "Tiếp tục", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(230f, 58f));
        primaryButton.onClick.AddListener(HideCurrent);
    }

    private void ApplyStyle(EventPopupStyle style)
    {
        Color headerColor;
        Color cardColor = new Color(0.96f, 0.94f, 0.9f, 0.99f);
        Color bodyColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        switch (style)
        {
            case EventPopupStyle.WoodenCard:
                headerColor = new Color(0.58f, 0.32f, 0.15f, 1f);
                break;
            case EventPopupStyle.Bankruptcy:
                headerColor = new Color(0.84f, 0.05f, 0.16f, 1f);
                break;
            case EventPopupStyle.Debt:
                headerColor = new Color(0.94f, 0.45f, 0.08f, 1f);
                break;
            case EventPopupStyle.Victory:
                headerColor = new Color(1f, 0.74f, 0.18f, 1f);
                cardColor = new Color(0f, 0f, 0f, 0f);
                bodyColor = new Color(1f, 0.86f, 0.46f, 1f);
                break;
            case EventPopupStyle.Defeat:
                headerColor = new Color(0.44f, 0.18f, 0.95f, 0.88f);
                cardColor = new Color(0f, 0f, 0f, 0f);
                bodyColor = new Color(0.82f, 0.75f, 1f, 1f);
                break;
            default:
                headerColor = new Color(0.08f, 0.45f, 0.68f, 1f);
                break;
        }

        if (headerImage != null)
            headerImage.color = headerColor;

        if (cardImage != null)
            cardImage.color = cardColor;

        Color headerTextColor = GetReadableTextColor(headerColor);
        eyebrowText.color = headerTextColor;
        titleText.color = headerTextColor;
        messageText.color = bodyColor;
        detailText.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        if (primaryButton != null && primaryButton.targetGraphic != null)
            primaryButton.targetGraphic.color = headerColor;
    }

    private void ApplyLayout(EventPopupStyle style)
    {
        bool isEndScreen = style == EventPopupStyle.Victory || style == EventPopupStyle.Defeat;

        if (isEndScreen)
        {
            ApplyEndScreenLayout(style);
            return;
        }

        ApplyDefaultPopupLayout();
    }

    private void ApplyDefaultPopupLayout()
    {
        overlayImage.color = new Color(0.02f, 0.04f, 0.05f, 0.66f);
        cardImage.raycastTarget = true;
        SetRect(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 500f));
        SetRect(headerImage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 108f));
        SetRect(eyebrowText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-36f, -24f), new Vector2(-170f, 26f));
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-36f, -64f), new Vector2(-170f, 50f));
        SetStretch(messageText.rectTransform, 76f, 148f, 76f, 238f);
        SetStretch(detailText.rectTransform, 86f, 292f, 86f, 96f);
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -24f), new Vector2(64f, 56f));
        SetRect(primaryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(230f, 58f));

        closeButton.gameObject.SetActive(true);
        primaryButton.GetComponent<Image>().raycastTarget = true;
        primaryButton.GetComponent<Image>().color = new Color(0.08f, 0.45f, 0.68f, 1f);

        TextMeshProUGUI primaryText = primaryButton.GetComponentInChildren<TextMeshProUGUI>();
        if (primaryText != null)
        {
            primaryText.text = "Tiep tuc";
            primaryText.fontSize = 22f;
            primaryText.color = Color.white;
        }

        eyebrowText.fontSize = 18f;
        eyebrowText.fontStyle = FontStyles.Bold;
        eyebrowText.alignment = TextAlignmentOptions.Center;
        eyebrowText.outlineWidth = 0f;
        eyebrowText.characterSpacing = 0f;
        titleText.fontSizeMax = 40f;
        titleText.fontSizeMin = 26f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.outlineWidth = 0f;
        titleText.characterSpacing = 0f;
        messageText.fontSize = 28f;
        messageText.fontStyle = FontStyles.Normal;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.outlineWidth = 0f;
        messageText.characterSpacing = 0f;
        detailText.fontSize = 20f;
        detailText.alignment = TextAlignmentOptions.TopLeft;
        detailText.outlineWidth = 0f;
    }

    private void ApplyEndScreenLayout(EventPopupStyle style)
    {
        bool isVictory = style == EventPopupStyle.Victory;
        Color accent = isVictory
            ? new Color(1f, 0.73f, 0.16f, 1f)
            : new Color(0.58f, 0.28f, 1f, 0.9f);
        Color titleColor = isVictory
            ? new Color(1f, 0.82f, 0.34f, 1f)
            : new Color(0.93f, 0.9f, 1f, 1f);

        overlayImage.color = isVictory
            ? new Color(0.02f, 0.02f, 0.01f, 0.72f)
            : new Color(0.02f, 0.01f, 0.08f, 0.78f);

        SetStretch(cardRect, 0f, 0f, 0f, 0f);
        SetRect(headerImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 188f));
        SetRect(eyebrowText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 122f), new Vector2(-120f, 42f));
        SetRect(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(-80f, 138f));
        SetRect(messageText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -116f), new Vector2(-120f, 62f));
        SetRect(detailText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(-120f, 34f));
        SetStretch(primaryButton.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        closeButton.gameObject.SetActive(false);
        headerImage.color = isVictory
            ? new Color(0.34f, 0.22f, 0.03f, 0.32f)
            : accent;
        cardImage.color = Color.clear;
        cardImage.raycastTarget = false;

        Image primaryImage = primaryButton.GetComponent<Image>();
        primaryImage.color = new Color(0f, 0f, 0f, 0.01f);
        primaryImage.raycastTarget = true;

        TextMeshProUGUI primaryText = primaryButton.GetComponentInChildren<TextMeshProUGUI>();
        if (primaryText != null)
        {
            primaryText.text = "";
        }

        eyebrowText.text = "";
        titleText.color = titleColor;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableAutoSizing = true;
        eyebrowText.fontSize = 36f;
        eyebrowText.fontStyle = FontStyles.Bold;
        eyebrowText.alignment = TextAlignmentOptions.Center;
        eyebrowText.color = isVictory
            ? new Color(1f, 0.78f, 0.22f, 1f)
            : new Color(0.9f, 0.84f, 1f, 1f);
        eyebrowText.outlineWidth = 0.16f;
        eyebrowText.outlineColor = new Color(0.04f, 0.02f, 0.08f, 0.95f);

        titleText.fontSizeMin = 78f;
        titleText.fontSizeMax = 112f;
        titleText.characterSpacing = 8f;
        titleText.outlineWidth = isVictory ? 0.34f : 0.42f;
        titleText.outlineColor = isVictory
            ? new Color(0.95f, 0.55f, 0.04f, 0.95f)
            : new Color(0.16f, 0.03f, 0.52f, 1f);

        messageText.fontSize = 38f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.outlineWidth = 0.14f;
        messageText.outlineColor = new Color(0.04f, 0.02f, 0.08f, 0.9f);
        messageText.color = isVictory
            ? new Color(1f, 0.9f, 0.5f, 1f)
            : new Color(0.88f, 0.82f, 1f, 1f);
        detailText.fontSize = 20f;
        detailText.alignment = TextAlignmentOptions.Center;
        detailText.color = new Color(0.82f, 0.82f, 0.82f, 0.95f);
    }

    private Image CreatePanelImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.45f, 0.68f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Text", rect, label, 22f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
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

    private RankingEntryData FindLocalRanking(GameOverData gameOver)
    {
        string localUsername = PlayerSession.Instance?.Username ?? "";

        if (gameOver?.Rankings == null || string.IsNullOrWhiteSpace(localUsername))
            return null;

        foreach (RankingEntryData entry in gameOver.Rankings)
        {
            if (entry == null)
                continue;

            if (string.Equals(entry.DisplayName, localUsername, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.UserId, localUsername, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private string BuildRankingDetails(List<RankingEntryData> rankings)
    {
        if (rankings == null || rankings.Count == 0)
            return "Server chưa gửi dữ liệu xếp hạng.";

        StringBuilder builder = new StringBuilder();

        foreach (RankingEntryData entry in rankings)
        {
            if (entry == null)
                continue;

            string name = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.UserId : entry.DisplayName;
            builder.Append("#")
                .Append(entry.Rank)
                .Append("  ")
                .Append(ShortName(name))
                .Append("  +")
                .Append(entry.ScoreEarned)
                .Append(" điểm")
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private string FindActionMessage(GameStateData state, string username, string keyword)
    {
        if (state == null)
            return "";

        if (ContainsUsername(state.LastActionMessage, username) &&
            ContainsIgnoreDiacritics(state.LastActionMessage, keyword))
        {
            return state.LastActionMessage;
        }

        if (state.ActionLog == null)
            return "";

        for (int i = state.ActionLog.Count - 1; i >= 0; i--)
        {
            string line = state.ActionLog[i];

            if (ContainsUsername(line, username) && ContainsIgnoreDiacritics(line, keyword))
                return line;
        }

        return "";
    }

    private int CountOwnedProperties(GameStateData state, int playerIndex)
    {
        if (state?.Properties == null)
            return 0;

        int count = 0;

        foreach (KeyValuePair<int, GamePropertyStateData> pair in state.Properties)
        {
            if (pair.Value != null && pair.Value.OwnerPlayerIndex == playerIndex)
                count++;
        }

        return count;
    }

    private GamePlayerStateData GetLocalPlayer(GameStateData state)
    {
        if (state?.Players == null)
            return null;

        string username = PlayerSession.Instance?.Username ?? "";

        foreach (GamePlayerStateData player in state.Players)
        {
            if (player != null && string.Equals(player.Username, username, StringComparison.OrdinalIgnoreCase))
                return player;
        }

        return null;
    }

    private bool IsLocalPlayer(GamePlayerStateData player)
    {
        return player != null && IsLocalUsername(player.Username);
    }

    private bool IsLocalUsername(string username)
    {
        string localUsername = PlayerSession.Instance?.Username ?? "";
        return !string.IsNullOrWhiteSpace(username) &&
            string.Equals(username, localUsername, StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsUsername(string text, string username)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(username))
            return false;

        if (text.IndexOf(username, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return text.IndexOf(ShortName(username), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool ContainsIgnoreDiacritics(string text, string keyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            return false;

        return NormalizeForSearch(text).Contains(NormalizeForSearch(keyword));
    }

    private string NormalizeForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (c == 'đ' || c == 'Đ')
            {
                builder.Append('d');
                continue;
            }

            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private long ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        string digits = Regex.Replace(value, @"[^\d-]", "");
        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)
            ? result
            : 0;
    }

    private string FormatMoney(long amount)
    {
        return amount >= 0 ? $"${amount:N0}" : $"-${Math.Abs(amount):N0}";
    }

    private string ShortName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Player";

        int atIndex = username.IndexOf("@", StringComparison.Ordinal);

        if (atIndex > 0)
            username = username.Substring(0, atIndex);

        return username.Length > 18 ? username.Substring(0, 18) : username;
    }

    private Color GetReadableTextColor(Color background)
    {
        float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
        return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.06f, 1f) : Color.white;
    }

    private enum EventPopupStyle
    {
        WoodenCard,
        Bankruptcy,
        Debt,
        Victory,
        Defeat
    }

    private sealed class EventPopupData
    {
        public readonly EventPopupStyle Style;
        public readonly string Eyebrow;
        public readonly string Title;
        public readonly string Message;
        public readonly string Details;
        public readonly Action OnClosed;

        public EventPopupData(EventPopupStyle style, string eyebrow, string title, string message, string details, Action onClosed)
        {
            Style = style;
            Eyebrow = eyebrow ?? "";
            Title = title ?? "";
            Message = message ?? "";
            Details = details ?? "";
            OnClosed = onClosed;
        }
    }
}
