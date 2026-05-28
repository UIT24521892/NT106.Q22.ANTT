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
    private TextMeshProUGUI actionHintText;
    private Button closeButton;
    private Button buildButton;
    private RectTransform markerClickLayer;
    private int currentPopupPosition = -1;
    private float nextPopupRefreshTime;

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

    private void Update()
    {
        if (popupRoot == null || !popupRoot.gameObject.activeSelf || currentPopupPosition < 0)
            return;

        if (Time.unscaledTime < nextPopupRefreshTime)
            return;

        nextPopupRefreshTime = Time.unscaledTime + 0.35f;
        RefreshCurrentPopup();
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
        popupRoot.sizeDelta = new Vector2(500f, 470f);

        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = new Color(0.07f, 0.08f, 0.09f, 0.94f);
        rootImage.raycastTarget = true;

        titleText = CreateText("Txt_TileTitle", popupRoot, "", 24f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.color = new Color(1f, 0.86f, 0.42f, 1f);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(20f, -18f), new Vector2(-74f, 54f));

        bodyText = CreateText("Txt_TileBody", popupRoot, "", 15f, FontStyles.Normal);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Ellipsis;
        bodyText.lineSpacing = 2f;
        SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(-40f, -118f));

        closeButton = CreateButton("Btn_CloseTilePopup", popupRoot, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 38f));
        closeButton.onClick.AddListener(HidePopup);

        actionHintText = CreateText("Txt_TileActionHint", popupRoot, "", 14f, FontStyles.Normal);
        actionHintText.alignment = TextAlignmentOptions.MidlineLeft;
        actionHintText.color = new Color(0.82f, 0.9f, 1f, 1f);
        SetRect(actionHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-80f, 62f), new Vector2(-204f, 34f));

        buildButton = CreateButton("Btn_BuildProperty", popupRoot, "Nâng cấp", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(150f, 44f));
        buildButton.onClick.AddListener(SendBuildRequest);
        SetButtonColor(buildButton, new Color(0.18f, 0.62f, 0.25f, 0.98f));

        popupRoot.gameObject.SetActive(false);
    }

    private void RegisterBoardButtons()
    {
        if (TryRegisterMarkerClickZones())
        {
            Debug.Log($"[BoardTileInfoUI] Registered {buttonsByPosition.Count} marker tile click zones.");
            return;
        }

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

    private bool TryRegisterMarkerClickZones()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return false;

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null)
            return false;

        buttonsByPosition.Clear();

        GameObject layerObject = new GameObject("Runtime_BoardTileClickZones", typeof(RectTransform), typeof(CanvasRenderer));
        markerClickLayer = layerObject.GetComponent<RectTransform>();
        markerClickLayer.SetParent(canvasRect, false);
        markerClickLayer.anchorMin = Vector2.zero;
        markerClickLayer.anchorMax = Vector2.one;
        markerClickLayer.offsetMin = Vector2.zero;
        markerClickLayer.offsetMax = Vector2.zero;
        markerClickLayer.pivot = new Vector2(0.5f, 0.5f);
        markerClickLayer.SetAsLastSibling();

        for (int position = 0; position < BoardSquareCount; position++)
        {
            Transform marker = FindBoardPointMarker(position);

            if (marker == null)
            {
                Destroy(markerClickLayer.gameObject);
                markerClickLayer = null;
                buttonsByPosition.Clear();
                return false;
            }

            RectTransform markerRect = marker as RectTransform;
            Vector3 worldCenter = markerRect != null ? GetRectWorldCenter(markerRect) : marker.position;
            Vector2 anchoredPosition = WorldPositionToCanvasPoint(worldCenter, canvas);
            Vector2 size = markerRect != null ? markerRect.rect.size : new Vector2(88f, 88f);
            size = new Vector2(Mathf.Max(48f, size.x), Mathf.Max(48f, size.y));

            int capturedPosition = position;
            GameObject zoneObject = new GameObject($"Btn_TileZone_{position:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform zoneRect = zoneObject.GetComponent<RectTransform>();
            zoneRect.SetParent(markerClickLayer, false);
            zoneRect.anchorMin = new Vector2(0.5f, 0.5f);
            zoneRect.anchorMax = new Vector2(0.5f, 0.5f);
            zoneRect.pivot = new Vector2(0.5f, 0.5f);
            zoneRect.anchoredPosition = anchoredPosition;
            zoneRect.sizeDelta = size;

            Image image = zoneObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;

            Button button = zoneObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ShowTileInfo(capturedPosition));

            buttonsByPosition[capturedPosition] = button;
        }

        return buttonsByPosition.Count == BoardSquareCount;
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

    public void ShowTileInfo(int position)
    {
        currentPopupPosition = position;
        RefreshCurrentPopup();
        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);
    }

    private void RefreshCurrentPopup()
    {
        GameStateData state = GameSession.CurrentState;

        if (state == null || state.Properties == null)
        {
            ShowFallback("Chưa có dữ liệu", "Chưa nhận dữ liệu trận đấu từ server.");
            return;
        }

        if (!state.Properties.TryGetValue(currentPopupPosition, out GamePropertyStateData property) || property == null)
        {
            ShowFallback($"Ô {currentPopupPosition}", "Không tìm thấy thông tin ô này trong GameState.Properties.");
            return;
        }

        titleText.text = $"{property.Name}  |  Ô {property.PositionIndex}";
        Color popupTextColor = GetPopupTextColor(property);
        string popupTextColorHex = ColorUtility.ToHtmlStringRGB(popupTextColor);

        titleText.richText = true;
        bodyText.richText = true;
        titleText.text = $"<color=#{popupTextColorHex}>{titleText.text}</color>";
        bodyText.text = $"<color=#{popupTextColorHex}>{BuildTileDescription(property, state)}</color>";
        ApplyPopupTextColor(property);
        RefreshBuildButton(property, state);
        ApplyPopupTextColor(property);
    }

    private void ApplyPopupTextColor(GamePropertyStateData property)
    {
        Color textColor = GetPopupTextColor(property);

        if (titleText != null)
            titleText.color = textColor;

        if (bodyText != null)
            bodyText.color = textColor;

        if (actionHintText != null)
            actionHintText.color = textColor;
    }

    private Color GetPopupTextColor(GamePropertyStateData property)
    {
        if (property != null &&
            (property.Type == "City" || property.Type == "Resort") &&
            TryGetMonopolyColor(property.ColorSet, out Color monopolyColor))
        {
            return monopolyColor;
        }

        return new Color(1f, 0.86f, 0.42f, 1f);
    }

    private string BuildTileDescription(GamePropertyStateData property, GameStateData state)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Loại: {DescribeType(property.Type)}");

        if (!string.IsNullOrWhiteSpace(property.ColorSet))
            builder.AppendLine($"Nhóm màu: {property.ColorSet}");

        if (!string.IsNullOrWhiteSpace(property.LineIndex))
            builder.AppendLine($"Cạnh bàn cờ: {property.LineIndex}");

        if (property.Type == "City" || property.Type == "Resort")
        {
            builder.AppendLine($"Giá mua: {FormatMoney(property.BuyPrice)}");
            builder.AppendLine($"Chủ sở hữu: {GetOwnerName(property.OwnerPlayerIndex, state)}");
            builder.AppendLine($"Cấp hiện tại: {DescribeUpgradeLevel(property)}");
            builder.AppendLine($"Hệ số tiền thuê: x{Math.Max(1, property.Multiplier)}");
            builder.AppendLine($"Tiền thuê hiện tại: {FormatMoney(GetCurrentRent(property))}");

            if (property.Type == "City" && !property.HasHotel)
                builder.AppendLine($"Chi phí nâng cấp tiếp theo: {FormatMoney(GetBuildCost(property))}");

            builder.AppendLine("Bảng tiền thuê:");
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
            return "Không có bảng tiền thuê.";

        string[] labels = property.Type == "Resort"
            ? new[] { "Mặc định" }
            : new[] { "Đất trống", "1 nhà", "2 nhà", "3 nhà", "Khách sạn" };

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < property.RentPrices.Count; i++)
        {
            string label = i < labels.Length ? labels[i] : $"Cấp {i}";
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
            return "Khách sạn";

        if (property.HouseCount > 0)
            return $"{property.HouseCount} nhà";

        return "Đất trống";
    }

    private long GetBuildCost(GamePropertyStateData property)
    {
        if (property == null || property.Type != "City" || property.BuyPrice <= 0 || property.HasHotel)
            return 0;

        return property.HouseCount >= 3 ? property.BuyPrice : Math.Max(1, property.BuyPrice / 2);
    }

    private void RefreshBuildButton(GamePropertyStateData property, GameStateData state)
    {
        if (buildButton == null || actionHintText == null)
            return;

        bool canBuild = CanBuildProperty(property, state, out string reason, out long buildCost);
        buildButton.gameObject.SetActive(property != null && property.Type == "City");
        buildButton.interactable = canBuild;

        TextMeshProUGUI buttonText = buildButton.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null)
            buttonText.text = "Nâng cấp";

        actionHintText.text = reason;
    }

    private bool CanBuildProperty(GamePropertyStateData property, GameStateData state, out string reason, out long buildCost)
    {
        buildCost = GetBuildCost(property);

        if (property == null)
        {
            reason = "";
            return false;
        }

        if (property.Type != "City")
        {
            reason = "Chỉ thành phố mới có thể nâng cấp.";
            return false;
        }

        if (state == null || state.IsFinished)
        {
            reason = "Trận đấu không còn đang diễn ra.";
            return false;
        }

        GamePlayerStateData localPlayer = GetLocalPlayer(state);

        if (localPlayer == null)
        {
            reason = "Không tìm thấy người chơi hiện tại.";
            return false;
        }

        if (!localPlayer.IsConnected || localPlayer.IsBankrupt)
        {
            reason = "Người chơi hiện tại không thể thao tác.";
            return false;
        }

        if (localPlayer.PlayerIndex != state.CurrentTurnPlayerIndex)
        {
            reason = "Chỉ nâng cấp trong lượt của bạn.";
            return false;
        }

        if (property.OwnerPlayerIndex != localPlayer.PlayerIndex)
        {
            reason = property.OwnerPlayerIndex < 0 ? "Đất này chưa có chủ." : "Bạn không sở hữu đất này.";
            return false;
        }

        if (property.HasHotel)
        {
            reason = "Đất này đã đạt cấp khách sạn.";
            return false;
        }

        if (buildCost <= 0)
        {
            reason = "Không có chi phí nâng cấp hợp lệ.";
            return false;
        }

        if (localPlayer.Money < buildCost)
        {
            reason = $"Cần {FormatMoney(buildCost)}, bạn chỉ có {FormatMoney(localPlayer.Money)}.";
            return false;
        }

        reason = $"Có thể nâng cấp với giá {FormatMoney(buildCost)}.";
        return true;
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

    private void SendBuildRequest()
    {
        if (currentPopupPosition < 0 || NetworkManager.Instance == null)
            return;

        if (buildButton != null)
            buildButton.interactable = false;

        NetworkManager.Instance.SendBuildPropertyRequest(currentPopupPosition);
        actionHintText.text = "Đã gửi yêu cầu nâng cấp lên server...";
    }

    private string GetOwnerName(int ownerPlayerIndex, GameStateData state)
    {
        if (ownerPlayerIndex < 0)
            return "Chưa có chủ";

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
            case "Start": return "Bắt đầu";
            case "City": return "Thành phố";
            case "Resort": return "Khu nghỉ dưỡng";
            case "Tax": return "Thuế";
            case "Chance": return "Cơ hội";
            case "LostIsland": return "Đảo hoang";
            case "WorldChampionship": return "Giải vô địch";
            case "WorldTour": return "Du lịch thế giới";
            default: return string.IsNullOrWhiteSpace(type) ? "Không xác định" : type;
        }
    }

    private string DescribeSpecialTile(string type)
    {
        switch (type)
        {
            case "Start":
                return "Đi qua hoặc dừng tại ô Bắt Đầu sẽ nhận tiền thưởng theo luật server.";
            case "Tax":
                return "Người chơi dừng tại ô này sẽ nộp thuế.";
            case "Chance":
                return "Người chơi rút thẻ Cơ Hội, có thể nhận tiền, bị phạt, về Start hoặc tới Đảo Hoang.";
            case "LostIsland":
                return "Người chơi vào Đảo Hoang sẽ bị mất lượt kế tiếp.";
            case "WorldChampionship":
                return "Người chơi nhận tiền thưởng Giải Vô Địch.";
            case "WorldTour":
                return "Người chơi nhận tiền thưởng Du Lịch Thế Giới.";
            default:
                return "Ô đặc biệt, không thể mua.";
        }
    }

    private void ShowFallback(string title, string body)
    {
        titleText.text = title;
        bodyText.text = body;
        currentPopupPosition = -1;

        if (buildButton != null)
            buildButton.gameObject.SetActive(false);

        if (actionHintText != null)
            actionHintText.text = "";

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

    private void SetButtonColor(Button button, Color color)
    {
        if (button == null || button.targetGraphic == null)
            return;

        button.targetGraphic.color = color;
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

    private bool TryGetMonopolyColor(string colorSet, out Color color)
    {
        switch ((colorSet ?? "").Trim().ToLowerInvariant())
        {
            case "pink":
                color = new Color(1f, 0.38f, 0.72f, 1f);
                return true;
            case "yellow":
                color = new Color(1f, 0.84f, 0.16f, 1f);
                return true;
            case "blue":
                color = new Color(0.26f, 0.58f, 1f, 1f);
                return true;
            case "green":
                color = new Color(0.26f, 0.88f, 0.45f, 1f);
                return true;
            case "brown":
                color = new Color(0.72f, 0.48f, 0.28f, 1f);
                return true;
            case "purple":
                color = new Color(0.76f, 0.45f, 1f, 1f);
                return true;
            case "orange":
                color = new Color(1f, 0.53f, 0.18f, 1f);
                return true;
            case "cyan":
                color = new Color(0.16f, 0.86f, 0.95f, 1f);
                return true;
            default:
                color = Color.white;
                return false;
        }
    }

    private string FormatMoney(long amount)
    {
        return amount >= 0 ? $"${amount:N0}" : "Không có";
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
