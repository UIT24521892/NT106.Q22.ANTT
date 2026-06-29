using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DiceVisualUI : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const float RollAnimationSeconds = 1.25f;
    private const float RollFaceChangeInterval = 0.08f;
    private const float RollSpinDegreesPerSecond = 360f;
    private const string DiceFaceResourceFolder = "DiceFaces";

    private GameObject dicePanel;
    private Image dice1Image;
    private Image dice2Image;
    private TextMeshProUGUI dice1Text;
    private TextMeshProUGUI dice2Text;
    private TextMeshProUGUI totalText;
    private readonly Sprite[] diceFaceSprites = new Sprite[7];
    private Coroutine rollRoutine;
    private float nextRefreshTime;
    private string lastDiceKey = "";

    public static DiceVisualUI EnsureExists()
    {
        DiceVisualUI existing = FindObjectOfType<DiceVisualUI>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("DiceVisualUI");
        return host.AddComponent<DiceVisualUI>();
    }

    private void Start()
    {
        BindPanel();
        Refresh(force: true);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh(force: false);
    }

    private void BindPanel()
    {
        dicePanel = FindSceneObjectByName("DicePanel");

        if (dicePanel == null)
        {
            Debug.LogWarning("[DiceVisualUI] DicePanel not found. Create it under CenterActionLayer or Panel_GameScene.");
            return;
        }

        dice1Image = FindChildComponent<Image>(dicePanel.transform, "Img_Dice1");
        dice2Image = FindChildComponent<Image>(dicePanel.transform, "Img_Dice2");
        totalText = FindChildComponent<TextMeshProUGUI>(dicePanel.transform, "Txt_DiceTotal");

        if (dice1Image == null || dice2Image == null)
        {
            Debug.LogWarning("[DiceVisualUI] DicePanel needs Img_Dice1 and Img_Dice2.");
            return;
        }

        dice1Text = FindChildComponent<TextMeshProUGUI>(dice1Image.transform, "Txt_Dice1");
        dice2Text = FindChildComponent<TextMeshProUGUI>(dice2Image.transform, "Txt_Dice2");

        if (dice1Text == null)
            dice1Text = CreateDiceNumberText("Txt_Dice1", dice1Image.transform);

        if (dice2Text == null)
            dice2Text = CreateDiceNumberText("Txt_Dice2", dice2Image.transform);

        LoadDiceFaceSprites();
        dicePanel.SetActive(true);
    }

    private void Refresh(bool force)
    {
        if (dicePanel == null || dice1Text == null || dice2Text == null)
        {
            if (force)
                BindPanel();

            return;
        }

        GameStateData state = GameSession.CurrentState;

        if (state == null || state.LastDiceTotal <= 0)
        {
            SetDiceValues(0, 0, 0);
            return;
        }

        string diceKey = $"{state.TurnNumber}:{state.LastMovedPlayerIndex}:{state.LastDice1}:{state.LastDice2}:{state.LastDiceTotal}";

        if (!force && diceKey == lastDiceKey)
            return;

        lastDiceKey = diceKey;

        if (rollRoutine != null)
            StopCoroutine(rollRoutine);

        AudioManager.EnsureExists().PlaySfx("dice");
        rollRoutine = StartCoroutine(AnimateToServerDice(state.LastDice1, state.LastDice2, state.LastDiceTotal));
    }

    private IEnumerator AnimateToServerDice(int dice1, int dice2, int total)
    {
        float elapsed = 0f;
        float nextFaceChangeTime = 0f;

        while (elapsed < RollAnimationSeconds)
        {
            if (elapsed >= nextFaceChangeTime)
            {
                SetDiceValues(Random.Range(1, 7), Random.Range(1, 7), 0);
                nextFaceChangeTime = elapsed + RollFaceChangeInterval;
            }

            SetDiceRotation(elapsed * RollSpinDegreesPerSecond);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetDiceRotation(0f);
        SetDiceValues(dice1, dice2, total);
        rollRoutine = null;
    }

    private void SetDiceRotation(float zDegrees)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, zDegrees);

        if (dice1Image != null)
            dice1Image.rectTransform.localRotation = rotation;

        if (dice2Image != null)
            dice2Image.rectTransform.localRotation = rotation;
    }

    private void SetDiceValues(int dice1, int dice2, int total)
    {
        ApplyDiceFace(dice1Image, dice1Text, dice1);
        ApplyDiceFace(dice2Image, dice2Text, dice2);

        if (totalText != null)
            totalText.text = total <= 0 ? "" : $"Total: {total}";
    }

    private void ApplyDiceFace(Image image, TextMeshProUGUI fallbackText, int diceValue)
    {
        if (diceValue <= 0)
        {
            if (image != null)
                image.enabled = false;

            if (fallbackText != null)
                fallbackText.gameObject.SetActive(false);

            return;
        }

        Sprite sprite = diceValue >= 1 && diceValue <= 6 ? diceFaceSprites[diceValue] : null;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.enabled = true;

            if (fallbackText != null)
                fallbackText.gameObject.SetActive(false);

            return;
        }

        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(true);
            fallbackText.text = diceValue.ToString();
        }
    }

    private void LoadDiceFaceSprites()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(DiceFaceResourceFolder);

        foreach (Sprite sprite in sprites)
        {
            int diceValue = ParseDiceValue(sprite.name);

            if (diceValue >= 1 && diceValue <= 6)
                diceFaceSprites[diceValue] = sprite;
        }

        for (int i = 1; i <= 6; i++)
        {
            if (diceFaceSprites[i] == null)
            {
                diceFaceSprites[i] =
                    Resources.Load<Sprite>($"{DiceFaceResourceFolder}/Dice_{i}") ??
                    Resources.Load<Sprite>($"{DiceFaceResourceFolder}/dice_{i}") ??
                    Resources.Load<Sprite>($"{DiceFaceResourceFolder}/Dice-{i}") ??
                    Resources.Load<Sprite>($"{DiceFaceResourceFolder}/dice-{i}");
            }

            if (diceFaceSprites[i] == null)
                Debug.LogWarning($"[DiceVisualUI] Missing dice sprite {i}. Expected under Assets/Resources/DiceFaces.");
        }
    }

    private int ParseDiceValue(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return -1;

        string[] tokens = spriteName.Split('-', '_');

        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(tokens[i], out int diceValue) && diceValue >= 1 && diceValue <= 6)
                return diceValue;
        }

        return -1;
    }

    private TextMeshProUGUI CreateDiceNumberText(string name, Transform parent)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 34f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        Transform child = FindChildByName(root, childName);
        return child == null ? null : child.GetComponent<T>();
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);

            if (result != null)
                return result;
        }

        return null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject candidate in objects)
        {
            if (candidate == null ||
                candidate.name != objectName ||
                !candidate.scene.IsValid() ||
                !candidate.scene.isLoaded ||
                candidate.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }
}
