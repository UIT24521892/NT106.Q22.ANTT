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
        root.SetActive(true);
    }

    public void ShowLeaderboard(LeaderboardData leaderboard)
    {
        if (root == null)
            BuildUi();

        titleText.text = "Bảng xếp hạng";
        matchText.text = "Top người chơi toàn server";
        contentText.text = BuildLeaderboardText(leaderboard?.Entries);
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

        root = new GameObject("Panel_GameOver");
        root.transform.SetParent(canvas.transform, false);

        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject panel = new GameObject("GameOverCard");
        panel.transform.SetParent(root.transform, false);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.16f, 0.11f, 0.94f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(580f, 410f);
        panelRect.anchoredPosition = Vector2.zero;

        titleText = CreateText(panel.transform, "Txt_Title", new Vector2(0f, 150f), new Vector2(520f, 46f), 30, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;

        matchText = CreateText(panel.transform, "Txt_MatchId", new Vector2(0f, 108f), new Vector2(520f, 26f), 15, TextAlignmentOptions.Center);
        contentText = CreateText(panel.transform, "Txt_Content", new Vector2(0f, -15f), new Vector2(500f, 210f), 22, TextAlignmentOptions.TopLeft);

        Button leaderboardButton = CreateButton(panel.transform, "Btn_Leaderboard", "Bảng xếp hạng", new Vector2(-115f, -165f), new Vector2(210f, 48f));
        leaderboardButton.onClick.AddListener(RequestLeaderboard);

        Button lobbyButton = CreateButton(panel.transform, "Btn_ReturnLobby", "Về Lobby", new Vector2(125f, -165f), new Vector2(170f, 48f));
        lobbyButton.onClick.AddListener(ReturnToLobby);
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.red;
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
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.45f, 0.18f, 1f);

        Button button = go.AddComponent<Button>();
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = CreateText(go.transform, "Text", Vector2.zero, size, 22, TextAlignmentOptions.Center);
        text.text = label;
        text.fontStyle = FontStyles.Bold;

        return button;
    }
}
