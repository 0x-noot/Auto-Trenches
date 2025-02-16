using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;
using Photon.Pun;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject usernamePanel;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject matchLobbyPanel;

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
    }

    private void SetupUI()
    {
        // Setup button listeners
        confirmUsernameButton.onClick.AddListener(OnUsernameConfirmed);
        createLobbyButton.onClick.AddListener(OnCreateRoom);
        refreshButton.onClick.AddListener(OnRefreshClicked);
        backButton.onClick.AddListener(OnBackClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveRoom);

        // Initially show username panel
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
        // Photon automatically updates room list
        // We just need to clear the current list visually
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
        // Called when connected to Photon
        Debug.Log("Connected to Photon Lobby");
    }

    public void OnRoomJoined(bool isMasterClient)
    {
        isInRoom = true;
        ShowMatchLobbyPanel();
        
        // Update UI based on whether we're host or client
        leaveLobbyButton.gameObject.SetActive(!isMasterClient);
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
            // Update client view
            var host = PhotonNetwork.MasterClient;
            hostNameText.text = host.NickName;
            hostStatsText.text = "Wins: 0 Losses: 0";
            clientNameText.text = usernameInput.text;
            clientStatsText.text = "Wins: 0 Losses: 0";
            statusText.text = "Waiting for players to ready up...";
        }
    }

    public void UpdateRoomList(List<RoomInfo> roomList)
    {
        // Clear existing entries
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        // Create new entries for each room
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
    }

    #endregion

    private void UpdateHostInfo(string username, int wins, int losses)
    {
        hostNameText.text = username;
        hostStatsText.text = $"Wins: {wins} Losses: {losses}";
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
}