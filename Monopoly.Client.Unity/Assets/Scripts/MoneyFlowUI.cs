using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hiển thị 4 ô tiền ở 4 góc (mỗi người chơi một ô) và chạy hoạt ảnh "tiền bay":
/// khi một người chơi mất tiền, một chip tiền bay từ ô của họ vào tâm bàn cờ; khi một
/// người chơi nhận tiền, chip bay từ tâm bàn cờ ra ô của họ. Tâm bàn cờ là hub chung nên
/// xử lý được cả giao dịch player↔player lẫn player↔ngân hàng mà không cần ghép cặp.
/// </summary>
public class MoneyFlowUI : MonoBehaviour
{
    private const int MaxActiveChips = 24;
    private const long ChangeThreshold = 1; // bỏ qua thay đổi quá nhỏ

    private Canvas canvas;
    private Camera uiCamera;
    private RectTransform overlayLayer;

    private readonly Dictionary<int, MoneyBox> boxesByPlayerIndex = new Dictionary<int, MoneyBox>();
    private readonly Dictionary<int, long> prevMoneyByPlayerIndex = new Dictionary<int, long>();

    private Sprite boxSprite;
    private Sprite chipSprite;
    private int activeChipCount;
    private bool hasSnapshot;

    public static MoneyFlowUI EnsureExists()
    {
        MoneyFlowUI existing = FindObjectOfType<MoneyFlowUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("MoneyFlowUI");
        return host.AddComponent<MoneyFlowUI>();
    }

    public void NotifyStateUpdate(GameStateData state)
    {
        if (!EnsureBuilt())
            return;

        if (state == null || state.Players == null)
        {
            HideAllBoxes();
            return;
        }

        HashSet<int> activePlayers = new HashSet<int>();
        Dictionary<int, long> newMoney = new Dictionary<int, long>();

        for (int i = 0; i < state.Players.Count; i++)
        {
            GamePlayerStateData player = state.Players[i];

            if (player == null)
                continue;

            activePlayers.Add(player.PlayerIndex);
            newMoney[player.PlayerIndex] = player.Money;

            MoneyBox box = GetOrCreateBox(player.PlayerIndex);
            box.SetMoney(player.Money, player.IsBankrupt, IsLocalPlayer(player));
            box.Root.SetActive(true);
        }

        foreach (KeyValuePair<int, MoneyBox> pair in boxesByPlayerIndex)
        {
            if (!activePlayers.Contains(pair.Key))
                pair.Value.Root.SetActive(false);
        }

        // Chỉ animate khi đã có snapshot trước đó (tránh "bắn" chip lúc mới vào game / resume).
        if (hasSnapshot)
            AnimateMoneyChanges(state, newMoney);

        prevMoneyByPlayerIndex.Clear();
        foreach (KeyValuePair<int, long> pair in newMoney)
            prevMoneyByPlayerIndex[pair.Key] = pair.Value;

        hasSnapshot = true;
    }

    private void AnimateMoneyChanges(GameStateData state, Dictionary<int, long> newMoney)
    {
        if (!TryGetBoardCenterLocal(out Vector2 center))
            center = Vector2.zero;

        foreach (KeyValuePair<int, long> pair in newMoney)
        {
            if (!prevMoneyByPlayerIndex.TryGetValue(pair.Key, out long previous))
                continue; // người chơi mới xuất hiện -> không animate lần đầu

            long delta = pair.Value - previous;

            if (delta <= -ChangeThreshold)
            {
                // mất tiền: bay từ ô người chơi vào tâm
                if (TryGetBoxLocal(pair.Key, out Vector2 from))
                    SpawnChip(from, center, new Color(0.95f, 0.32f, 0.32f, 1f), 0f);
            }
            else if (delta >= ChangeThreshold)
            {
                // nhận tiền: bay từ tâm ra ô người chơi (delay nhẹ để đọc thành "vào rồi ra")
                if (TryGetBoxLocal(pair.Key, out Vector2 to))
                    SpawnChip(center, to, new Color(0.36f, 0.85f, 0.45f, 1f), 0.25f);
            }
        }
    }

    private void SpawnChip(Vector2 from, Vector2 to, Color tint, float delay)
    {
        if (overlayLayer == null || activeChipCount >= MaxActiveChips)
            return;

        StartCoroutine(FlyChip(from, to, tint, delay));
    }

