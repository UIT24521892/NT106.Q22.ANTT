using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHandUI : MonoBehaviour
{
    private RectTransform root;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI statusText;
    private readonly List<Button> cardButtons = new List<Button>();

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
        bool hasStatus = localPlayer.IsFreeRentShieldActive ||
            (state != null &&
             state.IsWaitingForCardChoice &&
             string.Equals(state.PendingCardPlayerUsername, localPlayer.Username, StringComparison.OrdinalIgnoreCase));

        root.gameObject.SetActive(cards.Count > 0 || hasStatus);

        if (!root.gameObject.activeSelf)
            return;

        titleText.text = "The dang giu";

        if (statusText != null)
        {
            statusText.text = localPlayer.IsFreeRentShieldActive
                ? "Khien mien tru dang kich hoat"
                : (hasStatus ? GetFriendlyPrompt(state.PendingCardEffectCode) : "");
        }

        bool canUseAnyCard = CanUseCards(state, localPlayer);

        for (int i = 0; i < cardButtons.Count; i++)
        {
            Button button = cardButtons[i];

            if (i >= cards.Count)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            CardDisplay card = cards[i];
            bool canUseCard = canUseAnyCard && CanUseCardNow(card.EffectCode, state, localPlayer);

            button.gameObject.SetActive(true);
            button.interactable = canUseCard;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (NetworkManager.Instance != null)
                    NetworkManager.Instance.SendUseCardRequest(card.EffectCode);
            });

            Image buttonImage = button.GetComponent<Image>();

            if (buttonImage != null)
            {
                buttonImage.color = canUseCard
                    ? new Color(0.12f, 0.26f, 0.34f, 0.96f)
                    : new Color(0.12f, 0.12f, 0.12f, 0.72f);
            }

            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null)
            {
                buttonText.text = card.Name;
                buttonText.color = card.Color;
            }
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
        root.sizeDelta = new Vector2(280f, 282f);

        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = new Color(0.05f, 0.07f, 0.09f, 0.72f);
        rootImage.raycastTarget = false;

        titleText = CreateText("Txt_PlayerHandTitle", root, "", 18f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.color = new Color(1f, 0.86f, 0.42f, 1f);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(-24f, 28f));

        for (int i = 0; i < 8; i++)
        {
            Button cardButton = CreateButton(
                $"Btn_PlayerHandCard_{i + 1}",
                root,
                "",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(12f, -42f - i * 27f),
                new Vector2(-24f, 25f));

            cardButtons.Add(cardButton);
        }

        statusText = CreateText("Txt_PlayerHandStatus", root, "", 12f, FontStyles.Normal);
        statusText.alignment = TextAlignmentOptions.TopLeft;
        statusText.color = new Color(0.82f, 0.9f, 1f, 1f);
        statusText.enableWordWrapping = false;
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(12f, 8f), new Vector2(-24f, 22f));

        root.gameObject.SetActive(false);
    }

    private List<CardDisplay> BuildHeldCards(GamePlayerStateData player)
    {
        List<CardDisplay> cards = new List<CardDisplay>();
        Color cardColor = new Color(1f, 0.78f, 0.18f, 1f);

        if (player.HasFreeRentCard)
            cards.Add(new CardDisplay("Khien Mien Tru", "FREE_RENT", cardColor));

        if (player.HasEscapeIslandCard)
            cards.Add(new CardDisplay("Truc Thang Cuu Ho", "ESCAPE_ISLAND", cardColor));

        if (player.HasFlightCard)
            cards.Add(new CardDisplay("Ve May Bay", "FLIGHT", cardColor));

        if (player.HasFreeUpgradeCard)
            cards.Add(new CardDisplay("Xay Dung Mien Phi", "FREE_UPGRADE", cardColor));

        if (player.HasForceDoubleCard)
            cards.Add(new CardDisplay("Xuc Xac Ma Thuat", "FORCE_DOUBLE", cardColor));

        if (player.HasEarthquakeCard)
            cards.Add(new CardDisplay("Dong Dat", "EARTHQUAKE", cardColor));

        if (player.HasPowerOutageCard)
            cards.Add(new CardDisplay("Cup Dien", "POWER_OUTAGE", cardColor));

        if (player.HasMoveChampionshipCard)
            cards.Add(new CardDisplay("Dang Cai Giai Dau", "MOVE_CHAMPIONSHIP", cardColor));

        return cards;
    }

    private string GetFriendlyPrompt(string effectCode)
    {
        switch (effectCode)
        {
            case "WORLD_TOUR": return "Chon diem den Du Lich The Gioi";
            case "WORLD_CHAMPIONSHIP_HOST": return "Chon noi dang cai Giai Vo Dich";
            case "FLIGHT": return "Chon diem den cho Ve May Bay";
            case "FREE_UPGRADE": return "Chon dat de Nang Cap Mien Phi";
            case "EARTHQUAKE": return "Chon dat doi thu de dung Dong Dat";
            case "POWER_OUTAGE": return "Chon dat doi thu de Cup Dien";
            case "MOVE_CHAMPIONSHIP": return "Chon dat cua ban de doi Giai Vo Dich";
            default: return $"Chon muc tieu cho the {effectCode}";
        }
    }

    private bool CanUseCards(GameStateData state, GamePlayerStateData localPlayer)
    {
        return state != null &&
            localPlayer != null &&
            !state.IsFinished &&
            !state.IsWaitingForCardChoice &&
            !state.IsWaitingForPropertySale &&
            localPlayer.IsConnected &&
            !localPlayer.IsBankrupt &&
            localPlayer.PlayerIndex == state.CurrentTurnPlayerIndex;
    }

    private bool CanUseCardNow(string effectCode, GameStateData state, GamePlayerStateData localPlayer)
    {
        if (!CanUseCards(state, localPlayer))
            return false;

        switch (effectCode)
        {
            case "ESCAPE_ISLAND":
                return localPlayer.IsOnIsland || localPlayer.JailTurnsLeft > 0;
            case "FORCE_DOUBLE":
                return !state.HasRolledThisTurn;
            default:
                return true;
        }
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
        image.color = new Color(0.12f, 0.26f, 0.34f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Text", rect, label, 14f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, 0f));
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

    private sealed class CardDisplay
    {
        public readonly string Name;
        public readonly string EffectCode;
        public readonly Color Color;

        public CardDisplay(string name, string effectCode, Color color)
        {
            Name = name;
            EffectCode = effectCode;
            Color = color;
        }
    }
}
