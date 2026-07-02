using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;

public class PlayerInfoLayerUI : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;
    private static readonly Color PlayerNameColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color PlayerDetailColor = new Color(0.88f, 0.96f, 1f, 1f);
    private static readonly Color TurnTimerTrackColor = new Color(0f, 0f, 0f, 0.32f);
    private static readonly Color TurnTimerFillColor = new Color(0.1f, 0.72f, 1f, 0.95f);
    private static readonly Color TurnTimerDangerColor = new Color(0.95f, 0.18f, 0.12f, 0.95f);

    private readonly Dictionary<int, PlayerCard> cardsByPlayerIndex = new Dictionary<int, PlayerCard>();

    private RectTransform root;
    private float nextRefreshTime;
    private bool usingScenePanels;

    private static readonly Dictionary<string, Sprite> avatarCache = new Dictionary<string, Sprite>();

    private Image FindOrCreateAvatarImage(Transform parent)
    {
        Transform found = parent.Find("Img_Avatar");
        bool created = false;

        if (found == null)
        {
            GameObject avatarObject = new GameObject(
                "Img_Avatar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            avatarObject.transform.SetParent(parent, false);
            found = avatarObject.transform;
            created = true;
        }

        Image image = found.GetComponent<Image>();

        if (image == null)
            image = found.gameObject.AddComponent<Image>();

        image.raycastTarget = false;
        image.preserveAspect = true;

        if (created)
        {
            RectTransform rect = image.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(45f, 0f);
            rect.sizeDelta = new Vector2(64f, 64f);
        }

        return image;
    }

    private static Sprite LoadAvatarSprite(string avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId))
            avatarId = "avatar_1";

        if (avatarCache.TryGetValue(avatarId, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>($"Avatars/{avatarId}");

        if (sprite == null && avatarId != "avatar_1")
            sprite = Resources.Load<Sprite>("Avatars/avatar_1");

        avatarCache[avatarId] = sprite;
        return sprite;
    }

    private static string GetAvatarId(GamePlayerStateData player)
    {
        if (player != null && !string.IsNullOrWhiteSpace(player.AvatarId))
            return player.AvatarId;

        PlayerSlotData slot = GameSession.Players?.FirstOrDefault(
            p => string.Equals(p.Username, player?.Username, StringComparison.OrdinalIgnoreCase)
        );

        return string.IsNullOrWhiteSpace(slot?.AvatarId) ? "avatar_1" : slot.AvatarId;
    }

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

            Image layerImage = existingLayer.GetComponent<Image>();

            if (layerImage != null)
            {
                Color color = layerImage.color;
                color.a = 0f;
                layerImage.color = color;
                layerImage.raycastTarget = false;
            }

            if (TryBindScenePlayerPanels(parent))
            {
                root = existingLayer.transform as RectTransform;
                usingScenePanels = true;
                return;
            }

            HideExistingChildren(parent);
        }

        GameObject rootObject = new GameObject("Runtime_PlayerInfoCorners", typeof(RectTransform), typeof(CanvasRenderer));
        root = rootObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
    }

    private bool TryBindScenePlayerPanels(Transform layer)
    {
        cardsByPlayerIndex.Clear();

        for (int i = 0; i < 4; i++)
        {
            Transform panel = FindChildRecursive(layer, $"Player{i + 1}_Panel");

            if (panel == null)
                return false;

            panel.gameObject.SetActive(false);
            cardsByPlayerIndex[i] = BindScenePlayerCard(panel.gameObject, i);
        }

        return true;
    }

    private PlayerCard BindScenePlayerCard(GameObject panelObject, int playerIndex)
    {
        RectTransform rect = panelObject.transform as RectTransform;
        Image background = panelObject.GetComponent<Image>();

        TextMeshProUGUI[] texts = panelObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI nameText = GetOrCreatePanelText(panelObject.transform, texts, 0, "Txt_Name", 16f, FontStyles.Bold);
        TextMeshProUGUI detailText = GetOrCreatePanelText(panelObject.transform, texts, 1, "Txt_Detail", 13f, FontStyles.Normal);
        TextMeshProUGUI badgeText = GetOrCreatePanelText(panelObject.transform, texts, 2, "Txt_Badge", 13f, FontStyles.Bold);

        for (int i = 3; texts != null && i < texts.Length; i++)
            texts[i].gameObject.SetActive(false);

        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        detailText.alignment = TextAlignmentOptions.MidlineLeft;
        badgeText.alignment = TextAlignmentOptions.MidlineRight;

        nameText.fontSize = 14f;
        detailText.fontSize = 10.5f;
        badgeText.fontSize = 11.5f;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 10f;
        nameText.fontSizeMax = 14f;
        detailText.enableAutoSizing = true;
        detailText.fontSizeMin = 8f;
        detailText.fontSizeMax = 10.5f;
        badgeText.enableAutoSizing = true;
        badgeText.fontSizeMin = 9f;
        badgeText.fontSizeMax = 11.5f;

        SetStretch(nameText.rectTransform, 100f, 4f, 6f, 62f);
        SetStretch(detailText.rectTransform, 100f, 34f, 6f, 34f);
        SetStretch(badgeText.rectTransform, 100f, 68f, 6f, 4f);

        Image avatarImage = FindOrCreateAvatarImage(panelObject.transform);
        CreateOrBindTurnTimerBar(panelObject.transform, true, out Image timerTrack, out Image timerFill);

        return new PlayerCard(
            panelObject,
            rect,
            background,
            nameText,
            detailText,
            badgeText,
            avatarImage,
            timerTrack,
            timerFill,
            keepSceneLayout: true
        );
    }

    private TextMeshProUGUI GetOrCreatePanelText(
        Transform parent,
        TextMeshProUGUI[] existingTexts,
        int index,
        string name,
        float fontSize,
        FontStyles style)
    {
        if (existingTexts != null && index >= 0 && index < existingTexts.Length && existingTexts[index] != null)
        {
            existingTexts[index].gameObject.name = name;
            existingTexts[index].fontSize = fontSize;
            existingTexts[index].fontStyle = style;
            existingTexts[index].enableWordWrapping = false;
            existingTexts[index].overflowMode = TextOverflowModes.Ellipsis;
            existingTexts[index].raycastTarget = false;
            return existingTexts[index];
        }

        TextMeshProUGUI text = CreateText(name, parent, fontSize, style);

        if (index == 0)
            SetStretch(text.rectTransform, 10f, 6f, 72f, 44f);
        else if (index == 1)
            SetStretch(text.rectTransform, 10f, 32f, 10f, 8f);
        else
            SetAnchored(text.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(62f, 24f));

        return text;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);

            if (nested != null)
                return nested;
        }

        return null;
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
        if (root == null && !usingScenePanels)
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
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 10f;
        nameText.fontSizeMax = 16f;
        SetStretch(nameText.rectTransform, 14f, 6f, 72f, 44f);

        TextMeshProUGUI detailText = CreateText("Txt_Detail", cardRect, 13f, FontStyles.Normal);
        detailText.color = new Color(0.9f, 0.96f, 1f, 1f);
        detailText.alignment = TextAlignmentOptions.MidlineLeft;
        detailText.enableAutoSizing = true;
        detailText.fontSizeMin = 9f;
        detailText.fontSizeMax = 13f;
        SetStretch(detailText.rectTransform, 14f, 32f, 10f, 8f);

        TextMeshProUGUI badgeText = CreateText("Txt_Badge", cardRect, 13f, FontStyles.Bold);
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.enableAutoSizing = true;
        badgeText.fontSizeMin = 9f;
        badgeText.fontSizeMax = 13f;
        SetAnchored(
            badgeText.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-8f, -8f),
            new Vector2(62f, 24f)
        );

        Image avatarImage = FindOrCreateAvatarImage(cardObject.transform);
        CreateOrBindTurnTimerBar(cardObject.transform, false, out Image timerTrack, out Image timerFill);

        PlayerCard card = new PlayerCard(
            cardObject,
            cardRect,
            background,
            nameText,
            detailText,
            badgeText,
            avatarImage,
            timerTrack,
            timerFill,
            keepSceneLayout: false
        );

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

    private void CreateOrBindTurnTimerBar(Transform parent, bool scenePanelLayout, out Image track, out Image fill)
    {
        Transform existingTrack = parent.Find("Img_TurnTimerTrack");

        if (existingTrack == null)
        {
            GameObject trackObject = new GameObject("Img_TurnTimerTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackObject.transform.SetParent(parent, false);
            existingTrack = trackObject.transform;
        }

        track = existingTrack.GetComponent<Image>();
        if (track == null)
            track = existingTrack.gameObject.AddComponent<Image>();

        track.color = TurnTimerTrackColor;
        track.raycastTarget = false;

        RectTransform trackRect = track.rectTransform;
        if (scenePanelLayout)
            SetStretch(trackRect, 100f, 72f, 8f, 2f);
        else
            SetStretch(trackRect, 14f, 70f, 10f, 2f);

        Transform existingFill = existingTrack.Find("Img_TurnTimerFill");

        if (existingFill == null)
        {
            GameObject fillObject = new GameObject("Img_TurnTimerFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(existingTrack, false);
            existingFill = fillObject.transform;
        }

        fill = existingFill.GetComponent<Image>();
        if (fill == null)
            fill = existingFill.gameObject.AddComponent<Image>();

        fill.color = TurnTimerFillColor;
        fill.raycastTarget = false;

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.pivot = new Vector2(0f, 0.5f);

        track.gameObject.SetActive(false);
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

    private static int GetRemainingTurnSeconds(GameStateData state)
    {
        long remainingTicks = GetRemainingTurnTicks(state);
        return Mathf.Max(0, Mathf.CeilToInt(remainingTicks / (float)TimeSpan.TicksPerSecond));
    }

    private static float GetTurnProgress(GameStateData state)
    {
        if (state == null || state.TurnDurationSeconds <= 0)
            return 0f;

        return Mathf.Clamp01(GetRemainingTurnTicks(state) / (float)(state.TurnDurationSeconds * TimeSpan.TicksPerSecond));
    }

    private static long GetRemainingTurnTicks(GameStateData state)
    {
        if (state == null || state.IsFinished || state.TurnEndsAtUtcTicks <= 0)
            return 0;

        long referenceTicks = state.IsPaused && state.PauseStartedAtUtcTicks > 0
            ? state.PauseStartedAtUtcTicks
            : NetworkManager.Instance != null ? NetworkManager.Instance.EstimatedServerNowTicks : DateTime.UtcNow.Ticks;

        return Math.Max(0, state.TurnEndsAtUtcTicks - referenceTicks);
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
        private readonly bool keepSceneLayout;
        private readonly Image avatarImage;
        private readonly Image timerTrack;
        private readonly Image timerFill;

        public PlayerCard(
            GameObject root,
            RectTransform rect,
            Image background,
            TextMeshProUGUI nameText,
            TextMeshProUGUI detailText,
            TextMeshProUGUI badgeText,
            Image avatarImage,
            Image timerTrack,
            Image timerFill,
            bool keepSceneLayout)
        {
            Root = root;
            this.rect = rect;
            this.background = background;
            this.nameText = nameText;
            this.detailText = detailText;
            this.badgeText = badgeText;
            this.keepSceneLayout = keepSceneLayout;
            this.avatarImage = avatarImage;
            this.timerTrack = timerTrack;
            this.timerFill = timerFill;
        }

        public void SetCorner(CornerLayout layout)
        {
            if (keepSceneLayout || rect == null)
                return;

            rect.anchorMin = layout.AnchorMin;
            rect.anchorMax = layout.AnchorMax;
            rect.pivot = layout.Pivot;
            rect.anchoredPosition = layout.Position;
        }

        public void Update(GamePlayerStateData player, GameStateData state, bool isLocal, int ownedCount)
        {
            if (avatarImage != null)
            {
                avatarImage.sprite = PlayerInfoLayerUI.LoadAvatarSprite(
                    PlayerInfoLayerUI.GetAvatarId(player)
                );

                avatarImage.enabled = avatarImage.sprite != null;
                avatarImage.preserveAspect = true;
            }

            bool isCurrentTurn = player.PlayerIndex == state.CurrentTurnPlayerIndex && !state.IsFinished;
            string status = player.IsBankrupt ? "BANKRUPT" : player.IsConnected ? "ACTIVE" : "OFFLINE";
            int remainingTurnSeconds = PlayerInfoLayerUI.GetRemainingTurnSeconds(state);
            string badge = isCurrentTurn ? $"{remainingTurnSeconds}s" : status;

            nameText.text = $"{(isLocal ? "You - " : "")}P{player.PlayerIndex + 1} {ShortName(player.Username)}";
            detailText.text = $"{FormatMoney(player.Money)} | Pos {player.Position} | Land {ownedCount}";
            badgeText.text = badge;
            nameText.color = PlayerNameColor;
            detailText.color = PlayerDetailColor;

            if (player.IsBankrupt)
            {
                if (background != null)
                    background.color = new Color(0.38f, 0.04f, 0.04f, 0.68f);
                badgeText.color = new Color(1f, 0.55f, 0.55f, 1f);
            }
            else if (!player.IsConnected)
            {
                if (background != null)
                    background.color = new Color(0.12f, 0.12f, 0.12f, 0.54f);
                badgeText.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            }
            else if (isCurrentTurn)
            {
                if (background != null)
                    background.color = new Color(0.95f, 0.62f, 0.08f, 0.38f);
                badgeText.color = new Color(1f, 0.88f, 0.25f, 1f);
            }
            else
            {
                if (background != null)
                    background.color = new Color(0f, 0f, 0f, 0.46f);
                badgeText.color = new Color(0.62f, 0.9f, 1f, 1f);
            }

            UpdateTurnTimerBar(isCurrentTurn, state);
        }

        private void UpdateTurnTimerBar(bool isCurrentTurn, GameStateData state)
        {
            if (timerTrack == null || timerFill == null)
                return;

            bool visible = isCurrentTurn && state != null && !state.IsPaused && state.TurnEndsAtUtcTicks > 0 && state.TurnDurationSeconds > 0;
            timerTrack.gameObject.SetActive(visible);

            if (!visible)
                return;

            float progress = PlayerInfoLayerUI.GetTurnProgress(state);
            RectTransform fillRect = timerFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(progress, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            timerFill.color = progress <= 0.25f ? TurnTimerDangerColor : TurnTimerFillColor;
        }
    }
}
