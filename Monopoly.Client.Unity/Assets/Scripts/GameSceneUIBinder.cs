using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneUIBinder : MonoBehaviour
{
    [Header("Gameplay Buttons")]
    [SerializeField] private Button btnRoll;
    [SerializeField] private Button btnBuy;
    [SerializeField] private Button btnEndTurn;

    [Header("Optional Text")]
    [SerializeField] private TextMeshProUGUI txtGameState;
    [SerializeField] private TextMeshProUGUI txtActionLog;
    [SerializeField] private TextMeshProUGUI txtError;

    private void Start()
    {
        Register();
        BoardTokenManager.EnsureExists();
        GameChatUI.EnsureExists();
        BoardTileInfoUI.EnsureExists();
        PropertyBuildMarkerUI.EnsureExists();
        DiceVisualUI.EnsureExists();
        PlayerHandUI.EnsureExists().Refresh(GameSession.CurrentState);
        PlayerInfoLayerUI.EnsureExists();
        MoneyFlowUI.EnsureExists();
        GameOverUI.EnsureExists();
        GameEventPopupUI.EnsureExists().ResetEvents();
        PropertySaleUI.EnsureExists().Refresh(GameSession.CurrentState);
        AudioManager.EnsureExists().PlayMusic("bgm");
        GameSettingsUI.EnsureExists().Refresh(GameSession.CurrentState);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.UnregisterGameplayUi();
    }

    private void Register()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogWarning("[GameSceneUIBinder] NetworkManager.Instance is null.");
            return;
        }

        NetworkManager.Instance.RegisterGameplayUi(
            btnRoll,
            btnBuy,
            btnEndTurn,
            txtGameState,
            txtActionLog,
            txtError
        );
    }
}
