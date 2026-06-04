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
    private const string TileInfoCardSpritePath = "UI/TileInfoCard";
    private const string TileImageResourcePath = "TileImages";

    private readonly Dictionary<int, Button> buttonsByPosition = new Dictionary<int, Button>();
    private RectTransform popupRoot;
    private RectTransform popupCardRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Image deedHeaderImage;
    private Image deedPreviewFrameImage;
    private Image deedPreviewImage;
    private TextMeshProUGUI deedLabelText;
    private TextMeshProUGUI deedPropertyNameText;
    private TextMeshProUGUI deedMetaText;
    private readonly List<TextMeshProUGUI> deedRentLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> deedRentValues = new List<TextMeshProUGUI>();
    private Image deedDividerImage;
    private TextMeshProUGUI deedHouseCostLabel;
    private TextMeshProUGUI deedHouseCostValue;
    private TextMeshProUGUI deedHotelCostLabel;
    private TextMeshProUGUI deedHotelCostValue;
    private TextMeshProUGUI actionHintText;
    private Button closeButton;
    private Button buildButton;
    private RectTransform markerClickLayer;
    private int currentPopupPosition = -1;
    private float nextPopupRefreshTime;
    private bool isSelectingCardTarget;
    private string selectingCardEffectCode = "";
    private readonly HashSet<int> selectableCardTargets = new HashSet<int>();

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

        GameObject rootObject = new GameObject("Panel_BoardTileInfoPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        popupRoot = rootObject.GetComponent<RectTransform>();
        popupRoot.SetParent(canvas.transform, false);
        popupRoot.anchorMin = Vector2.zero;
        popupRoot.anchorMax = Vector2.one;
        popupRoot.pivot = new Vector2(0.5f, 0.5f);
        popupRoot.anchoredPosition = Vector2.zero;
        popupRoot.sizeDelta = Vector2.zero;
        popupRoot.offsetMin = Vector2.zero;
        popupRoot.offsetMax = Vector2.zero;

        Image overlayImage = rootObject.GetComponent<Image>();
        overlayImage.sprite = null;
        overlayImage.color = new Color(0f, 0f, 0f, 0.48f);
        overlayImage.raycastTarget = true;

        GameObject cardObject = new GameObject("Panel_TileInfoModalCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        popupCardRoot = cardObject.GetComponent<RectTransform>();
        popupCardRoot.SetParent(popupRoot, false);
        popupCardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        popupCardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        popupCardRoot.pivot = new Vector2(0.5f, 0.5f);
        popupCardRoot.anchoredPosition = Vector2.zero;
        popupCardRoot.sizeDelta = new Vector2(980f, 620f);

        Image cardImage = cardObject.GetComponent<Image>();
        ApplyCardBackground(cardImage);
        cardImage.raycastTarget = true;

        BuildTitleDeedUi();

        titleText = CreateText("Txt_TileTitle", popupCardRoot, "", 34f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 22f;
        titleText.fontSizeMax = 36f;
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-20f, -35f), new Vector2(-190f, 70f));

        bodyText = CreateText("Txt_TileBody", popupCardRoot, "", 24f, FontStyles.Normal);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.lineSpacing = 8f;
        bodyText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        SetOffsets(bodyText.rectTransform, 72f, 142f, 72f, 122f);

        closeButton = CreateButton("Btn_CloseTilePopup", popupCardRoot, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -22f), new Vector2(64f, 54f));
        closeButton.onClick.AddListener(HidePopup);
        TextMeshProUGUI closeText = closeButton.GetComponentInChildren<TextMeshProUGUI>();

        if (closeText != null)
            closeText.fontSize = 34f;

        actionHintText = CreateText("Txt_TileActionHint", popupCardRoot, "", 20f, FontStyles.Normal);
        actionHintText.alignment = TextAlignmentOptions.MidlineLeft;
        actionHintText.color = new Color(0.26f, 0.26f, 0.26f, 1f);
        actionHintText.enableWordWrapping = true;
        SetRect(actionHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 28f), new Vector2(-430f, 58f));

        buildButton = CreateButton("Btn_BuildProperty", popupCardRoot, "Nâng cấp", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-52f, 26f), new Vector2(250f, 54f));
        buildButton.onClick.AddListener(SendBuildRequest);
        SetButtonColor(buildButton, new Color(0.1f, 0.56f, 0.86f, 0.98f));
        TextMeshProUGUI buildText = buildButton.GetComponentInChildren<TextMeshProUGUI>();

        if (buildText != null)
            buildText.fontSize = 22f;

        popupRoot.gameObject.SetActive(false);
    }

    private void ApplyCardBackground(Image rootImage)
    {
        if (rootImage == null)
            return;

        Sprite cardSprite = Resources.Load<Sprite>(TileInfoCardSpritePath);

        if (cardSprite == null || cardSprite.border.sqrMagnitude <= 0f)
        {
            rootImage.sprite = null;
            rootImage.color = new Color(0.96f, 0.92f, 0.84f, 0.99f);
            rootImage.type = Image.Type.Simple;
            return;
        }

        rootImage.sprite = cardSprite;
        rootImage.color = Color.white;
        rootImage.preserveAspect = false;
        rootImage.type = cardSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
    }

    private void BuildTitleDeedUi()
    {
        Transform parent = popupCardRoot != null ? popupCardRoot : popupRoot;

        deedHeaderImage = CreatePanelImage("Img_DeedHeader", parent, new Color(0.86f, 0.02f, 0.32f, 1f));
        SetRect(deedHeaderImage.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(-86f, 88f));

        deedLabelText = CreateText("Txt_DeedLabel", parent, "THẺ SỞ HỮU", 17f, FontStyles.Bold);
        deedLabelText.alignment = TextAlignmentOptions.Center;
        deedLabelText.color = Color.white;
        SetRect(deedLabelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-16f, -20f), new Vector2(-210f, 24f));

        deedPropertyNameText = CreateText("Txt_DeedPropertyName", parent, "", 36f, FontStyles.Bold);
        deedPropertyNameText.alignment = TextAlignmentOptions.Center;
        deedPropertyNameText.color = Color.white;
        deedPropertyNameText.enableAutoSizing = true;
        deedPropertyNameText.fontSizeMin = 24f;
        deedPropertyNameText.fontSizeMax = 36f;
        SetRect(deedPropertyNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-16f, -48f), new Vector2(-210f, 46f));

        deedMetaText = CreateText("Txt_DeedMeta", parent, "", 22f, FontStyles.Normal);
        deedMetaText.alignment = TextAlignmentOptions.Center;
        deedMetaText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        deedMetaText.enableWordWrapping = false;
        deedMetaText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(deedMetaText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(68f, -116f), new Vector2(-136f, 34f));

        deedPreviewFrameImage = CreatePanelImage("Img_DeedPreviewFrame", parent, new Color(1f, 1f, 1f, 0.96f));
        deedPreviewFrameImage.raycastTarget = false;
        SetRect(deedPreviewFrameImage.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(78f, -174f), new Vector2(270f, 270f));

        deedPreviewImage = CreatePanelImage("Img_DeedPreview", deedPreviewFrameImage.transform, new Color(0.88f, 0.93f, 0.94f, 1f));
        deedPreviewImage.preserveAspect = true;
        SetOffsets(deedPreviewImage.rectTransform, 12f, 12f, 12f, 12f);

        string[] labels =
        {
            "Tiền thuê đất trống",
            "Tiền thuê với 1 nhà",
            "Tiền thuê với 2 nhà",
            "Tiền thuê với 3 nhà",
            "Tiền thuê với khách sạn"
        };

        for (int i = 0; i < labels.Length; i++)
        {
            float y = -182f - i * 48f;
            TextMeshProUGUI label = CreateText($"Txt_DeedRentLabel_{i}", parent, labels[i], 24f, FontStyles.Normal);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, y), new Vector2(330f, 36f));
            deedRentLabels.Add(label);

            TextMeshProUGUI value = CreateText($"Txt_DeedRentValue_{i}", parent, "", 24f, FontStyles.Bold);
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            SetRect(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-86f, y), new Vector2(250f, 36f));
            deedRentValues.Add(value);
        }

        deedDividerImage = CreatePanelImage("Img_DeedDivider", parent, new Color(0.18f, 0.18f, 0.18f, 0.55f));
        SetRect(deedDividerImage.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -438f), new Vector2(500f, 2f));

        deedHouseCostLabel = CreateText("Txt_DeedHouseCostLabel", parent, "Chi phí nhà", 22f, FontStyles.Bold);
        deedHouseCostLabel.alignment = TextAlignmentOptions.MidlineLeft;
        deedHouseCostLabel.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        SetRect(deedHouseCostLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -462f), new Vector2(330f, 34f));

        deedHouseCostValue = CreateText("Txt_DeedHouseCostValue", parent, "", 22f, FontStyles.Bold);
        deedHouseCostValue.alignment = TextAlignmentOptions.MidlineRight;
        deedHouseCostValue.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        SetRect(deedHouseCostValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-86f, -462f), new Vector2(250f, 34f));

        deedHotelCostLabel = CreateText("Txt_DeedHotelCostLabel", parent, "Chi phí khách sạn", 22f, FontStyles.Bold);
        deedHotelCostLabel.alignment = TextAlignmentOptions.MidlineLeft;
        deedHotelCostLabel.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        SetRect(deedHotelCostLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -502f), new Vector2(330f, 34f));

        deedHotelCostValue = CreateText("Txt_DeedHotelCostValue", parent, "", 22f, FontStyles.Bold);
        deedHotelCostValue.alignment = TextAlignmentOptions.MidlineRight;
        deedHotelCostValue.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        SetRect(deedHotelCostValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-86f, -502f), new Vector2(250f, 34f));
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
        if (isSelectingCardTarget)
        {
            if (selectableCardTargets.Contains(position))
            {
                if (NetworkManager.Instance != null)
                    NetworkManager.Instance.SendCardChoiceMade(selectingCardEffectCode, position);

                ClearCardTargetSelection();
                return;
            }

            currentPopupPosition = position;
            RefreshCurrentPopup();

            if (actionHintText != null)
                actionHintText.text = $"O {position} khong hop le cho the {selectingCardEffectCode}.";

            popupRoot.SetAsLastSibling();
            popupRoot.gameObject.SetActive(true);
            return;
        }

        currentPopupPosition = position;
        RefreshCurrentPopup();
        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);
    }

    public void BeginCardTargetSelection(string effectCode, List<int> validPositions)
    {
        selectingCardEffectCode = effectCode ?? "";
        isSelectingCardTarget = !string.IsNullOrWhiteSpace(selectingCardEffectCode);
        selectableCardTargets.Clear();

        if (validPositions != null)
        {
            foreach (int position in validPositions)
                selectableCardTargets.Add(position);
        }

        ApplyTargetHighlights();
        HidePopup();
    }

    public void SyncCardChoiceState(GameStateData state)
    {
        string username = PlayerSession.Instance?.Username ?? "";

        if (state != null &&
            state.IsWaitingForCardChoice &&
            string.Equals(state.PendingCardPlayerUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            BeginCardTargetSelection(state.PendingCardEffectCode, state.PendingCardTargetPositions);
            return;
        }

        if (isSelectingCardTarget)
            ClearCardTargetSelection();
    }

    private void ClearCardTargetSelection()
    {
        isSelectingCardTarget = false;
        selectingCardEffectCode = "";
        selectableCardTargets.Clear();
        ApplyTargetHighlights();
    }

    private void ApplyTargetHighlights()
    {
        foreach (KeyValuePair<int, Button> entry in buttonsByPosition)
        {
            Image image = entry.Value != null ? entry.Value.GetComponent<Image>() : null;

            if (image == null)
                continue;

            bool isValidTarget = isSelectingCardTarget && selectableCardTargets.Contains(entry.Key);
            image.color = isValidTarget
                ? new Color(0.1f, 0.85f, 0.35f, 0.38f)
                : new Color(1f, 1f, 1f, 0.01f);
        }
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

        titleText.text = property.Name;
        if (property.Type == "City" || property.Type == "Resort")
        {
            ShowTitleDeed(property, state);
            RefreshBuildButton(property, state);
            return;
        }

        SetTitleDeedVisible(false);
        titleText.richText = false;
        bodyText.richText = false;
        Color headerColor = GetPopupHeaderColor(property);

        if (deedHeaderImage != null)
        {
            deedHeaderImage.gameObject.SetActive(true);
            deedHeaderImage.color = headerColor;
        }

        titleText.text = property.Name.ToUpperInvariant();
        titleText.color = GetReadableHeaderTextColor(headerColor);
        bodyText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        actionHintText.color = new Color(0.26f, 0.26f, 0.26f, 1f);
        bodyText.text = BuildTileDescription(property, state);
        RefreshBuildButton(property, state);
    }

    private void ShowTitleDeed(GamePropertyStateData property, GameStateData state)
    {
        SetTitleDeedVisible(true);

        if (titleText != null)
            titleText.gameObject.SetActive(false);

        if (bodyText != null)
            bodyText.gameObject.SetActive(false);

        Color propertyColor = GetPopupHeaderColor(property);
        Color headerTextColor = GetReadableHeaderTextColor(propertyColor);

        if (deedHeaderImage != null)
            deedHeaderImage.color = propertyColor;

        UpdateTilePreview(property);

        if (deedLabelText != null)
            deedLabelText.color = headerTextColor;

        if (deedPropertyNameText != null)
        {
            deedPropertyNameText.text = property.Name.ToUpperInvariant();
            deedPropertyNameText.color = headerTextColor;
        }

        if (deedMetaText != null)
        {
            string owner = GetOwnerName(property.OwnerPlayerIndex, state);
            deedMetaText.text = $"Vị trí: Ô {property.PositionIndex}  |  Giá mua: {FormatMoney(property.BuyPrice)}  |  Chủ: {owner}  |  Cấp: {DescribeUpgradeLevel(property)}";
        }

        if (actionHintText != null)
            actionHintText.color = new Color(0.08f, 0.08f, 0.08f, 1f);

        string[] labels = BuildDeedRentLabels(property);
        string[] values = BuildDeedRentValues(property);

        for (int i = 0; i < deedRentLabels.Count; i++)
        {
            bool active = i < labels.Length && i < values.Length;
            deedRentLabels[i].gameObject.SetActive(active);
            deedRentValues[i].gameObject.SetActive(active);

            if (!active)
                continue;

            deedRentLabels[i].text = labels[i];
            deedRentValues[i].text = values[i];
        }

        long buildCost = GetBuildCost(property);
        string costText = buildCost > 0 ? $"{FormatMoney(buildCost)} mỗi lần" : "Tối đa";
        bool showBuildCosts = property.Type == "City";

        if (deedDividerImage != null)
            deedDividerImage.gameObject.SetActive(showBuildCosts);

        if (deedHouseCostLabel != null)
            deedHouseCostLabel.gameObject.SetActive(showBuildCosts);

        if (deedHouseCostValue != null)
        {
            deedHouseCostValue.gameObject.SetActive(showBuildCosts);
            deedHouseCostValue.text = costText;
        }

        if (deedHotelCostLabel != null)
            deedHotelCostLabel.gameObject.SetActive(showBuildCosts);

        if (deedHotelCostValue != null)
        {
            deedHotelCostValue.gameObject.SetActive(showBuildCosts);
            deedHotelCostValue.text = costText;
        }
    }

    private void SetTitleDeedVisible(bool visible)
    {
        if (titleText != null)
            titleText.gameObject.SetActive(!visible);

        if (bodyText != null)
            bodyText.gameObject.SetActive(!visible);

        if (deedHeaderImage != null)
            deedHeaderImage.gameObject.SetActive(visible);

        if (deedLabelText != null)
            deedLabelText.gameObject.SetActive(visible);

        if (deedPropertyNameText != null)
            deedPropertyNameText.gameObject.SetActive(visible);

        if (deedPreviewFrameImage != null)
            deedPreviewFrameImage.gameObject.SetActive(visible);

        if (deedPreviewImage != null)
            deedPreviewImage.gameObject.SetActive(visible);

        if (deedMetaText != null)
            deedMetaText.gameObject.SetActive(visible);

        if (deedDividerImage != null)
            deedDividerImage.gameObject.SetActive(visible);

        foreach (TextMeshProUGUI label in deedRentLabels)
            label.gameObject.SetActive(visible);

        foreach (TextMeshProUGUI value in deedRentValues)
            value.gameObject.SetActive(visible);

        if (deedHouseCostLabel != null)
            deedHouseCostLabel.gameObject.SetActive(visible);

        if (deedHouseCostValue != null)
            deedHouseCostValue.gameObject.SetActive(visible);

        if (deedHotelCostLabel != null)
            deedHotelCostLabel.gameObject.SetActive(visible);

        if (deedHotelCostValue != null)
            deedHotelCostValue.gameObject.SetActive(visible);
    }

    private string[] BuildDeedRentLabels(GamePropertyStateData property)
    {
        if (property.Type == "Resort")
        {
            return new[]
            {
                "Tiền thuê"
            };
        }

        return new[]
        {
            "Tiền thuê đất trống",
            "Tiền thuê với 1 nhà",
            "Tiền thuê với 2 nhà",
            "Tiền thuê với 3 nhà",
            "Tiền thuê với khách sạn"
        };
    }

    private void UpdateTilePreview(GamePropertyStateData property)
    {
        if (deedPreviewImage == null)
            return;

        Sprite sprite = LoadTilePreviewSprite(property);
        deedPreviewImage.sprite = sprite;
        deedPreviewImage.color = sprite != null
            ? Color.white
            : new Color(0.86f, 0.92f, 0.94f, 1f);
    }

    private Sprite LoadTilePreviewSprite(GamePropertyStateData property)
    {
        if (property == null)
            return null;

        string[] resourceNames =
        {
            $"tile-{property.PositionIndex:00}",
            property.Name,
            (property.Name ?? "").Replace(" ", "_")
        };

        foreach (string resourceName in resourceNames)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                continue;

            Sprite sprite = Resources.Load<Sprite>($"{TileImageResourcePath}/{resourceName}");

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private string[] BuildDeedRentValues(GamePropertyStateData property)
    {
        List<string> values = new List<string>();

        if (property.RentPrices == null || property.RentPrices.Count == 0)
        {
            values.Add(FormatMoney(0));
            return values.ToArray();
        }

        if (property.Type == "Resort")
        {
            values.Add(FormatMoney(property.RentPrices[0]));
            return values.ToArray();
        }

        for (int i = 0; i < property.RentPrices.Count; i++)
            values.Add(FormatMoney(property.RentPrices[i]));

        while (values.Count < 5)
            values.Add("-");

        return values.ToArray();
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

    private Color GetPopupHeaderColor(GamePropertyStateData property)
    {
        if (property != null &&
            (property.Type == "City" || property.Type == "Resort") &&
            TryGetMonopolyColor(property.ColorSet, out Color monopolyColor))
        {
            return monopolyColor;
        }

        switch (property?.Type)
        {
            case "Start":
                return new Color(0.18f, 0.66f, 0.34f, 1f);
            case "Tax":
                return new Color(0.58f, 0.25f, 0.78f, 1f);
            case "Chance":
                return new Color(0.04f, 0.54f, 0.78f, 1f);
            case "LostIsland":
                return new Color(0.22f, 0.34f, 0.44f, 1f);
            case "WorldChampionship":
                return new Color(0.84f, 0.1f, 0.2f, 1f);
            case "WorldTour":
                return new Color(0.96f, 0.5f, 0.12f, 1f);
            default:
                return new Color(0.86f, 0.02f, 0.32f, 1f);
        }
    }

    private Color GetReadableHeaderTextColor(Color background)
    {
        float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
        return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.06f, 1f) : Color.white;
    }

    private string BuildTileDescription(GamePropertyStateData property, GameStateData state)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Ô: {property.PositionIndex}");
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

        bool showBuildButton = property != null && property.Type == "City";
        buildButton.gameObject.SetActive(showBuildButton);

        if (!showBuildButton)
        {
            actionHintText.text = "";
            return;
        }

        bool canBuild = CanBuildProperty(property, state, out string reason, out long buildCost);
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
        SetTitleDeedVisible(false);
        Color fallbackHeaderColor = new Color(0.22f, 0.34f, 0.44f, 1f);

        if (deedHeaderImage != null)
        {
            deedHeaderImage.gameObject.SetActive(true);
            deedHeaderImage.color = fallbackHeaderColor;
        }

        titleText.text = title;
        titleText.color = GetReadableHeaderTextColor(fallbackHeaderColor);
        bodyText.text = body;
        bodyText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
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

    private Image CreatePanelImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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

    private void SetOffsets(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
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
