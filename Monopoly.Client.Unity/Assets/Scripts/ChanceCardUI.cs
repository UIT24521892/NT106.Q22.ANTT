using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChanceCardUI : MonoBehaviour
{
    private RectTransform popupRoot;
    private Image panelImage;
    private Image headerImage;
    private TextMeshProUGUI cardTypeText;
    private TextMeshProUGUI cardNameText;
    private TextMeshProUGUI drawnByText;
    private TextMeshProUGUI effectText;
    private Button closeButton;
    private Coroutine hideRoutine;
    private Coroutine revealRoutine;

    public static ChanceCardUI EnsureExists()
    {
        ChanceCardUI existing = FindObjectOfType<ChanceCardUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("ChanceCardUI");
        return host.AddComponent<ChanceCardUI>();
    }

    private void Awake()
    {
        BuildUi();
    }

    public void ShowCard(string drawnByUsername, string cardId, string cardName, string cardType, string detailEffect)
    {
        BuildUi();

        panelImage.color = new Color(0.96f, 0.94f, 0.9f, 0.99f);

        cardTypeText.text = "CO HOI";
        cardNameText.text = "Dang rut the...";
        drawnByText.text = $"Drawn by: {ShortName(drawnByUsername)}";
        effectText.text = "Gold / Silver / Wood";

        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealCardAfterShuffle(drawnByUsername, cardId, cardName, cardType, detailEffect));
    }

    private void BuildUi()
    {
        if (popupRoot != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[ChanceCardUI] Canvas not found.");
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        GameObject rootObject = new GameObject("Panel_ChanceCardPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        popupRoot = rootObject.GetComponent<RectTransform>();
        popupRoot.SetParent(canvasRect, false);
        popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
        popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        popupRoot.pivot = new Vector2(0.5f, 0.5f);
        popupRoot.anchoredPosition = new Vector2(0f, 60f);
        popupRoot.sizeDelta = new Vector2(560f, 360f);

        panelImage = rootObject.GetComponent<Image>();
        panelImage.color = new Color(0.96f, 0.94f, 0.9f, 0.99f);
        panelImage.raycastTarget = true;

        headerImage = CreateImage("Img_ChanceCardHeader", popupRoot, GetCardTypeColor("Golden"));
        SetRect(headerImage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 92f));

        cardTypeText = CreateText("Txt_CardType", popupRoot, "", 16f, FontStyles.Bold);
        cardTypeText.alignment = TextAlignmentOptions.Center;
        cardTypeText.enableWordWrapping = false;
        cardTypeText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(cardTypeText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-34f, -18f), new Vector2(-120f, 28f));

        cardNameText = CreateText("Txt_CardName", popupRoot, "", 28f, FontStyles.Bold);
        cardNameText.alignment = TextAlignmentOptions.Center;
        cardNameText.enableWordWrapping = true;
        cardNameText.overflowMode = TextOverflowModes.Ellipsis;
        cardNameText.enableAutoSizing = true;
        cardNameText.fontSizeMin = 20f;
        cardNameText.fontSizeMax = 30f;
        SetRect(cardNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-34f, -56f), new Vector2(-120f, 40f));

        drawnByText = CreateText("Txt_CardDrawnBy", popupRoot, "", 15f, FontStyles.Normal);
        drawnByText.alignment = TextAlignmentOptions.Center;
        drawnByText.enableWordWrapping = false;
        drawnByText.overflowMode = TextOverflowModes.Ellipsis;
        SetStretch(drawnByText.rectTransform, 42f, 118f, 42f, 202f);

        effectText = CreateText("Txt_CardEffect", popupRoot, "", 18f, FontStyles.Normal);
        effectText.alignment = TextAlignmentOptions.Center;
        effectText.enableWordWrapping = true;
        effectText.overflowMode = TextOverflowModes.Ellipsis;
        effectText.lineSpacing = 2f;
        SetStretch(effectText.rectTransform, 52f, 168f, 52f, 82f);

        closeButton = CreateButton("Btn_CloseChanceCard", popupRoot, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 38f));
        closeButton.onClick.AddListener(Hide);

        popupRoot.gameObject.SetActive(false);
    }

    private IEnumerator RevealCardAfterShuffle(string drawnByUsername, string cardId, string cardName, string cardType, string detailEffect)
    {
        string[] types = { "Golden", "Silver", "Wooden" };
        float duration = 1.45f;
        float elapsed = 0f;
        int index = 0;

        while (elapsed < duration)
        {
            string type = types[index % types.Length];
            ApplyCardTypeStyle(type);
            cardTypeText.text = $"{GetCardTypeDisplayName(type)} CARD";
            cardNameText.text = "Dang rut the...";
            effectText.text = "Dang xao bai...";

            index++;
            elapsed += 0.12f;
            yield return new WaitForSecondsRealtime(0.12f);
        }

        ApplyCardTypeStyle(cardType);
        cardTypeText.text = string.IsNullOrWhiteSpace(cardType) ? "CHANCE CARD" : $"{GetCardTypeDisplayName(cardType)} CARD";
        cardNameText.text = string.IsNullOrWhiteSpace(cardName) ? "Unknown Card" : cardName;
        drawnByText.text = $"Drawn by: {ShortName(drawnByUsername)}";
        effectText.text = string.IsNullOrWhiteSpace(detailEffect) ? cardId : detailEffect;
        revealRoutine = null;
        hideRoutine = StartCoroutine(AutoHideAfterDelay());
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(4.5f);
        Hide();
    }

    private void Hide()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
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
        image.color = new Color(0.86f, 0.18f, 0.15f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Text", rect, label, 18f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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

    private Color GetCardTypeColor(string cardType)
    {
        switch ((cardType ?? "").Trim().ToLowerInvariant())
        {
            case "golden":
                return new Color(1f, 0.78f, 0.18f, 1f);
            case "silver":
                return new Color(0.75f, 0.84f, 0.92f, 1f);
            case "wooden":
                return new Color(0.76f, 0.48f, 0.25f, 1f);
            default:
                return new Color(1f, 0.86f, 0.42f, 1f);
        }
    }

    private void ApplyCardTypeStyle(string cardType)
    {
        Color accentColor = GetCardTypeColor(cardType);
        Color textColor = GetReadableHeaderTextColor(accentColor);

        if (headerImage != null)
            headerImage.color = accentColor;

        cardTypeText.color = textColor;
        cardNameText.color = textColor;
        drawnByText.color = new Color(0.16f, 0.16f, 0.16f, 1f);
        effectText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
    }

    private string GetCardTypeDisplayName(string cardType)
    {
        switch ((cardType ?? "").Trim().ToLowerInvariant())
        {
            case "golden": return "GOLD";
            case "silver": return "SILVER";
            case "wooden": return "WOOD";
            default: return "CHANCE";
        }
    }

    private Color GetReadableHeaderTextColor(Color background)
    {
        float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
        return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.06f, 1f) : Color.white;
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
}
