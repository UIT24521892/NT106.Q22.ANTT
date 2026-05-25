using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerInfoLayerUI : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private readonly Dictionary<int, PlayerCard> cardsByPlayerIndex = new Dictionary<int, PlayerCard>();

    private RectTransform root;
    private float nextRefreshTime;

    public static PlayerInfoLayerUI EnsureExists()
    {
        PlayerInfoLayerUI existing = FindObjectOfType<PlayerInfoLayerUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("PlayerInfoLayerUI");
        return host.AddComponent<PlayerInfoLayerUI>();
    }

    private void Start()
    {
        BuildUi();
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    private void BuildUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[PlayerInfoLayerUI] Canvas not found.");
            return;
        }

        Transform parent = canvas.transform;
        GameObject existingLayer = FindSceneObjectByName("PlayerInfoLayer");

        if (existingLayer != null)
        {
            existingLayer.SetActive(true);
            parent = existingLayer.transform;
            HideExistingChildren(parent);

            Image layerImage = existingLayer.GetComponent<Image>();

            if (layerImage != null)
            {
                Color color = layerImage.color;
                color.a = 0f;
                layerImage.color = color;
                layerImage.raycastTarget = false;
            }
        }

        GameObject rootObject = new GameObject("Runtime_PlayerInfoCorners", typeof(RectTransform), typeof(CanvasRenderer));
        root = rootObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
    }

    private void HideExistingChildren(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            parent.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void Refresh()
    {
        if (root == null)
            return;

        GameStateData state = GameSession.CurrentState;

        if (state == null || state.Players == null)
        {
            HideAllCards();
            return;
        }

        HashSet<int> activeCards = new HashSet<int>();

        for (int i = 0; i < state.Players.Count; i++)
        {
            GamePlayerStateData player = state.Players[i];

            if (player == null)
                continue;

            activeCards.Add(player.PlayerIndex);
            PlayerCard card = GetOrCreateCard(player.PlayerIndex);
            card.SetCorner(GetCorner(player.PlayerIndex));
            card.Update(player, state, IsLocalPlayer(player), CountOwnedProperties(state, player.PlayerIndex));
        }

        foreach (KeyValuePair<int, PlayerCard> pair in cardsByPlayerIndex)
        {
            pair.Value.Root.SetActive(activeCards.Contains(pair.Key));
        }
    }

    private void HideAllCards()
    {
        foreach (KeyValuePair<int, PlayerCard> pair in cardsByPlayerIndex)
        {
            pair.Value.Root.SetActive(false);
        }
    }

    private PlayerCard GetOrCreateCard(int playerIndex)
    {
        if (cardsByPlayerIndex.TryGetValue(playerIndex, out PlayerCard existing))
            return existing;

        GameObject cardObject = new GameObject($"Card_Player_{playerIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.SetParent(root, false);
        cardRect.sizeDelta = new Vector2(265f, 76f);

        Image background = cardObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.46f);
        background.raycastTarget = false;

        TextMeshProUGUI nameText = CreateText("Txt_Name", cardRect, 16f, FontStyles.Bold);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        SetStretch(nameText.rectTransform, 10f, 6f, 72f, 44f);

        TextMeshProUGUI detailText = CreateText("Txt_Detail", cardRect, 13f, FontStyles.Normal);
        detailText.color = new Color(0.9f, 0.96f, 1f, 1f);
        detailText.alignment = TextAlignmentOptions.MidlineLeft;
        SetStretch(detailText.rectTransform, 10f, 32f, 10f, 8f);

        TextMeshProUGUI badgeText = CreateText("Txt_Badge", cardRect, 13f, FontStyles.Bold);
        badgeText.alignment = TextAlignmentOptions.Center;
        SetAnchored(
            badgeText.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-8f, -8f),
            new Vector2(62f, 24f)
        );

        PlayerCard card = new PlayerCard(cardObject, cardRect, background, nameText, detailText, badgeText);
        cardsByPlayerIndex[playerIndex] = card;
        return card;
    }

    private CornerLayout GetCorner(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0:
                return new CornerLayout(Vector2.up, Vector2.up, Vector2.up, new Vector2(16f, -16f));
            case 1:
                return new CornerLayout(Vector2.one, Vector2.one, Vector2.one, new Vector2(-16f, -16f));
            case 2:
                return new CornerLayout(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(16f, 88f));
            case 3:
                return new CornerLayout(Vector2.right, Vector2.right, Vector2.right, new Vector2(-16f, 246f));
            default:
                return new CornerLayout(Vector2.up, Vector2.up, Vector2.up, new Vector2(16f, -16f - (playerIndex * 84f)));
        }
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void SetAnchored(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private bool IsLocalPlayer(GamePlayerStateData player)
    {
        string username = PlayerSession.Instance?.Username ?? "";
        return player != null && string.Equals(player.Username, username, StringComparison.OrdinalIgnoreCase);
    }

    private int CountOwnedProperties(GameStateData state, int playerIndex)
    {
        if (state.Properties == null)
            return 0;

        int count = 0;

        foreach (KeyValuePair<int, GamePropertyStateData> pair in state.Properties)
        {
            if (pair.Value != null && pair.Value.OwnerPlayerIndex == playerIndex)
                count++;
        }

        return count;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject candidate in objects)
        {
            if (candidate == null ||
                candidate.name != objectName ||
                !candidate.scene.IsValid() ||
                candidate.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static string ShortName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Player";

        int atIndex = username.IndexOf("@", StringComparison.Ordinal);

        if (atIndex > 0)
            username = username.Substring(0, atIndex);

        return username.Length > 12 ? username.Substring(0, 12) : username;
    }

    private static string FormatMoney(long amount)
    {
        if (amount >= 1000000)
            return $"${amount / 1000000f:0.#}M";

        if (amount >= 1000)
            return $"${amount / 1000f:0.#}K";

        return $"${amount:N0}";
    }

    private readonly struct CornerLayout
    {
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;
        public readonly Vector2 Pivot;
        public readonly Vector2 Position;

        public CornerLayout(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
            Pivot = pivot;
            Position = position;
        }
    }

    private sealed class PlayerCard
    {
        public readonly GameObject Root;
        private readonly RectTransform rect;
        private readonly Image background;
        private readonly TextMeshProUGUI nameText;
        private readonly TextMeshProUGUI detailText;
        private readonly TextMeshProUGUI badgeText;

        public PlayerCard(
            GameObject root,
            RectTransform rect,
            Image background,
            TextMeshProUGUI nameText,
            TextMeshProUGUI detailText,
            TextMeshProUGUI badgeText)
        {
            Root = root;
            this.rect = rect;
            this.background = background;
            this.nameText = nameText;
            this.detailText = detailText;
            this.badgeText = badgeText;
        }

        public void SetCorner(CornerLayout layout)
        {
            rect.anchorMin = layout.AnchorMin;
            rect.anchorMax = layout.AnchorMax;
            rect.pivot = layout.Pivot;
            rect.anchoredPosition = layout.Position;
        }

        public void Update(GamePlayerStateData player, GameStateData state, bool isLocal, int ownedCount)
        {
            bool isCurrentTurn = player.PlayerIndex == state.CurrentTurnPlayerIndex && !state.IsFinished;
            string status = player.IsBankrupt ? "BANKRUPT" : player.IsConnected ? "ACTIVE" : "OFFLINE";
            string badge = isCurrentTurn ? "TURN" : status;

            nameText.text = $"{(isLocal ? "You - " : "")}P{player.PlayerIndex + 1} {ShortName(player.Username)}";
            detailText.text = $"{FormatMoney(player.Money)} | Pos {player.Position} | Lands {ownedCount}";
            badgeText.text = badge;

            if (player.IsBankrupt)
            {
                background.color = new Color(0.38f, 0.04f, 0.04f, 0.68f);
                badgeText.color = new Color(1f, 0.55f, 0.55f, 1f);
            }
            else if (!player.IsConnected)
            {
                background.color = new Color(0.12f, 0.12f, 0.12f, 0.54f);
                badgeText.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            }
            else if (isCurrentTurn)
            {
                background.color = new Color(0.95f, 0.62f, 0.08f, 0.38f);
                badgeText.color = new Color(1f, 0.88f, 0.25f, 1f);
            }
            else
            {
                background.color = new Color(0f, 0f, 0f, 0.46f);
                badgeText.color = new Color(0.62f, 0.9f, 1f, 1f);
            }
        }
    }
}
