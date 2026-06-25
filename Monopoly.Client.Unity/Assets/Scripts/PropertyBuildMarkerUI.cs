using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PropertyBuildMarkerUI : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;
    private const int BoardSquareCount = 32;
    private const float MarkerDistanceFromTile = 46f;
    private const float HouseWidth = 24f;
    private const float HouseHeight = 30f;
    private const float HouseStep = 25f;   // khoảng cách tâm giữa các căn khi xếp hàng ngang
    private const int MaxHouses = 3;
    private const float HotelWidth = 78f;  // khách sạn: 1 toà dài, to ra
    private const float HotelHeight = 46f;

    private readonly Dictionary<int, MarkerGroup> markersByPosition = new Dictionary<int, MarkerGroup>();
    private RectTransform layerRoot;
    private RectTransform canvasRect;
    private float nextRefreshTime;

    // Sprite nhà/khách sạn (tùy chọn). Nếu thiếu file -> fallback về thanh màu.
    private Sprite houseSprite;
    private Sprite hotelSprite;

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
        houseSprite = Resources.Load<Sprite>("UI/house");
        if (houseSprite == null)
            houseSprite = CreateHouseSprite();

        hotelSprite = Resources.Load<Sprite>("UI/hotel");
        if (hotelSprite == null)
            hotelSprite = CreateHotelSprite();

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
        rootRect.sizeDelta = new Vector2(90f, 60f);

        // 1..3 ngôi nhà xếp hàng ngang theo số lượng; khách sạn = 1 toà dài thay cho cụm nhà.
        List<Image> houseImages = new List<Image>();

        for (int i = 0; i < MaxHouses; i++)
        {
            GameObject houseObject = new GameObject($"House_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform houseRect = houseObject.GetComponent<RectTransform>();
            houseRect.SetParent(rootRect, false);
            houseRect.anchorMin = new Vector2(0.5f, 0.5f);
            houseRect.anchorMax = new Vector2(0.5f, 0.5f);
            houseRect.pivot = new Vector2(0.5f, 0.5f);
            houseRect.sizeDelta = new Vector2(HouseWidth, HouseHeight);

            Image houseImage = houseObject.GetComponent<Image>();
            houseImage.raycastTarget = false;
            houseImage.preserveAspect = true;
            houseImage.sprite = houseSprite;
            houseImages.Add(houseImage);
        }

        GameObject hotelObject = new GameObject("Hotel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform hotelRect = hotelObject.GetComponent<RectTransform>();
        hotelRect.SetParent(rootRect, false);
        hotelRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotelRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotelRect.pivot = new Vector2(0.5f, 0.5f);
        hotelRect.anchoredPosition = Vector2.zero;
        hotelRect.sizeDelta = new Vector2(HotelWidth, HotelHeight);

        Image hotelImage = hotelObject.GetComponent<Image>();
        hotelImage.raycastTarget = false;
        hotelImage.preserveAspect = true;
        hotelImage.sprite = hotelSprite;

        MarkerGroup marker = new MarkerGroup(rootObject, rootRect, houseImages, hotelImage);
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

    // Vẽ một ngôi nhà (thân vuông + mái tam giác) bằng code; trắng/xám để Image.color tô theo chủ.
    private static Sprite CreateHouseSprite()
    {
        const int width = 64;
        const int height = 76;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color body = Color.white;                       // thân: sáng nhất
        Color roof = new Color(0.68f, 0.68f, 0.68f, 1f); // mái: xám -> tối hơn khi tô màu
        Color door = new Color(0.4f, 0.4f, 0.4f, 1f);    // cửa

        float bodyTop = height * 0.52f;       // thân từ đáy lên 52% chiều cao
        float bodyLeft = width * 0.17f;
        float bodyRight = width * 0.83f;
        float apexY = height - 1f;            // đỉnh mái ở trên cùng
        float roofBaseHalf = width * 0.5f;    // mái rộng hết khổ ở chân (nhô ra khỏi thân)
        float centerX = width * 0.5f;
        float doorLeft = width * 0.42f;
        float doorRight = width * 0.58f;
        float doorTop = bodyTop * 0.55f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = clear;

                if (y <= bodyTop)
                {
                    if (x >= bodyLeft && x <= bodyRight)
                        pixel = (x >= doorLeft && x <= doorRight && y <= doorTop) ? door : body;
                }
                else
                {
                    float t = (y - bodyTop) / (apexY - bodyTop);
                    float halfWidth = Mathf.Lerp(roofBaseHalf, 0f, t);

                    if (Mathf.Abs(x - centerX) <= halfWidth)
                        pixel = roof;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    // Vẽ khách sạn: 1 toà nhà dài (thân chữ nhật rộng + mái thang nông). Trắng/xám để tô màu.
    private static Sprite CreateHotelSprite()
    {
        const int width = 96;
        const int height = 56;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color body = Color.white;
        Color roof = new Color(0.66f, 0.66f, 0.66f, 1f);
        Color window = new Color(0.42f, 0.42f, 0.42f, 1f);

        float bodyTop = height * 0.66f;       // thân cao 66% -> toà nhà bè ra
        float bodyLeft = width * 0.06f;
        float bodyRight = width * 0.94f;
        float roofTopY = height - 1f;
        float roofBaseHalf = width * 0.5f;    // mái rộng hết khổ ở chân
        float roofTopHalf = width * 0.30f;    // đỉnh mái phẳng (hình thang)
        float centerX = width * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = clear;

                if (y <= bodyTop)
                {
                    if (x >= bodyLeft && x <= bodyRight)
                    {
                        // Hàng cửa sổ để nhìn ra "khách sạn".
                        float wy = y / bodyTop;
                        float wx = (x - bodyLeft) / (bodyRight - bodyLeft);
                        bool windowRow = (wy > 0.25f && wy < 0.5f) || (wy > 0.62f && wy < 0.87f);
                        bool windowCol = Mathf.Repeat(wx * 6f, 1f) < 0.5f;
                        pixel = (windowRow && windowCol) ? window : body;
                    }
                }
                else
                {
                    float t = (y - bodyTop) / (roofTopY - bodyTop);
                    float halfWidth = Mathf.Lerp(roofBaseHalf, roofTopHalf, t);

                    if (Mathf.Abs(x - centerX) <= halfWidth)
                        pixel = roof;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private sealed class MarkerGroup
    {
        public readonly GameObject Root;
        private readonly RectTransform rootRect;
        private readonly List<Image> houseImages;
        private readonly Image hotelImage;

        public MarkerGroup(GameObject root, RectTransform rootRect, List<Image> houseImages, Image hotelImage)
        {
            Root = root;
            this.rootRect = rootRect;
            this.houseImages = houseImages;
            this.hotelImage = hotelImage;
        }

        public void SetState(GamePropertyStateData property, Vector2 position, Color color)
        {
            Root.SetActive(true);
            rootRect.anchoredPosition = position;

            if (property.HasHotel)
            {
                hotelImage.gameObject.SetActive(true);
                hotelImage.color = new Color(0.85f, 0.12f, 0.12f, 1f); // khách sạn: đỏ

                foreach (Image house in houseImages)
                    house.gameObject.SetActive(false);

                return;
            }

            hotelImage.gameObject.SetActive(false);

            int houseCount = Mathf.Clamp(property.HouseCount, 1, MaxHouses);

            for (int i = 0; i < houseImages.Count; i++)
            {
                Image house = houseImages[i];
                bool show = i < houseCount;
                house.gameObject.SetActive(show);

                if (!show)
                    continue;

                house.color = color;
                // Căn giữa cụm: 1 căn ở giữa, 2/3 căn xếp đối xứng quanh tâm.
                house.rectTransform.anchoredPosition = new Vector2((i - (houseCount - 1) * 0.5f) * HouseStep, 0f);
            }
        }
    }
}
