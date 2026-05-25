using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHandUI : MonoBehaviour
{
    private RectTransform root;
    private TextMeshProUGUI titleText;
    private readonly List<TextMeshProUGUI> cardTexts = new List<TextMeshProUGUI>();

    public static PlayerHandUI EnsureExists()
    {
        PlayerHandUI existing = FindObjectOfType<PlayerHandUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("PlayerHandUI");
        return host.AddComponent<PlayerHandUI>();
    }

    private void Awake()
    {
        BuildUi();
    }

    public void Refresh(GameStateData state)
    {
        BuildUi();

        if (root == null)
            return;

        GamePlayerStateData localPlayer = GetLocalPlayer(state);

        if (localPlayer == null)
        {
            root.gameObject.SetActive(false);
            return;
        }

        List<CardDisplay> cards = BuildHeldCards(localPlayer);
        root.gameObject.SetActive(cards.Count > 0);

        if (cards.Count == 0)
            return;

        titleText.text = "Thẻ đang giữ";

        for (int i = 0; i < cardTexts.Count; i++)
        {
            if (i >= cards.Count)
            {
                cardTexts[i].gameObject.SetActive(false);
                continue;
            }

            CardDisplay card = cards[i];
            cardTexts[i].gameObject.SetActive(true);
            cardTexts[i].text = card.Name;
            cardTexts[i].color = card.Color;
        }

        root.SetAsLastSibling();
    }

    private void BuildUi()
    {
        if (root != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[PlayerHandUI] Canvas not found.");
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        GameObject rootObject = new GameObject("Panel_PlayerHand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root = rootObject.GetComponent<RectTransform>();
        root.SetParent(canvasRect, false);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = new Vector2(12f, 74f);
        root.sizeDelta = new Vector2(260f, 180f);

        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = new Color(0.05f, 0.07f, 0.09f, 0.62f);
        rootImage.raycastTarget = false;

        titleText = CreateText("Txt_PlayerHandTitle", root, "", 18f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.color = new Color(1f, 0.86f, 0.42f, 1f);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(-24f, 28f));

        for (int i = 0; i < 5; i++)
        {
            TextMeshProUGUI cardText = CreateText($"Txt_PlayerHandCard_{i + 1}", root, "", 15f, FontStyles.Bold);
            cardText.alignment = TextAlignmentOptions.TopLeft;
            SetRect(cardText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(14f, -42f - i * 25f), new Vector2(-28f, 24f));
            cardTexts.Add(cardText);
        }

        root.gameObject.SetActive(false);
    }

    private List<CardDisplay> BuildHeldCards(GamePlayerStateData player)
    {
        List<CardDisplay> cards = new List<CardDisplay>();

        if (player.HasFreeRentCard)
            cards.Add(new CardDisplay("Khiên Miễn Trừ", new Color(1f, 0.78f, 0.18f, 1f)));

        if (player.HasEscapeIslandCard)
            cards.Add(new CardDisplay("Trực Thăng Cứu Hộ", new Color(1f, 0.78f, 0.18f, 1f)));

        if (player.HasFlightCard)
            cards.Add(new CardDisplay("Vé Máy Bay", new Color(1f, 0.78f, 0.18f, 1f)));

        if (player.HasFreeUpgradeCard)
            cards.Add(new CardDisplay("Xây Dựng Miễn Phí", new Color(1f, 0.78f, 0.18f, 1f)));

        if (player.HasForceDoubleCard)
            cards.Add(new CardDisplay("Xúc Xắc Ma Thuật", new Color(1f, 0.78f, 0.18f, 1f)));

        return cards;
    }

    private GamePlayerStateData GetLocalPlayer(GameStateData state)
    {
        string username = PlayerSession.Instance?.Username ?? "";

        if (state == null || state.Players == null || string.IsNullOrWhiteSpace(username))
            return null;

        foreach (GamePlayerStateData player in state.Players)
        {
            if (player != null && string.Equals(player.Username, username, StringComparison.OrdinalIgnoreCase))
                return player;
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
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private sealed class CardDisplay
    {
        public readonly string Name;
        public readonly Color Color;

        public CardDisplay(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}
