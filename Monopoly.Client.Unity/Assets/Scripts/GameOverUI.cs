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
    private TextMeshProUGUI rankingText;

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
        rankingText.text = BuildRankingText(gameOver?.Rankings);
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

    private void Hide()
    {
        if (root != null)
            root.SetActive(false);
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
        panelRect.sizeDelta = new Vector2(520f, 360f);
        panelRect.anchoredPosition = Vector2.zero;

        titleText = CreateText(panel.transform, "Txt_Title", new Vector2(0f, 125f), new Vector2(460f, 46f), 30, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;

        matchText = CreateText(panel.transform, "Txt_MatchId", new Vector2(0f, 83f), new Vector2(460f, 26f), 15, TextAlignmentOptions.Center);
        rankingText = CreateText(panel.transform, "Txt_Rankings", new Vector2(0f, -20f), new Vector2(440f, 160f), 22, TextAlignmentOptions.TopLeft);

        Button lobbyButton = CreateButton(panel.transform, "Btn_ReturnLobby", "Về Lobby", new Vector2(0f, -135f), new Vector2(190f, 48f));
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
