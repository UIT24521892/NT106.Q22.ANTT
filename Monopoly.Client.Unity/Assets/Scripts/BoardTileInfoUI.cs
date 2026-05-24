using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BoardTileInfoUI : MonoBehaviour
{
    private const int BoardSquareCount = 32;

    private readonly Dictionary<int, Button> buttonsByPosition = new Dictionary<int, Button>();
    private RectTransform popupRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Button closeButton;

    public static BoardTileInfoUI EnsureExists()
    {
        BoardTileInfoUI existing = FindObjectOfType<BoardTileInfoUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("BoardTileInfoUI");
        return host.AddComponent<BoardTileInfoUI>();
    }

    private void Start()
    {
        BuildPopupUi();
        RegisterBoardButtons();
    }

    private void BuildPopupUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[BoardTileInfoUI] Canvas not found.");
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        GameObject rootObject = new GameObject("Panel_BoardTileInfoPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        popupRoot = rootObject.GetComponent<RectTransform>();
        popupRoot.SetParent(canvasRect, false);
        popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
        popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        popupRoot.pivot = new Vector2(0.5f, 0.5f);
        popupRoot.anchoredPosition = Vector2.zero;
        popupRoot.sizeDelta = new Vector2(470f, 430f);

        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = new Color(0.07f, 0.08f, 0.09f, 0.94f);
        rootImage.raycastTarget = true;

        titleText = CreateText("Txt_TileTitle", popupRoot, "", 26f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.color = new Color(1f, 0.86f, 0.42f, 1f);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(20f, -18f), new Vector2(-74f, 54f));

        bodyText = CreateText("Txt_TileBody", popupRoot, "", 18f, FontStyles.Normal);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(-40f, -104f));

        closeButton = CreateButton("Btn_CloseTilePopup", popupRoot, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 38f));
        closeButton.onClick.AddListener(HidePopup);

        popupRoot.gameObject.SetActive(false);
    }

    private void RegisterBoardButtons()
    {
        GameObject boardContainer = FindSceneObjectByName("BoardContainer");

        if (boardContainer == null)
        {
            Debug.LogWarning("[BoardTileInfoUI] BoardContainer not found. Tile popup buttons are not registered.");
            return;
        }

        boardContainer.SetActive(true);
        MakeBoardContainerTransparent(boardContainer);

        Button[] buttons = boardContainer.GetComponentsInChildren<Button>(true);
        List<Button> boardButtons = new List<Button>();

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            boardButtons.Add(button);
        }

        if (boardButtons.Count < BoardSquareCount)
        {
            Debug.LogWarning($"[BoardTileInfoUI] Expected {BoardSquareCount} board buttons, found {boardButtons.Count}.");
            return;
        }

        Dictionary<int, Button> mappedButtons = TryMapButtonsByMarkers(boardButtons);

        if (mappedButtons.Count != BoardSquareCount)
            mappedButtons = MapButtonsByClockwiseSort(boardButtons);

        foreach (KeyValuePair<int, Button> entry in mappedButtons)
        {
            int position = entry.Key;
            Button button = entry.Value;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowTileInfo(position));
            button.interactable = true;
            buttonsByPosition[position] = button;
        }

        Debug.Log($"[BoardTileInfoUI] Registered {buttonsByPosition.Count} board tile buttons.");
    }

    private Dictionary<int, Button> TryMapButtonsByMarkers(List<Button> boardButtons)
    {
        Dictionary<int, Button> mappedButtons = new Dictionary<int, Button>();
        HashSet<Button> usedButtons = new HashSet<Button>();
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return mappedButtons;

        for (int position = 0; position < BoardSquareCount; position++)
        {
            Transform marker = FindBoardPointMarker(position);

            if (marker == null)
                return new Dictionary<int, Button>();

            Vector2 markerPoint = WorldPositionToCanvasPoint(marker.position, canvas);
            Button nearestButton = null;
            float nearestDistance = float.MaxValue;

            foreach (Button button in boardButtons)
            {
                if (usedButtons.Contains(button))
                    continue;

                RectTransform buttonRect = button.transform as RectTransform;

                if (buttonRect == null)
                    continue;

                Vector2 buttonPoint = WorldPositionToCanvasPoint(GetRectWorldCenter(buttonRect), canvas);
                float distance = Vector2.SqrMagnitude(buttonPoint - markerPoint);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestButton = button;
                }
            }

            if (nearestButton == null)
                return new Dictionary<int, Button>();

            usedButtons.Add(nearestButton);
            mappedButtons[position] = nearestButton;
        }

        return mappedButtons;
    }

    private Dictionary<int, Button> MapButtonsByClockwiseSort(List<Button> boardButtons)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        List<ButtonPoint> points = new List<ButtonPoint>();

        foreach (Button button in boardButtons)
        {
            RectTransform rect = button.transform as RectTransform;

            if (rect == null)
                continue;

            points.Add(new ButtonPoint(button, WorldPositionToCanvasPoint(GetRectWorldCenter(rect), canvas)));
        }

        SortPathClockwise(points);

        while (points.Count > BoardSquareCount)
            points.RemoveAt(points.Count - 1);

        int startIndex = FindBottomMostPointIndex(points);
        Rotate(points, startIndex);
        ReversePathKeepingStart(points);

        Dictionary<int, Button> mappedButtons = new Dictionary<int, Button>();

        for (int i = 0; i < points.Count && i < BoardSquareCount; i++)
            mappedButtons[i] = points[i].Button;

        return mappedButtons;
    }

    private void ShowTileInfo(int position)
    {
        GameStateData state = GameSession.CurrentState;

        if (state == null || state.Properties == null)
        {
            ShowFallback("Chua co du lieu", "Chua nhan du lieu tran dau tu server.");
            return;
        }

        if (!state.Properties.TryGetValue(position, out GamePropertyStateData property) || property == null)
        {
            ShowFallback($"O {position}", "Khong tim thay thong tin o nay trong GameState.Properties.");
            return;
        }

        titleText.text = $"{property.Name}  |  O {property.PositionIndex}";
        bodyText.text = BuildTileDescription(property, state);
        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);
    }

    private string BuildTileDescription(GamePropertyStateData property, GameStateData state)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Loai: {DescribeType(property.Type)}");

        if (!string.IsNullOrWhiteSpace(property.ColorSet))
            builder.AppendLine($"Nhom mau: {property.ColorSet}");

        if (!string.IsNullOrWhiteSpace(property.LineIndex))
            builder.AppendLine($"Canh ban co: {property.LineIndex}");

        builder.AppendLine();

        if (property.Type == "City" || property.Type == "Resort")
        {
            builder.AppendLine($"Gia mua: {FormatMoney(property.BuyPrice)}");
            builder.AppendLine($"Chu so huu: {GetOwnerName(property.OwnerPlayerIndex, state)}");
            builder.AppendLine($"Cap hien tai: {DescribeUpgradeLevel(property)}");
            builder.AppendLine($"He so tien thue: x{Math.Max(1, property.Multiplier)}");
            builder.AppendLine($"Tien thue hien tai: {FormatMoney(GetCurrentRent(property))}");
            builder.AppendLine();
            builder.AppendLine("Bang tien thue:");
            builder.Append(BuildRentTable(property));
        }
        else
        {
            builder.AppendLine(DescribeSpecialTile(property.Type));
        }

        return builder.ToString();
    }

    private string BuildRentTable(GamePropertyStateData property)
    {
        if (property.RentPrices == null || property.RentPrices.Count == 0)
            return "Khong co bang tien thue.";

        string[] labels = property.Type == "Resort"
            ? new[] { "Mac dinh" }
            : new[] { "Dat trong", "1 nha", "2 nha", "3 nha", "Khach san" };

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < property.RentPrices.Count; i++)
        {
            string label = i < labels.Length ? labels[i] : $"Cap {i}";
            builder.AppendLine($"{label}: {FormatMoney(property.RentPrices[i])}");
        }

        return builder.ToString();
    }

    private long GetCurrentRent(GamePropertyStateData property)
    {
        if (property.RentPrices == null || property.RentPrices.Count == 0)
            return 0;

        int rentIndex = property.HasHotel
            ? property.RentPrices.Count - 1
            : Mathf.Clamp(property.HouseCount, 0, property.RentPrices.Count - 1);

        return property.RentPrices[rentIndex] * Math.Max(1, property.Multiplier);
    }

    private string DescribeUpgradeLevel(GamePropertyStateData property)
    {
        if (property.HasHotel)
            return "Khach san";

        if (property.HouseCount > 0)
            return $"{property.HouseCount} nha";

        return "Dat trong";
    }

    private string GetOwnerName(int ownerPlayerIndex, GameStateData state)
    {
        if (ownerPlayerIndex < 0)
            return "Chua co chu";

        if (state.Players != null)
        {
            foreach (GamePlayerStateData player in state.Players)
            {
                if (player != null && player.PlayerIndex == ownerPlayerIndex)
                    return ShortName(player.Username);
            }
        }

        return $"Player {ownerPlayerIndex + 1}";
    }

    private string DescribeType(string type)
    {
        switch (type)
        {
            case "Start": return "Bat dau";
            case "City": return "Thanh pho";
            case "Resort": return "Khu nghi duong";
            case "Tax": return "Thue";
            case "Chance": return "Co hoi";
            case "LostIsland": return "Dao hoang";
            case "WorldChampionship": return "Giai vo dich";
            case "WorldTour": return "Du lich the gioi";
            default: return string.IsNullOrWhiteSpace(type) ? "Khong xac dinh" : type;
        }
    }

    private string DescribeSpecialTile(string type)
    {
        switch (type)
        {
            case "Start":
                return "Di qua hoac dung tai o Bat Dau se nhan tien thuong theo luat server.";
            case "Tax":
                return "Nguoi choi dung tai o nay se nop thue.";
            case "Chance":
                return "Nguoi choi rut the Co Hoi, co the nhan tien, bi phat, ve Start hoac toi Dao Hoang.";
            case "LostIsland":
                return "Nguoi choi vao Dao Hoang se bi mat luot ke tiep.";
            case "WorldChampionship":
                return "Nguoi choi nhan tien thuong Giai Vo Dich.";
            case "WorldTour":
                return "Nguoi choi nhan tien thuong Du Lich The Gioi.";
            default:
                return "O dac biet, khong the mua.";
        }
    }

    private void ShowFallback(string title, string body)
    {
        titleText.text = title;
        bodyText.text = body;
        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);
    }

    private void HidePopup()
    {
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    private void MakeBoardContainerTransparent(GameObject boardContainer)
    {
        Graphic[] graphics = boardContainer.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic is TextMeshProUGUI)
            {
                graphic.raycastTarget = false;
                Color hiddenTextColor = graphic.color;
                hiddenTextColor.a = 0f;
                graphic.color = hiddenTextColor;
                continue;
            }

            graphic.raycastTarget = graphic.GetComponent<Button>() != null;
            Color color = graphic.color;
            color.a = graphic.GetComponent<Button>() != null ? 0.01f : 0f;
            graphic.color = color;
        }
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
        image.color = new Color(0.85f, 0.22f, 0.18f, 0.96f);

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

    private Transform FindBoardPointMarker(int index)
    {
        string paddedIndex = index.ToString("00");
        GameObject marker = FindSceneObjectByName($"BoardPoint_{paddedIndex}");

        if (marker == null)
            marker = FindSceneObjectByName($"BoardPoint_{index}");

        return marker != null ? marker.transform : null;
    }

    private Vector3 GetRectWorldCenter(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    private Vector2 WorldPositionToCanvasPoint(Vector3 worldPosition, Canvas canvas)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint);
        return localPoint;
    }

    private void SortPathClockwise(List<ButtonPoint> points)
    {
        Vector2 center = Vector2.zero;

        foreach (ButtonPoint point in points)
            center += point.Point;

        center /= points.Count;

        points.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.Point.y - center.y, a.Point.x - center.x);
            float angleB = Mathf.Atan2(b.Point.y - center.y, b.Point.x - center.x);
            return angleA.CompareTo(angleB);
        });
    }

    private int FindBottomMostPointIndex(List<ButtonPoint> points)
    {
        int index = 0;

        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].Point.y < points[index].Point.y)
                index = i;
        }

        return index;
    }

    private void Rotate(List<ButtonPoint> points, int startIndex)
    {
        if (startIndex <= 0)
            return;

        List<ButtonPoint> rotated = new List<ButtonPoint>(points.Count);

        for (int i = 0; i < points.Count; i++)
            rotated.Add(points[(startIndex + i) % points.Count]);

        points.Clear();
        points.AddRange(rotated);
    }

    private void ReversePathKeepingStart(List<ButtonPoint> points)
    {
        if (points.Count <= 2)
            return;

        ButtonPoint start = points[0];
        List<ButtonPoint> tail = points.GetRange(1, points.Count - 1);
        tail.Reverse();

        points.Clear();
        points.Add(start);
        points.AddRange(tail);
    }

    private string FormatMoney(long amount)
    {
        return amount > 0 ? $"${amount:N0}" : "N/A";
    }

    private string ShortName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Player";

        int atIndex = username.IndexOf("@", StringComparison.Ordinal);

        if (atIndex > 0)
            username = username.Substring(0, atIndex);

        return username.Length > 14 ? username.Substring(0, 14) : username;
    }

    private sealed class ButtonPoint
    {
        public readonly Button Button;
        public readonly Vector2 Point;

        public ButtonPoint(Button button, Vector2 point)
        {
            Button = button;
            Point = point;
        }
    }
}
