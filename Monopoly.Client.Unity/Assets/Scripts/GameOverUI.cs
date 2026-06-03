using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    private static GameOverUI instance;

    private GameObject root;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI matchText;
    private TextMeshProUGUI contentText;

    public static GameOverUI EnsureExists()
    {
        if (instance != null)
            return instance;

        GameObject go = new GameObject("GameOverUI");
        instance = go.AddComponent<GameOverUI>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
        Hide();
    }

    public void Show(GameOverData gameOver)
    {
        if (root == null)
            BuildUi();

        titleText.text = "Kết thúc trận";
        matchText.text = string.IsNullOrWhiteSpace(gameOver?.MatchId)
            ? "Match: N/A"
            : $"Match: {gameOver.MatchId}";
        contentText.text = BuildRankingText(gameOver?.Rankings);
        root.transform.SetAsLastSibling();
        root.SetActive(true);
    }

    public void ShowLeaderboard(LeaderboardData leaderboard)
    {
        if (root == null)
            BuildUi();

        titleText.text = "Bảng xếp hạng";
        matchText.text = "Top người chơi toàn server";
        contentText.text = BuildLeaderboardText(leaderboard?.Entries);
        root.transform.SetAsLastSibling();
        root.SetActive(true);
    }

    private string BuildRankingText(List<RankingEntryData> rankings)
    {
        if (rankings == null || rankings.Count == 0)
            return "Chưa có dữ liệu xếp hạng.";

        StringBuilder builder = new StringBuilder();

        foreach (RankingEntryData entry in rankings)
        {
            if (entry == null)
                continue;

            string name = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.UserId
                : entry.DisplayName;

            builder.Append("#")
                .Append(entry.Rank)
                .Append("  ")
                .Append(name)
                .Append("  |  +")
                .Append(entry.ScoreEarned)
                .Append(" điểm")
                .AppendLine();
        }

        return builder.ToString();
    }

    private string BuildLeaderboardText(List<LeaderboardEntryData> entries)
    {
        if (entries == null || entries.Count == 0)
            return "Chưa có dữ liệu bảng xếp hạng.";

        StringBuilder builder = new StringBuilder();

        foreach (LeaderboardEntryData entry in entries)
        {
            if (entry == null)
                continue;

            string name = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.UserId
                : entry.DisplayName;

            builder.Append("#")
                .Append(entry.Rank)
                .Append("  ")
                .Append(name)
                .Append("  |  ")
                .Append(entry.Score)
                .Append(" điểm")
                .Append("  |  Thắng ")
                .Append(entry.Wins)
                .Append("/")
                .Append(entry.TotalMatches)
                .AppendLine();
        }

        return builder.ToString();
    }

    private void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void RequestLeaderboard()
    {
        if (NetworkManager.Instance == null)
        {
            ShowLeaderboard(null);
            return;
        }

        titleText.text = "Bảng xếp hạng";
        matchText.text = "Đang tải...";
        contentText.text = "";
        NetworkManager.Instance.RequestLeaderboard();
    }

    private void ReturnToLobby()
    {
        NetworkManager.Instance?.SendLeaveRoomRequest();
        SceneManager.LoadScene("LobbyScene");
    }

    private void BuildUi()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        root = new GameObject("Panel_GameOver", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        Image overlay = root.GetComponent<Image>();
        overlay.color = new Color(0.02f, 0.04f, 0.05f, 0.62f);
        overlay.raycastTarget = true;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject panel = new GameObject("GameOverCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(root.transform, false);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.95f, 0.93f, 0.88f, 0.99f);
        panelImage.raycastTarget = true;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 460f);
        panelRect.anchoredPosition = Vector2.zero;

        Image header = CreatePanelImage(panel.transform, "Img_Header", new Color(0.08f, 0.45f, 0.68f, 1f));
        SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 92f));

        titleText = CreateText(panel.transform, "Txt_Title", new Vector2(0f, -34f), new Vector2(-60f, 46f), 32, TextAlignmentOptions.Center, Color.white);
        titleText.fontStyle = FontStyles.Bold;
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(-60f, 46f));

        matchText = CreateText(panel.transform, "Txt_MatchId", new Vector2(0f, -118f), new Vector2(600f, 28f), 17, TextAlignmentOptions.Center, new Color(0.18f, 0.18f, 0.18f, 1f));
        SetRect(matchText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-70f, 28f));

        contentText = CreateText(panel.transform, "Txt_Content", Vector2.zero, new Vector2(560f, 210f), 22, TextAlignmentOptions.TopLeft, new Color(0.08f, 0.08f, 0.08f, 1f));
        SetStretch(contentText.rectTransform, 70f, 154f, 70f, 104f);

        Button leaderboardButton = CreateButton(panel.transform, "Btn_Leaderboard", "Bảng xếp hạng", new Vector2(-130f, 42f), new Vector2(230f, 54f));
        leaderboardButton.onClick.AddListener(RequestLeaderboard);

        Button lobbyButton = CreateButton(panel.transform, "Btn_ReturnLobby", "Về Lobby", new Vector2(150f, 42f), new Vector2(180f, 54f));
        lobbyButton.onClick.AddListener(ReturnToLobby);
    }

    private Image CreatePanelImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.08f, 0.45f, 0.68f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = CreateText(go.transform, "Text", Vector2.zero, size, 22, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
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

    private void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