    private IEnumerator FlyChip(Vector2 from, Vector2 to, Color tint, float delay)
    {
        activeChipCount++;

        GameObject chipObject = new GameObject("MoneyChip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = chipObject.GetComponent<RectTransform>();
        rect.SetParent(overlayLayer, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(40f, 30f);
        rect.anchoredPosition = from;

        Image image = chipObject.GetComponent<Image>();
        image.sprite = chipSprite;
        image.type = Image.Type.Sliced;
        image.color = tint;
        image.raycastTarget = false;

        TextMeshProUGUI label = CreateText("$", rect, 16f, FontStyles.Bold, new Color(1f, 1f, 1f, 1f));
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        const float duration = 0.55f;
        Vector2 direction = to - from;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
        float arc = Mathf.Min(direction.magnitude * 0.18f, 120f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector2 position = Vector2.Lerp(from, to, eased);
            position += perpendicular * (Mathf.Sin(t * Mathf.PI) * arc);
            rect.anchoredPosition = position;

            float scale = 0.7f + Mathf.Sin(t * Mathf.PI) * 0.5f;
            rect.localScale = Vector3.one * scale;
            image.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(1f - t * 0.35f));

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(chipObject);
        activeChipCount--;
    }

    private bool EnsureBuilt()
    {
        if (overlayLayer != null)
            return true;

        canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return false;

        uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (boxSprite == null)
            boxSprite = CreateRoundedRectSprite(64, 64, 18, Color.white);

        if (chipSprite == null)
            chipSprite = CreateRoundedRectSprite(48, 48, 16, Color.white);

        GameObject layerObject = new GameObject("Runtime_MoneyFlowLayer", typeof(RectTransform), typeof(CanvasRenderer));
        overlayLayer = layerObject.GetComponent<RectTransform>();
        overlayLayer.SetParent(canvas.transform, false);
        overlayLayer.anchorMin = Vector2.zero;
        overlayLayer.anchorMax = Vector2.one;
        overlayLayer.offsetMin = Vector2.zero;
        overlayLayer.offsetMax = Vector2.zero;
        overlayLayer.pivot = new Vector2(0.5f, 0.5f);
        overlayLayer.SetAsLastSibling();

        return true;
    }

    private MoneyBox GetOrCreateBox(int playerIndex)
    {
        if (boxesByPlayerIndex.TryGetValue(playerIndex, out MoneyBox existing))
            return existing;

        CornerLayout corner = GetCorner(playerIndex);

        GameObject boxObject = new GameObject($"MoneyBox_Player_{playerIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = boxObject.GetComponent<RectTransform>();
        rect.SetParent(overlayLayer, false);
        rect.anchorMin = corner.Anchor;
        rect.anchorMax = corner.Anchor;
        rect.pivot = corner.Pivot;
        rect.anchoredPosition = corner.Position;
        rect.sizeDelta = new Vector2(180f, 46f);

        Image background = boxObject.GetComponent<Image>();
        background.sprite = boxSprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.07f, 0.11f, 0.18f, 0.82f);
        background.raycastTarget = false;

        // Chip icon bên trái
        GameObject iconObject = new GameObject("Img_Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(rect, false);
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);
        iconRect.sizeDelta = new Vector2(34f, 26f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = chipSprite;
        icon.type = Image.Type.Sliced;
        icon.color = new Color(0.36f, 0.85f, 0.45f, 1f);
        icon.raycastTarget = false;

        TextMeshProUGUI iconLabel = CreateText("$", iconRect, 14f, FontStyles.Bold, Color.white);
        iconLabel.alignment = TextAlignmentOptions.Center;
        iconLabel.rectTransform.anchorMin = Vector2.zero;
        iconLabel.rectTransform.anchorMax = Vector2.one;
        iconLabel.rectTransform.offsetMin = Vector2.zero;
        iconLabel.rectTransform.offsetMax = Vector2.zero;

        // Số tiền
        TextMeshProUGUI amount = CreateText("0", rect, 18f, FontStyles.Bold, Color.white);
        amount.alignment = TextAlignmentOptions.MidlineRight;
        amount.enableAutoSizing = true;
        amount.fontSizeMin = 10f;
        amount.fontSizeMax = 18f;
        RectTransform amountRect = amount.rectTransform;
        amountRect.anchorMin = Vector2.zero;
        amountRect.anchorMax = Vector2.one;
        amountRect.offsetMin = new Vector2(48f, 4f);
        amountRect.offsetMax = new Vector2(-12f, -4f);

        MoneyBox box = new MoneyBox(boxObject, rect, background, amount);
        boxesByPlayerIndex[playerIndex] = box;
        return box;
    }

    private bool TryGetBoxLocal(int playerIndex, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (!boxesByPlayerIndex.TryGetValue(playerIndex, out MoneyBox box) || box.Rect == null)
            return false;

        return TryWorldToOverlayLocal(box.Rect.position, out localPoint);
    }

    private bool TryGetBoardCenterLocal(out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        BoardTokenManager tokenManager = FindObjectOfType<BoardTokenManager>();

        if (tokenManager == null || !tokenManager.TryGetBoardCenterWorldPosition(out Vector3 worldCenter))
            return false;

        return TryWorldToOverlayLocal(worldCenter, out localPoint);
    }

    private bool TryWorldToOverlayLocal(Vector3 worldPosition, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (overlayLayer == null)
            return false;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayLayer, screenPoint, uiCamera, out localPoint);
    }

    private CornerLayout GetCorner(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: // trên-trái, dưới thẻ player
                return new CornerLayout(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -118f));
            case 1: // trên-phải
                return new CornerLayout(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -118f));
            case 2: // dưới-trái
                return new CornerLayout(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 140f));
            case 3: // dưới-phải
                return new CornerLayout(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 140f));
            default:
                return new CornerLayout(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -118f - (playerIndex * 56f)));
        }
    }

    private void HideAllBoxes()
    {
        foreach (KeyValuePair<int, MoneyBox> pair in boxesByPlayerIndex)
            pair.Value.Root.SetActive(false);
    }

    private bool IsLocalPlayer(GamePlayerStateData player)
    {
        string username = PlayerSession.Instance?.Username ?? "";
        return player != null && string.Equals(player.Username, username, System.StringComparison.OrdinalIgnoreCase);
    }

    private TextMeshProUGUI CreateText(string content, Transform parent, float fontSize, FontStyles style, Color color)
    {
        GameObject textObject = new GameObject("Txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static string FormatMoney(long amount)
    {
        return amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
    }

    private static Sprite CreateRoundedRectSprite(int width, int height, int radius, Color color)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(color.r, color.g, color.b, 0f);
        int r = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = true;

                // bo 4 góc
                if (x < r && y < r)
                    inside = (new Vector2(r - x, r - y).sqrMagnitude <= r * r);
                else if (x >= width - r && y < r)
                    inside = (new Vector2(x - (width - 1 - r), r - y).sqrMagnitude <= r * r);
                else if (x < r && y >= height - r)
                    inside = (new Vector2(r - x, y - (height - 1 - r)).sqrMagnitude <= r * r);
                else if (x >= width - r && y >= height - r)
                    inside = (new Vector2(x - (width - 1 - r), y - (height - 1 - r)).sqrMagnitude <= r * r);

                texture.SetPixel(x, y, inside ? color : clear);
            }
        }

        texture.Apply();

        // 9-slice border = radius để giữ bo góc khi co giãn
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(r, r, r, r));
    }

    private readonly struct CornerLayout
    {
        public readonly Vector2 Anchor;
        public readonly Vector2 Pivot;
        public readonly Vector2 Position;

        public CornerLayout(Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            Anchor = anchor;
            Pivot = pivot;
            Position = position;
        }
    }

    private sealed class MoneyBox
    {
        public readonly GameObject Root;
        public readonly RectTransform Rect;
        private readonly Image background;
        private readonly TextMeshProUGUI amount;

        public MoneyBox(GameObject root, RectTransform rect, Image background, TextMeshProUGUI amount)
        {
            Root = root;
            Rect = rect;
            this.background = background;
            this.amount = amount;
        }

        public void SetMoney(long money, bool isBankrupt, bool isLocal)
        {
            amount.text = FormatMoney(money);

            if (isBankrupt)
            {
                background.color = new Color(0.32f, 0.05f, 0.05f, 0.82f);
                amount.color = new Color(1f, 0.55f, 0.55f, 1f);
            }
            else
            {
                background.color = isLocal
                    ? new Color(0.10f, 0.22f, 0.14f, 0.86f)
                    : new Color(0.07f, 0.11f, 0.18f, 0.82f);
                amount.color = money < 0 ? new Color(1f, 0.5f, 0.5f, 1f) : Color.white;
            }
        }
    }
}
