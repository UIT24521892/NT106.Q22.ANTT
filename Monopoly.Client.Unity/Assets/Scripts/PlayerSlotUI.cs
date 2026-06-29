using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text txtUsername;
    [SerializeField] private TMP_Text txtStatus;
    [SerializeField] private Image imgReadyIndicator;
    [SerializeField] private GameObject crownIcon;

    [Header("Avatar")]
    [SerializeField] private Image imgAvatar;
    [SerializeField] private Sprite[] avatarSprites;

    private readonly string[] avatarIds =
    {
        "avatar_1",
        "avatar_2",
        "avatar_3",
        "avatar_4"
    };

    private static readonly Color ColorReady = new Color(0.2f, 0.85f, 0.3f);
    private static readonly Color ColorNotReady = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color ColorBot = new Color(0.5f, 0.5f, 0.9f);
    private static readonly Color ColorUsername = new Color(0.12f, 0.08f, 0.04f, 1f);

    public void Setup(string username, bool isReady, bool isHost, bool isBot, string avatarId)
    {
        AutoBindMissingReferences();
        NormalizeSlotVisuals();

        if (txtUsername != null)
        {
            txtUsername.text = isBot ? $"[BOT] {username}" : username;
            txtUsername.color = ColorUsername;
            txtUsername.transform.SetAsLastSibling();
        }

        if (txtStatus != null)
        {
            if (isBot)
            {
                txtStatus.text = "BOT";
                txtStatus.color = ColorBot;
            }
            else
            {
                txtStatus.text = isReady ? "READY" : "WAITING";
                txtStatus.color = isReady ? ColorReady : ColorNotReady;
            }

            txtStatus.transform.SetAsLastSibling();
        }

        if (imgReadyIndicator != null)
            imgReadyIndicator.color = isBot ? ColorBot : (isReady ? ColorReady : ColorNotReady);

        if (crownIcon != null)
        {
            crownIcon.SetActive(isHost);
            crownIcon.transform.SetAsLastSibling();
        }

        ApplyAvatar(avatarId);
    }

    private void ApplyAvatar(string avatarId)
    {
        if (imgAvatar == null || avatarSprites == null || avatarSprites.Length == 0)
            return;

        if (string.IsNullOrWhiteSpace(avatarId))
            avatarId = "avatar_1";

        int index = System.Array.IndexOf(avatarIds, avatarId);

        if (index < 0)
            index = 0;

        if (index < avatarSprites.Length && avatarSprites[index] != null)
        {
            imgAvatar.sprite = avatarSprites[index];
            imgAvatar.preserveAspect = true;
        }
    }

    private void AutoBindMissingReferences()
    {
        Transform usernameTransform = transform.Find("Txt_Username");
        Transform statusTransform = transform.Find("Txt_Status");
        Transform readyTransform = transform.Find("Img_ReadyDot");
        Transform avatarTransform = transform.Find("Img_Avatar");
        Transform crownTransform = transform.Find("Icon_Crown");

        if (txtUsername == null && usernameTransform != null)
            txtUsername = usernameTransform.GetComponent<TMP_Text>();

        if (txtStatus == null && statusTransform != null)
            txtStatus = statusTransform.GetComponent<TMP_Text>();

        if (imgReadyIndicator == null && readyTransform != null)
            imgReadyIndicator = readyTransform.GetComponent<Image>();

        if (imgAvatar == null && avatarTransform != null)
            imgAvatar = avatarTransform.GetComponent<Image>();

        if (crownIcon == null && crownTransform != null)
            crownIcon = crownTransform.gameObject;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (txtUsername == null)
        {
            foreach (TMP_Text text in texts)
            {
                if (text.name.ToLower().Contains("username"))
                {
                    txtUsername = text;
                    break;
                }
            }
        }

        if (txtStatus == null)
        {
            foreach (TMP_Text text in texts)
            {
                if (text != txtUsername)
                {
                    txtStatus = text;
                    break;
                }
            }
        }

        if (imgReadyIndicator == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image.name.ToLower().Contains("ready"))
                {
                    imgReadyIndicator = image;
                    break;
                }
            }
        }

        if (imgAvatar == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image.name.ToLower().Contains("avatar"))
                {
                    imgAvatar = image;
                    break;
                }
            }
        }
    }

    private void NormalizeSlotVisuals()
    {
        if (imgReadyIndicator != null)
        {
            imgReadyIndicator.raycastTarget = false;

            RectTransform readyRect = imgReadyIndicator.GetComponent<RectTransform>();

            if (readyRect != null && (readyRect.sizeDelta.x > 36f || readyRect.sizeDelta.y > 36f))
            {
                readyRect.sizeDelta = new Vector2(24f, 24f);
            }
        }

        if (imgAvatar != null)
        {
            imgAvatar.raycastTarget = false;
            imgAvatar.preserveAspect = true;
        }

        ConfigureText(txtUsername);
        ConfigureText(txtStatus);
    }

    private void ConfigureText(TMP_Text text)
    {
        if (text == null) return;

        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }
}