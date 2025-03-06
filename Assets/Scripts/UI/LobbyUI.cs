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
    
    // Add a dictionary to keep track of cached rooms
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    private void Awake()
    {
        // Make sure only username panel is visible initially if there's no saved username
        string savedUsername = PlayerPrefs.GetString(USERNAME_PREF, "");
        if (string.IsNullOrEmpty(savedUsername))
        {
            usernamePanel.SetActive(true);
            lobbyListPanel.SetActive(false);
            matchLobbyPanel.SetActive(false);
        }
        else
        {
            usernamePanel.SetActive(false);
        }
        
        // Make sure connecting panel is behind other panels in hierarchy
        if (connectingPanel != null)
        {
            connectingPanel.transform.SetAsFirstSibling();
        }
    }

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

        // Initialize with appropriate panel visibility
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

    public void ShowUsernamePanel()
    {
        usernamePanel.SetActive(true);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(false);
    }

    public void ShowLobbyListPanel()
    {
        usernamePanel.SetActive(false);
        lobbyListPanel.SetActive(true);
        matchLobbyPanel.SetActive(false);
    }

    public void ShowMatchLobbyPanel()
    {
        usernamePanel.SetActive(false);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(true);
        readyButton.interactable = true;
    }

    private void HideAllPanels()
    {
        usernamePanel.SetActive(false);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(false);
        connectingPanel.SetActive(false);
    }

    #endregion

    #region Button Handlers

    private void OnUsernameConfirmed()
    {
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username)) return;

        PlayerPrefs.SetString(USERNAME_PREF, username);
        PlayerPrefs.Save();
        
        // Change this line to go back to the main menu instead of showing the lobby
        HideAllPanels();
        
        // Find MenuManager and show the main menu
        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null)
        {
            menuManager.ShowMainMenu();
        }
        else
        {
            Debug.LogError("MenuManager not found!");
        }
    }

    private void OnCreateRoom()
    {
        PhotonManager.Instance.CreateRoom(usernameInput.text);
    }

    private void OnRefreshClicked()
    {
        // Simply request a room list update from the PhotonManager
        // The actual UI update will happen in UpdateRoomList when Photon sends the callback
        if (PhotonManager.Instance != null)
        {
            // Clear the UI first to give immediate feedback that refresh was clicked
            foreach (Transform child in lobbyListContent)
            {
                Destroy(child.gameObject);
            }
            
            // Request a fresh room list - this will trigger OnRoomListUpdate in PhotonManager
            PhotonManager.Instance.RefreshRoomList();
        }
    }

    private void OnBackClicked()
    {
        // Instead of showing username panel, go back to main menu
        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null)
        {
            menuManager.ShowMainMenu();
        }
        else
        {
            Debug.LogError("MenuManager not found!");
        }
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
        
        // Get the host's name from master client
        if (Photon.Pun.PhotonNetwork.CurrentRoom != null && 
            Photon.Pun.PhotonNetwork.CurrentRoom.Players.ContainsKey(1)) // Master client is always ID 1
        {
            string hostNickname = Photon.Pun.PhotonNetwork.CurrentRoom.Players[1].NickName;
            
            if (isMasterClient)
            {
                statusText.text = "Waiting for opponent...";
                UpdateHostInfo(hostNickname, 0, 0);
                clientNameText.text = "Waiting for player...";
                clientStatsText.text = "";
            }
            else
            {
                hostNameText.text = hostNickname; // Show actual host name
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = usernameInput.text;
                clientStatsText.text = "Wins: 0 Losses: 0";
                statusText.text = "Waiting for players to ready up...";
            }
        }
        else
        {
            // Fallback if we can't get the host info for some reason
            if (isMasterClient)
            {
                statusText.text = "Waiting for opponent...";
                UpdateHostInfo(usernameInput.text, 0, 0);
                clientNameText.text = "Waiting for player...";
                clientStatsText.text = "";
            }
            else
            {
                hostNameText.text = "Host"; // Fallback
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = usernameInput.text;
                clientStatsText.text = "Wins: 0 Losses: 0";
                statusText.text = "Waiting for players to ready up...";
            }
        }
    }

    public void UpdateRoomList(List<RoomInfo> roomList)
    {
        Debug.Log($"Updating room list with {roomList.Count} rooms");
        
        // Update cached room list first
        foreach (RoomInfo info in roomList)
        {
            // Remove room from cached list if it's no longer available
            if (info.RemovedFromList)
            {
                cachedRoomList.Remove(info.Name);
                Debug.Log($"Room removed: {info.Name}");
            }
            // Add or update room in cached list
            else
            {
                cachedRoomList[info.Name] = info;
                Debug.Log($"Room added/updated: {info.Name} - Players: {info.PlayerCount}/{info.MaxPlayers}");
            }
        }

        // Clear the UI
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        // Populate UI with current cached rooms
        Debug.Log($"Displaying {cachedRoomList.Count} rooms in UI");
        foreach (RoomInfo room in cachedRoomList.Values)
        {
            if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
            {
                GameObject entryObj = Instantiate(lobbyEntryPrefab, lobbyListContent);
                var entryUI = entryObj.GetComponent<LobbyEntryUI>();
                if (entryUI != null)
                {
                    // Try to get host name from custom properties
                    string hostName = "Unknown";
                    if (room.CustomProperties.TryGetValue("HostName", out object hostNameObj))
                    {
                        hostName = hostNameObj.ToString();
                    }
                    
                    entryUI.Initialize(room.Name, hostName, () => OnJoinRoomClicked(room.Name));
                }
            }
        }
        
        // Force the layout to update
        if (lobbyListContent.TryGetComponent<VerticalLayoutGroup>(out var layout))
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyListContent as RectTransform);
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