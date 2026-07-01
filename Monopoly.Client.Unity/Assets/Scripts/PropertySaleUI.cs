using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PropertySaleUI : MonoBehaviour
{
    private const string DebtAlertSpritePath = "khongdutien";
    private static PropertySaleUI instance;

    private RectTransform rootRect;
    private RectTransform alertRect;
    private RectTransform cardRect;
    private RectTransform contentRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI debtText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI loanHintText;
    private string activeDebtKey = "";
    private bool isSaleListVisible;
    private readonly List<GameObject> rowObjects = new List<GameObject>();

    public static PropertySaleUI EnsureExists()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("PropertySaleUI");
        instance = host.AddComponent<PropertySaleUI>();
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
        Hide();
    }

    public void Refresh(GameStateData state)
    {
        BuildUi();

        if (rootRect == null)
            return;

        GamePlayerStateData localPlayer = GetLocalPlayer(state);
        bool shouldShow = state != null &&
            localPlayer != null &&
            state.IsWaitingForPropertySale &&
            string.Equals(state.PendingSalePlayerUsername, localPlayer.Username, StringComparison.OrdinalIgnoreCase);

        if (!shouldShow)
        {
            activeDebtKey = "";
            isSaleListVisible = false;
            Hide();
            return;
        }

        titleText.text = "Bán tài sản để trả nợ";
        debtText.text = $"Còn nợ: {FormatMoney(state.PendingDebtAmount)}  |  Lý do: {state.PendingDebtReason}";
        hintText.text = "Chọn tài sản muốn bán. Tiền thu được sẽ trả khoản nợ trước, phần dư tự cộng vào tiền mặt.";

        string debtKey = $"{state.RoomId}:{state.PendingSalePlayerIndex}:{state.PendingDebtAmount}:{state.PendingDebtReason}";

        if (!string.Equals(activeDebtKey, debtKey, StringComparison.Ordinal))
        {
            activeDebtKey = debtKey;
            isSaleListVisible = false;
        }

        RebuildRows(state);
        ApplyVisibleMode();
        rootRect.SetAsLastSibling();
        rootRect.gameObject.SetActive(true);
    }

    private void RebuildRows(GameStateData state)
    {
        foreach (GameObject rowObject in rowObjects)
        {
            if (rowObject != null)
                Destroy(rowObject);
        }

        rowObjects.Clear();

        if (state?.PendingSalePropertyPositions == null || state.Properties == null)
            return;

        const float rowHeight = 62f;
        const float spacing = 10f;
        float y = -10f;
        int rowIndex = 0;

        foreach (int position in state.PendingSalePropertyPositions)
        {
            if (!state.Properties.TryGetValue(position, out GamePropertyStateData property) || property == null)
                continue;

            long saleValue = GetSaleValue(property);
            string level = DescribeLevel(property);
            int capturedPosition = position;

            Button button = CreateButton(
                $"Btn_SellProperty_{position:00}",
                contentRect,
                "",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, y),
                new Vector2(-24f, rowHeight)
            );
            button.onClick.AddListener(() => SellProperty(capturedPosition));

            Image image = button.targetGraphic as Image;

            if (image != null)
                image.color = rowIndex % 2 == 0
                    ? new Color(0.98f, 0.98f, 0.96f, 1f)
                    : new Color(0.9f, 0.94f, 0.96f, 1f);

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = $"Ô {position:00}  {property.Name}  |  {level}  |  Thu về {FormatMoney(saleValue)}";
                text.fontSize = 21f;
                text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                SetStretch(text.rectTransform, 22f, 0f, 18f, 0f);
            }

            rowObjects.Add(button.gameObject);
            y -= rowHeight + spacing;
            rowIndex++;
        }

        float contentHeight = Math.Max(360f, rowObjects.Count * (rowHeight + spacing) + 16f);
        contentRect.sizeDelta = new Vector2(0f, contentHeight);
    }

    private void SellProperty(int positionIndex)
    {
        if (NetworkManager.Instance == null)
            return;

        foreach (GameObject rowObject in rowObjects)
        {
            Button button = rowObject != null ? rowObject.GetComponent<Button>() : null;

            if (button != null)
                button.interactable = false;
        }

        NetworkManager.Instance.SendSellPropertyForDebtRequest(positionIndex);
    }

    private void BuildUi()
    {
        if (rootRect != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[PropertySaleUI] Canvas not found.");
            return;
        }

        GameObject rootObject = new GameObject("Panel_PropertySale", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = rootObject.GetComponent<Image>();
        overlay.color = new Color(0.02f, 0.04f, 0.05f, 0.68f);
        overlay.raycastTarget = true;

        BuildDebtAlertUi();

        GameObject cardObject = new GameObject("Card_PropertySale", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.SetParent(rootRect, false);
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(920f, 620f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.95f, 0.93f, 0.88f, 0.99f);
        cardImage.raycastTarget = true;

        Image header = CreatePanelImage("Img_PropertySaleHeader", cardRect, new Color(0.92f, 0.43f, 0.08f, 1f));
        SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 94f));

        titleText = CreateText("Txt_PropertySaleTitle", cardRect, "", 36f, FontStyles.Bold);
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-72f, 54f));

        debtText = CreateText("Txt_PropertySaleDebt", cardRect, "", 22f, FontStyles.Bold);
        debtText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        debtText.alignment = TextAlignmentOptions.Center;
        SetRect(debtText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(-72f, 34f));

        hintText = CreateText("Txt_PropertySaleHint", cardRect, "", 18f, FontStyles.Normal);
        hintText.color = new Color(0.26f, 0.26f, 0.26f, 1f);
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.enableWordWrapping = true;
        SetRect(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(-120f, 44f));

        BuildScrollArea();
        ApplyVisibleMode();
    }

    private void BuildDebtAlertUi()
    {
        GameObject alertObject = new GameObject("Card_NoMoneyAlert", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        alertRect = alertObject.GetComponent<RectTransform>();
        alertRect.SetParent(rootRect, false);
        alertRect.anchorMin = new Vector2(0.5f, 0.5f);
        alertRect.anchorMax = new Vector2(0.5f, 0.5f);
        alertRect.pivot = new Vector2(0.5f, 0.5f);
        alertRect.anchoredPosition = Vector2.zero;
        alertRect.sizeDelta = new Vector2(1500f, 900f);

        Image alertImage = alertObject.GetComponent<Image>();
        alertImage.sprite = Resources.Load<Sprite>(DebtAlertSpritePath);
        alertImage.color = Color.white;
        alertImage.preserveAspect = true;
        alertImage.raycastTarget = true;

        Button sellButton = CreateHotspotButton("Btn_NoMoneySellAssets", alertRect, new Vector2(0.5f, 0f), new Vector2(-330f, 80f), new Vector2(300f, 92f));
        sellButton.onClick.AddListener(ShowSaleList);

        Button loanButton = CreateHotspotButton("Btn_NoMoneyLoan", alertRect, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(300f, 92f));
        loanButton.interactable = false;

        Button continueButton = CreateHotspotButton("Btn_NoMoneyContinue", alertRect, new Vector2(0.5f, 0f), new Vector2(330f, 80f), new Vector2(300f, 92f));
        continueButton.onClick.AddListener(ShowSaleList);

        loanHintText = CreateText("Txt_NoMoneyLoanHint", alertRect, "", 18f, FontStyles.Bold);
        loanHintText.alignment = TextAlignmentOptions.Center;
        loanHintText.color = new Color(1f, 0.92f, 0.28f, 0.96f);
        SetRect(loanHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(520f, 34f));
    }

    private void ShowSaleList()
    {
        isSaleListVisible = true;
        ApplyVisibleMode();
    }

    private void ApplyVisibleMode()
    {
        if (alertRect != null)
            alertRect.gameObject.SetActive(!isSaleListVisible);

        if (cardRect != null)
            cardRect.gameObject.SetActive(isSaleListVisible);

        if (loanHintText != null)
            loanHintText.text = !isSaleListVisible ? "Tinh nang vay tien chua duoc mo." : "";
    }

    private void BuildScrollArea()
    {
        GameObject scrollObject = new GameObject("Scroll_SaleProperties", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.SetParent(cardRect, false);
        SetStretch(scrollRectTransform, 70f, 216f, 70f, 52f);

        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(0.78f, 0.82f, 0.84f, 0.35f);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.SetParent(scrollRectTransform, false);
        SetStretch(viewportRect, 0f, 0f, 0f, 0f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.05f);

        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.SetParent(viewportRect, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 360f);

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
    }

    private void Hide()
    {
        if (rootRect != null)
            rootRect.gameObject.SetActive(false);
    }

    private long GetSaleValue(GamePropertyStateData property)
    {
        if (property == null || property.BuyPrice <= 0 || (property.Type != "City" && property.Type != "Resort"))
            return 0;

        long saleValue = Math.Max(1, property.BuyPrice / 2);

        if (property.Type == "City")
        {
            long houseBuildCost = Math.Max(1, property.BuyPrice / 2);
            saleValue += property.HouseCount * Math.Max(1, houseBuildCost / 2);

            if (property.HasHotel)
                saleValue += Math.Max(1, property.BuyPrice / 2);
        }

        return saleValue;
    }

    private string DescribeLevel(GamePropertyStateData property)
    {
        if (property == null)
            return "";

        if (property.HasHotel)
            return "Khách sạn";

        if (property.HouseCount > 0)
            return $"{property.HouseCount} nhà";

        return property.Type == "Resort" ? "Resort" : "Đất trống";
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

    private string FormatMoney(long amount)
    {
        return amount >= 0 ? $"${amount:N0}" : $"-${Math.Abs(amount):N0}";
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
        image.color = new Color(0.95f, 0.95f, 0.93f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Text", rect, label, 20f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        SetStretch(text.rectTransform, 10f, 0f, 10f, 0f);
        return button;
    }

    private Button CreateHotspotButton(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, anchor, anchor, new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.01f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
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
}
