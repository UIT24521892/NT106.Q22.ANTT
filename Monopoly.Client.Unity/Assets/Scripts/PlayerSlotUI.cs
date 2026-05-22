using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text txtUsername;
    [SerializeField] private TMP_Text txtStatus;
    [SerializeField] private Image imgReadyIndicator;
    [SerializeField] private GameObject crownIcon;

    private static readonly Color ColorReady = new Color(0.2f, 0.85f, 0.3f);
    private static readonly Color ColorNotReady = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color ColorBot = new Color(0.5f, 0.5f, 0.9f);

    public void Setup(string username, bool isReady, bool isHost, bool isBot)
    {
        AutoBindMissingReferences();

        if (txtUsername != null)
            txtUsername.text = isBot ? $"[BOT] {username}" : username;

        if (txtStatus != null)
        {
            if (isBot) { txtStatus.text = "Bot tự động"; txtStatus.color = ColorBot; }
            else
            {
                txtStatus.text = isReady ? "✓ Sẵn sàng" : "⏳ Đang chờ...";
                txtStatus.color = isReady ? ColorReady : ColorNotReady;
            }
        }

        if (imgReadyIndicator != null)
            imgReadyIndicator.color = isBot ? ColorBot : (isReady ? ColorReady : ColorNotReady);

        if (crownIcon != null)
            crownIcon.SetActive(isHost);
    }

    private void AutoBindMissingReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (txtUsername == null && texts.Length > 0)
            txtUsername = texts[0];

        if (txtStatus == null && texts.Length > 1)
            txtStatus = texts[1];

        if (imgReadyIndicator == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image.gameObject != gameObject)
                {
                    imgReadyIndicator = image;
                    break;
                }
            }
        }
    }
}
