using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject usernamePanel;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject matchLobbyPanel;
    [SerializeField] private GameObject connectingPanel;

    [Header("Username Panel")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button confirmUsernameButton;

    [Header("Lobby List Panel")]
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private GameObject lobbyEntryPrefab;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button backButton;

    [Header("Match Lobby Panel")]
    [SerializeField] private TextMeshProUGUI hostNameText;
    [SerializeField] private TextMeshProUGUI hostStatsText;
    [SerializeField] private TextMeshProUGUI clientNameText;
    [SerializeField] private TextMeshProUGUI clientStatsText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private const string USERNAME_PREF = "PlayerUsername";
    private bool isInRoom = false;

    private void Start()
    {
        SetupUI();
        LoadUsername();
        ShowConnectingPanel(true);
    }

    private void SetupUI()
    {
        confirmUsernameButton.onClick.AddListener(OnUsernameConfirmed);
        createLobbyButton.onClick.AddListener(OnCreateRoom);
        refreshButton.onClick.AddListener(OnRefreshClicked);
        backButton.onClick.AddListener(OnBackClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveRoom);

        ShowUsernamePanel();
    }

    private void LoadUsername()
    {
        string savedUsername = PlayerPrefs.GetString(USERNAME_PREF, "");
        if (!string.IsNullOrEmpty(savedUsername))
        {
            usernameInput.text = savedUsername;
        }
    }

    #region Panel Management

    private void ShowConnectingPanel(bool show)
    {
        if (connectingPanel != null)
        {
            connectingPanel.SetActive(show);
        }
    }

    private void ShowUsernamePanel()
    {
        usernamePanel.SetActive(true);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(false);
    }

    private void ShowLobbyListPanel()
    {
        usernamePanel.SetActive(false);
        lobbyListPanel.SetActive(true);
        matchLobbyPanel.SetActive(false);
    }

    private void ShowMatchLobbyPanel()
    {
        usernamePanel.SetActive(false);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(true);
        readyButton.interactable = true;
    }

    #endregion

    #region Button Handlers

    private void OnUsernameConfirmed()
    {
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username)) return;

        PlayerPrefs.SetString(USERNAME_PREF, username);
        PlayerPrefs.Save();
        ShowLobbyListPanel();
    }

    private void OnCreateRoom()
    {
        PhotonManager.Instance.CreateRoom(usernameInput.text);
    }

    private void OnRefreshClicked()
    {
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnBackClicked()
    {
        ShowUsernamePanel();
    }

    private void OnReadyClicked()
    {
        readyButton.interactable = false;
        PhotonManager.Instance.SetPlayerReady(true);
    }

    private void OnLeaveRoom()
    {
        if (isInRoom)
        {
            PhotonManager.Instance.LeaveRoom();
        }
    }

    #endregion

    #region Photon Callbacks

    public void OnPhotonConnected()
    {
        ShowConnectingPanel(false);
        Debug.Log("Connected to Photon Lobby");
    }

    public void OnRoomJoined(bool isMasterClient)
    {
        isInRoom = true;
        ShowMatchLobbyPanel();
        
        leaveLobbyButton.gameObject.SetActive(true);
        readyButton.interactable = true;
        
        if (isMasterClient)
        {
            statusText.text = "Waiting for opponent...";
            UpdateHostInfo(usernameInput.text, 0, 0);
            clientNameText.text = "Waiting for player...";
            clientStatsText.text = "";
        }
        else
        {
            hostNameText.text = "Host";
            hostStatsText.text = "Wins: 0 Losses: 0";
            clientNameText.text = usernameInput.text;
            clientStatsText.text = "Wins: 0 Losses: 0";
            statusText.text = "Waiting for players to ready up...";
        }
    }

    public void UpdateRoomList(List<RoomInfo> roomList)
    {
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        foreach (RoomInfo room in roomList)
        {
            if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
            {
                GameObject entryObj = Instantiate(lobbyEntryPrefab, lobbyListContent);
                var entryUI = entryObj.GetComponent<LobbyEntryUI>();
                if (entryUI != null)
                {
                    entryUI.Initialize(room.Name, () => OnJoinRoomClicked(room.Name));
                }
            }
        }
    }

    private void OnJoinRoomClicked(string roomName)
    {
        PhotonManager.Instance.JoinRoom(roomName, usernameInput.text);
    }

    public void OnPlayerJoinedRoom(Player newPlayer)
    {
        if (matchLobbyPanel.activeSelf)
        {
            clientNameText.text = newPlayer.NickName;
            clientStatsText.text = "Wins: 0 Losses: 0";
            statusText.text = "Waiting for players to ready up...";
        }
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (matchLobbyPanel.activeSelf)
        {
            clientNameText.text = "Waiting for player...";
            clientStatsText.text = "";
            statusText.text = "Opponent left the room";
            readyButton.interactable = true;
        }
    }

    public void OnRoomLeft()
    {
        isInRoom = false;
        ShowLobbyListPanel();
    }

    public void OnDisconnected()
    {
        isInRoom = false;
        ShowUsernamePanel();
        ShowConnectingPanel(true);
    }

    public void UpdatePlayerReadyState(Player player, bool isReady)
    {
        if (!matchLobbyPanel.activeSelf) return;

        if (player.IsMasterClient)
        {
            statusText.text = isReady ? "Host is Ready!" : "Waiting for host...";
        }
        else
        {
            statusText.text = isReady ? "Opponent is Ready!" : "Waiting for opponent...";
        }
    }

    #endregion

    private void UpdateHostInfo(string username, int wins, int losses)
    {
        hostNameText.text = username;
        hostStatsText.text = $"Wins: {wins} Losses: {losses}";
    }
}