using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text txtRoomId;
    [SerializeField] private TMP_Text txtHostName;
    [SerializeField] private TMP_Text txtPlayerCount;
    [SerializeField] private TMP_Text txtBotCount;
    [SerializeField] private TMP_Text txtMapName;
    [SerializeField] private Button btnJoin;

    private string roomId;
    private LobbyManager lobbyManager;

    public void Setup(RoomSummaryData data, LobbyManager manager)
    {
        roomId = data.RoomId;
        lobbyManager = manager;

        if (txtRoomId != null)
            txtRoomId.text = $"Phòng #{data.RoomId}";

        if (txtHostName != null)
            txtHostName.text = $"Host: {data.HostUsername}";

        if (txtPlayerCount != null)
            txtPlayerCount.text = $"{data.CurrentPlayers}/{data.MaxPlayers} người";

        if (txtBotCount != null)
            txtBotCount.text = data.BotCount <= 0 ? "Không bot" : $"{data.BotCount} bot";

        if (txtMapName != null)
            txtMapName.text = $"Map: {data.MapName}";

        if (btnJoin != null)
        {
            btnJoin.onClick.RemoveAllListeners();
            btnJoin.onClick.AddListener(() =>
            {
                if (lobbyManager != null)
                    lobbyManager.OnBtnJoinSpecificRoomClicked(roomId);
            });
        }
    }
}