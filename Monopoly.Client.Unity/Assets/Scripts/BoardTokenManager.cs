using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoardTokenManager : MonoBehaviour
{
    private const int BoardSquareCount = 32;

    [SerializeField] private RectTransform tokenLayer;
    [SerializeField] private float tokenSize = 68f;
    [SerializeField] private float stepDuration = 0.18f;
    [SerializeField] private float bobHeight = 16f;
    [SerializeField] private bool reversePathDirection = true;

    private readonly Dictionary<int, TokenView> tokensByPlayerIndex = new Dictionary<int, TokenView>();
    private readonly Dictionary<int, int> lastPositionsByPlayerIndex = new Dictionary<int, int>();
    private readonly List<Vector2> boardPath = new List<Vector2>();

    private Sprite circleSprite;
    private RectTransform canvasRect;
    private string lastAnimatedMoveKey = "";

    public static BoardTokenManager EnsureExists()
    {
        BoardTokenManager existing = FindObjectOfType<BoardTokenManager>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("BoardTokenManager");
        return host.AddComponent<BoardTokenManager>();
    }

    private void Start()
    {
        Initialize();
        ApplyState(GameSession.CurrentState, false);
    }

    private void Update()
    {
        ApplyState(GameSession.CurrentState, true);
    }

    private void Initialize()
    {
        if (tokenLayer != null && boardPath.Count == BoardSquareCount)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[BoardTokenManager] Canvas not found.");
            return;
        }

        canvasRect = canvas.transform as RectTransform;

        if (tokenLayer == null)
            tokenLayer = CreateTokenLayer(canvasRect);

        tokenLayer.SetAsLastSibling();

        if (circleSprite == null)
            circleSprite = CreateCircleSprite(64, Color.white);

        BuildBoardPathFromTileButtons(canvas);
    }

    private RectTransform CreateTokenLayer(RectTransform parent)
    {
        GameObject layerObject = new GameObject("Runtime_PlayerTokens", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void BuildBoardPathFromTileButtons(Canvas canvas)
    {
        boardPath.Clear();

        if (TryBuildManualBoardPath(canvas))
            return;

        Button[] buttons = FindBoardTileButtons(out bool fromBoardContainer);
        List<Vector2> points = new List<Vector2>();

        foreach (Button button in buttons)
        {
            if (!IsBoardTileButton(button, fromBoardContainer))
                continue;

            RectTransform rect = button.transform as RectTransform;

            if (rect == null)
                continue;

            Vector2 localPoint = WorldToTokenLayerPoint(rect, canvas);
            points.Add(localPoint);
        }

        if (points.Count < BoardSquareCount)
        {
            Debug.LogWarning($"[BoardTokenManager] Expected {BoardSquareCount} board tiles, found {points.Count}.");
            return;
        }

        SortPathClockwise(points);

        while (points.Count > BoardSquareCount)
            points.RemoveAt(points.Count - 1);

        int startIndex = FindBottomMostPointIndex(points);
        Rotate(points, startIndex);

        if (reversePathDirection)
            ReversePathKeepingStart(points);

        boardPath.AddRange(points);
        Debug.Log($"[BoardTokenManager] Board path ready with {boardPath.Count} tiles.");
    }

    private bool TryBuildManualBoardPath(Canvas canvas)
    {
        List<Vector2> manualPoints = new List<Vector2>();

        for (int i = 0; i < BoardSquareCount; i++)
        {
            Transform marker = FindBoardPointMarker(i);

            if (marker == null)
                return false;

            if (marker is RectTransform markerRect)
            {
                manualPoints.Add(WorldToTokenLayerPoint(markerRect, canvas));
            }
            else
            {
                manualPoints.Add(WorldPositionToTokenLayerPoint(marker.position, canvas));
            }
        }

        boardPath.AddRange(manualPoints);
        Debug.Log("[BoardTokenManager] Manual board path ready from BoardPoint_00..31.");
        return true;
    }

    private Transform FindBoardPointMarker(int index)
    {
        string paddedIndex = index.ToString("00");
        string[] names =
        {
            $"BoardPoint_{paddedIndex}",
            $"BoardPoint_{index}",
            $"Tile_{paddedIndex}",
            $"Tile_{index}"
        };

        foreach (string markerName in names)
        {
            GameObject marker = GameObject.Find(markerName);

            if (marker != null)
                return marker.transform;
        }

        return null;
    }

    private Button[] FindBoardTileButtons(out bool fromBoardContainer)
    {
        GameObject boardContainer = GameObject.Find("BoardContainer");

        if (boardContainer != null)
        {
            Button[] boardButtons = boardContainer.GetComponentsInChildren<Button>(true);

            if (boardButtons.Length >= BoardSquareCount)
            {
                fromBoardContainer = true;
                return boardButtons;
            }
        }

        fromBoardContainer = false;
        return FindObjectsOfType<Button>(true);
    }

    private bool IsBoardTileButton(Button button, bool fromBoardContainer)
    {
        string objectName = button.gameObject.name.ToLowerInvariant();

        if (objectName.Contains("btn_") ||
            objectName.Contains("roll") ||
            objectName.Contains("buy") ||
            objectName.Contains("endturn") ||
            objectName.Contains("ready") ||
            objectName.Contains("start") ||
            objectName.Contains("leave"))
        {
            return false;
        }

        if (fromBoardContainer)
            return true;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text == null)
            return false;

        return string.Equals(text.text.Trim(), "Button", StringComparison.OrdinalIgnoreCase);
    }

    private Vector2 WorldToTokenLayerPoint(RectTransform rect, Canvas canvas)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 worldPosition = (corners[0] + corners[2]) * 0.5f;

        return WorldPositionToTokenLayerPoint(worldPosition, canvas);
    }

    private Vector2 WorldPositionToTokenLayerPoint(Vector3 worldPosition, Canvas canvas)
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tokenLayer, screenPoint, uiCamera, out Vector2 localPoint);
        return localPoint;
    }

    private void SortPathClockwise(List<Vector2> points)
    {
        Vector2 center = Vector2.zero;

        foreach (Vector2 point in points)
            center += point;

        center /= points.Count;

        points.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.y - center.y, a.x - center.x);
            float angleB = Mathf.Atan2(b.y - center.y, b.x - center.x);
            return angleA.CompareTo(angleB);
        });
    }

    private int FindBottomMostPointIndex(List<Vector2> points)
    {
        int index = 0;

        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].y < points[index].y)
                index = i;
        }

        return index;
    }

    private void Rotate(List<Vector2> points, int startIndex)
    {
        if (startIndex <= 0)
            return;

        List<Vector2> rotated = new List<Vector2>(points.Count);

        for (int i = 0; i < points.Count; i++)
            rotated.Add(points[(startIndex + i) % points.Count]);

        points.Clear();
        points.AddRange(rotated);
    }

    private void ReversePathKeepingStart(List<Vector2> points)
    {
        if (points.Count <= 2)
            return;

        Vector2 start = points[0];
        List<Vector2> tail = points.GetRange(1, points.Count - 1);
        tail.Reverse();

        points.Clear();
        points.Add(start);
        points.AddRange(tail);
    }

    private void ApplyState(GameStateData state, bool animateChanges)
    {
        if (state == null || state.Players == null)
            return;

        Initialize();

        if (boardPath.Count != BoardSquareCount || tokenLayer == null)
            return;

        Dictionary<int, int> slotByPosition = BuildSlotByPosition(state.Players);

        foreach (GamePlayerStateData player in state.Players)
        {
            if (player == null || player.IsBankrupt)
                continue;

            TokenView token = GetOrCreateToken(player);
            int position = NormalizePosition(player.Position);
            Vector2 target = GetTokenTarget(position, GetSameTileSlot(player, slotByPosition));

            if (!lastPositionsByPlayerIndex.TryGetValue(player.PlayerIndex, out int oldPosition))
            {
                token.Rect.anchoredPosition = target;
                token.Rect.localScale = Vector3.one;
            }
            else if (oldPosition != position)
            {
                int slot = GetSameTileSlot(player, slotByPosition);
                List<Vector2> path = BuildAuthoritativeMovePath(state, player, oldPosition, position, slot);
                token.MoveRoutine = RestartMoveRoutine(token.MoveRoutine, AnimateToken(token, path, target));
            }
            else if (token.MoveRoutine == null)
            {
                token.Rect.anchoredPosition = target;
                token.Rect.localScale = Vector3.one;
            }

            token.SetOnlineState(player.IsConnected);
            lastPositionsByPlayerIndex[player.PlayerIndex] = position;
        }
    }

    private Dictionary<int, int> BuildSlotByPosition(List<GamePlayerStateData> players)
    {
        Dictionary<int, int> countByPosition = new Dictionary<int, int>();
        Dictionary<int, int> slotByPlayer = new Dictionary<int, int>();

        foreach (GamePlayerStateData player in players)
        {
            if (player == null || player.IsBankrupt)
                continue;

            int position = NormalizePosition(player.Position);
            countByPosition.TryGetValue(position, out int count);
            countByPosition[position] = count + 1;
            slotByPlayer[player.PlayerIndex] = count;
        }

        return slotByPlayer;
    }

    private int GetSameTileSlot(GamePlayerStateData player, Dictionary<int, int> slotByPlayer)
    {
        if (player == null)
            return 0;

        return slotByPlayer.TryGetValue(player.PlayerIndex, out int slot) ? slot : 0;
    }

    private TokenView GetOrCreateToken(GamePlayerStateData player)
    {
        if (tokensByPlayerIndex.TryGetValue(player.PlayerIndex, out TokenView existing))
        {
            existing.SetLabel($"P{player.PlayerIndex + 1}");
            return existing;
        }

        GameObject tokenObject = new GameObject($"Token_P{player.PlayerIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = tokenObject.GetComponent<RectTransform>();
        rect.SetParent(tokenLayer, false);
        rect.sizeDelta = new Vector2(tokenSize, tokenSize);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = tokenObject.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = GetPlayerColor(player.PlayerIndex);
        image.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = $"P{player.PlayerIndex + 1}";
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        TokenView token = new TokenView(rect, image, label);
        tokensByPlayerIndex[player.PlayerIndex] = token;
        return token;
    }

    private Coroutine RestartMoveRoutine(Coroutine currentRoutine, IEnumerator routine)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        return StartCoroutine(routine);
    }

    private List<Vector2> BuildAuthoritativeMovePath(
        GameStateData state,
        GamePlayerStateData player,
        int oldPosition,
        int finalPosition,
        int slot)
    {
        if (CanUseServerMove(state, player))
        {
            int fromPosition = NormalizePosition(state.LastMoveFromPosition);
            int toPosition = NormalizePosition(state.LastMoveToPosition);
            string moveKey = $"{state.TurnNumber}:{state.LastMovedPlayerIndex}:{fromPosition}:{toPosition}:{state.LastDiceTotal}";

            if (lastAnimatedMoveKey != moveKey)
            {
                lastAnimatedMoveKey = moveKey;
                return BuildMovePath(fromPosition, toPosition, slot);
            }
        }

        return BuildMovePath(oldPosition, finalPosition, slot);
    }

    private bool CanUseServerMove(GameStateData state, GamePlayerStateData player)
    {
        return state != null &&
            player != null &&
            state.LastMovedPlayerIndex == player.PlayerIndex &&
            state.LastDiceTotal > 0 &&
            state.LastMoveFromPosition >= 0 &&
            state.LastMoveToPosition >= 0;
    }

    private IEnumerator AnimateToken(TokenView token, List<Vector2> path, Vector2 finalTarget)
    {
        token.Rect.SetAsLastSibling();

        foreach (Vector2 target in path)
        {
            Vector2 start = token.Rect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < stepDuration)
            {
                float t = Mathf.Clamp01(elapsed / stepDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                Vector2 position = Vector2.Lerp(start, target, eased);
                position.y += Mathf.Sin(t * Mathf.PI) * bobHeight;
                token.Rect.anchoredPosition = position;
                token.Rect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);

                elapsed += Time.deltaTime;
                yield return null;
            }

            token.Rect.anchoredPosition = target;
        }

        token.Rect.localScale = Vector3.one;
        token.Rect.anchoredPosition = finalTarget;
        token.MoveRoutine = null;
    }

    private List<Vector2> BuildMovePath(int fromPosition, int toPosition, int slot)
    {
        List<Vector2> path = new List<Vector2>();
        int current = NormalizePosition(fromPosition);
        int target = NormalizePosition(toPosition);
        int guard = 0;

        while (current != target && guard < BoardSquareCount + 1)
        {
            current = NormalizePosition(current + 1);
            path.Add(GetTokenTarget(current, slot));
            guard++;
        }

        if (path.Count == 0)
            path.Add(GetTokenTarget(target, slot));

        return path;
    }

    private Vector2 GetTokenTarget(int position, int slot)
    {
        Vector2 basePoint = boardPath[NormalizePosition(position)];
        Vector2[] offsets =
        {
            new Vector2(-15f, 12f),
            new Vector2(15f, 12f),
            new Vector2(-15f, -12f),
            new Vector2(15f, -12f)
        };

        return basePoint + offsets[Mathf.Clamp(slot, 0, offsets.Length - 1)];
    }

    private int NormalizePosition(int position)
    {
        int normalized = position % BoardSquareCount;

        if (normalized < 0)
            normalized += BoardSquareCount;

        return normalized;
    }

    private Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0:
                return new Color(0.9f, 0.15f, 0.12f);
            case 1:
                return new Color(0.1f, 0.35f, 0.95f);
            case 2:
                return new Color(0.05f, 0.65f, 0.25f);
            case 3:
                return new Color(0.95f, 0.65f, 0.08f);
            default:
                return new Color(0.55f, 0.25f, 0.9f);
        }
    }

    private Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 1f);
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private sealed class TokenView
    {
        public readonly RectTransform Rect;
        private readonly Image image;
        private readonly TextMeshProUGUI label;
        private readonly Color normalColor;

        public Coroutine MoveRoutine;

        public TokenView(RectTransform rect, Image image, TextMeshProUGUI label)
        {
            Rect = rect;
            this.image = image;
            this.label = label;
            normalColor = image.color;
        }

        public void SetLabel(string value)
        {
            if (label != null)
                label.text = value;
        }

        public void SetOnlineState(bool isConnected)
        {
            if (image == null)
                return;

            image.color = isConnected ? normalColor : new Color(normalColor.r, normalColor.g, normalColor.b, 0.45f);
        }
    }
}
