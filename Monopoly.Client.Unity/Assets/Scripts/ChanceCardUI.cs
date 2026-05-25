using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChanceCardUI : MonoBehaviour
{
    private RectTransform popupRoot;
    private Image panelImage;
    private TextMeshProUGUI cardTypeText;
    private TextMeshProUGUI cardNameText;
    private TextMeshProUGUI drawnByText;
    private TextMeshProUGUI effectText;
    private Button closeButton;
    private Coroutine hideRoutine;

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

        Color accentColor = GetCardTypeColor(cardType);
        panelImage.color = new Color(0.06f, 0.07f, 0.08f, 0.96f);
        cardTypeText.color = accentColor;
        cardNameText.color = accentColor;
        drawnByText.color = Color.white;
        effectText.color = Color.white;

        cardTypeText.text = string.IsNullOrWhiteSpace(cardType) ? "CHANCE CARD" : $"{cardType.ToUpperInvariant()} CARD";
        cardNameText.text = string.IsNullOrWhiteSpace(cardName) ? "Unknown Card" : cardName;
        drawnByText.text = $"Drawn by: {ShortName(drawnByUsername)}";
        effectText.text = string.IsNullOrWhiteSpace(detailEffect) ? cardId : detailEffect;

        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(AutoHideAfterDelay());
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
        popupRoot.sizeDelta = new Vector2(440f, 280f);

        panelImage = rootObject.GetComponent<Image>();
        panelImage.color = new Color(0.06f, 0.07f, 0.08f, 0.96f);
        panelImage.raycastTarget = true;

        cardTypeText = CreateText("Txt_CardType", popupRoot, "", 16f, FontStyles.Bold);
        cardTypeText.alignment = TextAlignmentOptions.TopLeft;
        SetRect(cardTypeText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -20f), new Vector2(-88f, 32f));

        cardNameText = CreateText("Txt_CardName", popupRoot, "", 28f, FontStyles.Bold);
        cardNameText.alignment = TextAlignmentOptions.TopLeft;
        cardNameText.enableWordWrapping = true;
        SetRect(cardNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -56f), new Vector2(-48f, 72f));

        drawnByText = CreateText("Txt_CardDrawnBy", popupRoot, "", 15f, FontStyles.Normal);
        drawnByText.alignment = TextAlignmentOptions.TopLeft;
        SetRect(drawnByText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -128f), new Vector2(-48f, 30f));

        effectText = CreateText("Txt_CardEffect", popupRoot, "", 18f, FontStyles.Normal);
        effectText.alignment = TextAlignmentOptions.TopLeft;
        effectText.enableWordWrapping = true;
        SetRect(effectText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(24f, -34f), new Vector2(-48f, -178f));

        closeButton = CreateButton("Btn_CloseChanceCard", popupRoot, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 38f));
        closeButton.onClick.AddListener(Hide);

        popupRoot.gameObject.SetActive(false);
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

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
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
