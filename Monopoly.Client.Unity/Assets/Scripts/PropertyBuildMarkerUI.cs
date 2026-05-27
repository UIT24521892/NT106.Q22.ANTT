using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PropertyBuildMarkerUI : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;
    private const int BoardSquareCount = 32;
    private const float MarkerDistanceFromTile = 46f;
    private const float HouseBarWidth = 12f;
    private const float HouseBarHeight = 46f;
    private const float HouseBarSpacing = 17f;
    private const float HotelBarWidth = 52f;
    private const float HotelBarHeight = 46f;

    private readonly Dictionary<int, MarkerGroup> markersByPosition = new Dictionary<int, MarkerGroup>();
    private RectTransform layerRoot;
    private RectTransform canvasRect;
    private float nextRefreshTime;

    public static PropertyBuildMarkerUI EnsureExists()
    {
        PropertyBuildMarkerUI existing = FindObjectOfType<PropertyBuildMarkerUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("PropertyBuildMarkerUI");
        return host.AddComponent<PropertyBuildMarkerUI>();
    }

    private void Start()
    {
        BuildLayer();
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    private void BuildLayer()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[PropertyBuildMarkerUI] Canvas not found.");
            return;
        }

        canvasRect = canvas.transform as RectTransform;

        GameObject layerObject = new GameObject("Runtime_PropertyBuildMarkers", typeof(RectTransform), typeof(CanvasRenderer));
        layerRoot = layerObject.GetComponent<RectTransform>();
        layerRoot.SetParent(canvasRect, false);
        layerRoot.anchorMin = Vector2.zero;
        layerRoot.anchorMax = Vector2.one;
        layerRoot.offsetMin = Vector2.zero;
        layerRoot.offsetMax = Vector2.zero;
        layerRoot.SetAsLastSibling();
    }

    private void Refresh()
    {
        if (layerRoot == null || canvasRect == null)
            return;

        GameStateData state = GameSession.CurrentState;

        if (state == null || state.Properties == null)
        {
            HideAllMarkers();
            return;
        }

        HashSet<int> activePositions = new HashSet<int>();

        foreach (KeyValuePair<int, GamePropertyStateData> pair in state.Properties)
        {
            GamePropertyStateData property = pair.Value;

            if (!ShouldShowMarker(property))
                continue;

            if (!TryGetBoardPointLocalPosition(property.PositionIndex, out Vector2 tilePosition))
                continue;

            activePositions.Add(property.PositionIndex);
            MarkerGroup marker = GetOrCreateMarker(property.PositionIndex);
            marker.SetState(property, GetMarkerPosition(tilePosition), GetPlayerColor(property.OwnerPlayerIndex));
        }

        foreach (KeyValuePair<int, MarkerGroup> pair in markersByPosition)
        {
            pair.Value.Root.SetActive(activePositions.Contains(pair.Key));
        }
    }

    private bool ShouldShowMarker(GamePropertyStateData property)
    {
        if (property == null ||
            property.PositionIndex < 0 ||
            property.PositionIndex >= BoardSquareCount ||
            property.OwnerPlayerIndex < 0 ||
            property.Type != "City")
        {
            return false;
        }

        return property.HasHotel || property.HouseCount > 0;
    }

    private Vector2 GetMarkerPosition(Vector2 tilePosition)
    {
        Vector2 boardCenter = EstimateBoardCenter();
        Vector2 direction = tilePosition - boardCenter;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.up;

        direction.Normalize();
        return tilePosition + direction * MarkerDistanceFromTile;
    }

    private Vector2 EstimateBoardCenter()
    {
        Vector2 total = Vector2.zero;
        int count = 0;

        for (int i = 0; i < BoardSquareCount; i++)
        {
            if (TryGetBoardPointLocalPosition(i, out Vector2 position))
            {
                total += position;
                count++;
            }
        }

        return count == 0 ? Vector2.zero : total / count;
    }

    private bool TryGetBoardPointLocalPosition(int positionIndex, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;

        RectTransform boardPoint = FindBoardPoint(positionIndex);

        if (boardPoint == null)
            return false;

        Vector3 worldCenter = boardPoint.TransformPoint(boardPoint.rect.center);
        Camera uiCamera = null;
        Canvas canvas = canvasRect.GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCenter);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out localPosition);
    }

    private RectTransform FindBoardPoint(int positionIndex)
    {
        string paddedIndex = positionIndex.ToString("00");
        GameObject marker = FindSceneObjectByName($"BoardPoint_{paddedIndex}");

        if (marker == null)
            marker = FindSceneObjectByName($"BoardPoint_{positionIndex}");

        return marker == null ? null : marker.transform as RectTransform;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject candidate in objects)
        {
            if (candidate == null ||
                candidate.name != objectName ||
                !candidate.scene.IsValid() ||
                !candidate.scene.isLoaded)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private MarkerGroup GetOrCreateMarker(int positionIndex)
    {
        if (markersByPosition.TryGetValue(positionIndex, out MarkerGroup existing))
            return existing;

        GameObject rootObject = new GameObject($"BuildMarker_{positionIndex:00}", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(layerRoot, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(70f, 58f);

        List<Image> houseBars = new List<Image>();

        for (int i = 0; i < 3; i++)
        {
            GameObject barObject = new GameObject($"HouseBar_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.SetParent(rootRect, false);
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2((i - 1) * HouseBarSpacing, 0f);
            barRect.sizeDelta = new Vector2(HouseBarWidth, HouseBarHeight);

            Image barImage = barObject.GetComponent<Image>();
            barImage.raycastTarget = false;
            houseBars.Add(barImage);
        }

        GameObject hotelObject = new GameObject("HotelBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform hotelRect = hotelObject.GetComponent<RectTransform>();
        hotelRect.SetParent(rootRect, false);
        hotelRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotelRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotelRect.pivot = new Vector2(0.5f, 0.5f);
        hotelRect.anchoredPosition = Vector2.zero;
        hotelRect.sizeDelta = new Vector2(HotelBarWidth, HotelBarHeight);

        Image hotelImage = hotelObject.GetComponent<Image>();
        hotelImage.raycastTarget = false;

        MarkerGroup marker = new MarkerGroup(rootObject, rootRect, houseBars, hotelImage);
        markersByPosition[positionIndex] = marker;
        return marker;
    }

    private void HideAllMarkers()
    {
        foreach (KeyValuePair<int, MarkerGroup> pair in markersByPosition)
        {
            pair.Value.Root.SetActive(false);
        }
    }

    private Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0:
                return new Color(0.95f, 0.08f, 0.08f, 0.95f);
            case 1:
                return new Color(0.1f, 0.35f, 1f, 0.95f);
            case 2:
                return new Color(0.1f, 0.75f, 0.25f, 0.95f);
            case 3:
                return new Color(1f, 0.75f, 0.08f, 0.95f);
            default:
                return new Color(1f, 1f, 1f, 0.95f);
        }
    }

    private sealed class MarkerGroup
    {
        public readonly GameObject Root;
        private readonly RectTransform rootRect;
        private readonly List<Image> houseBars;
        private readonly Image hotelBar;

        public MarkerGroup(GameObject root, RectTransform rootRect, List<Image> houseBars, Image hotelBar)
        {
            Root = root;
            this.rootRect = rootRect;
            this.houseBars = houseBars;
            this.hotelBar = hotelBar;
        }

        public void SetState(GamePropertyStateData property, Vector2 position, Color color)
        {
            Root.SetActive(true);
            rootRect.anchoredPosition = position;

            hotelBar.gameObject.SetActive(property.HasHotel);
            hotelBar.color = color;

            int houseCount = Mathf.Clamp(property.HouseCount, 0, 3);

            for (int i = 0; i < houseBars.Count; i++)
            {
                Image bar = houseBars[i];
                bar.gameObject.SetActive(!property.HasHotel && i < houseCount);
                bar.color = color;
            }
        }
    }
}
