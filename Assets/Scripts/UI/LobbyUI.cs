using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;
using System.Collections.Generic;
using Photon.Pun;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject walletPanel;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject matchLobbyPanel;
    [SerializeField] private GameObject connectingPanel;

    [Header("Wallet Panel")]
    [SerializeField] private Button connectWalletButton;
    [SerializeField] private TextMeshProUGUI walletAddressText;
    [SerializeField] private TextMeshProUGUI connectionStatusText;

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

    private bool isInRoom = false;
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    private void Awake()
    {
        if (!WalletManager.Instance.IsConnected)
        {
            walletPanel.SetActive(true);
            lobbyListPanel.SetActive(false);
            matchLobbyPanel.SetActive(false);
        }
        else
        {
            walletPanel.SetActive(false);
        }
        
        if (connectingPanel != null)
        {
            connectingPanel.transform.SetAsFirstSibling();
        }
    }

    private void Start()
    {
        SetupUI();
        UpdateWalletUI();
        ShowConnectingPanel(true);
        
        if (WalletManager.Instance == null)
        {
            Debug.LogError("WalletManager not found in scene!");
        }
        else
        {
            WalletManager.Instance.OnWalletConnected += HandleWalletConnected;
            WalletManager.Instance.OnWalletDisconnected += HandleWalletDisconnected;
            WalletManager.Instance.OnConnectionError += HandleConnectionError;
        }
    }
    
    private void Update()
    {
        if (connectionStatusText != null)
        {
            string status = "Unknown";
            
            if (!PhotonNetwork.IsConnected)
                status = "Disconnected";
            else if (!PhotonNetwork.InLobby)
                status = "Connected, Not In Lobby";
            else
                status = $"Connected: {PhotonNetwork.CloudRegion}";
                
            connectionStatusText.text = $"Status: {status}";
        }
    }
    
    private void OnDestroy()
    {
        if (WalletManager.Instance != null)
        {
            WalletManager.Instance.OnWalletConnected -= HandleWalletConnected;
            WalletManager.Instance.OnWalletDisconnected -= HandleWalletDisconnected;
            WalletManager.Instance.OnConnectionError -= HandleConnectionError;
        }
    }

    private void SetupUI()
    {
        connectWalletButton.onClick.AddListener(OnConnectWalletClicked);
        createLobbyButton.onClick.AddListener(OnCreateRoom);
        refreshButton.onClick.AddListener(OnRefreshClicked);
        backButton.onClick.AddListener(OnBackClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveRoom);

        ShowWalletPanel();
    }

    private void UpdateWalletUI()
    {
        if (WalletManager.Instance.IsConnected)
        {
            walletAddressText.text = WalletManager.Instance.GetFormattedWalletAddress();
            connectionStatusText.text = "Connected";
        }
        else
        {
            walletAddressText.text = "Not Connected";
            connectionStatusText.text = "Please connect your wallet";
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

    public void ShowWalletPanel()
    {
        walletPanel.SetActive(true);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(false);
    }

    public void ShowLobbyListPanel()
    {
        walletPanel.SetActive(false);
        lobbyListPanel.SetActive(true);
        matchLobbyPanel.SetActive(false);
    }

    public void ShowMatchLobbyPanel()
    {
        walletPanel.SetActive(false);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(true);
        readyButton.interactable = true;
    }

    private void HideAllPanels()
    {
        walletPanel.SetActive(false);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(false);
        connectingPanel.SetActive(false);
    }

    #endregion

    #region Button Handlers

    private async void OnConnectWalletClicked()
    {
        connectionStatusText.text = "Connecting...";
        connectWalletButton.interactable = false;
        
        bool success = await WalletManager.Instance.ConnectWallet();
        
        if (!success)
        {
            connectionStatusText.text = "Connection failed. Try again.";
            connectWalletButton.interactable = true;
        }
    }

    private void OnCreateRoom()
    {
        if (!WalletManager.Instance.IsConnected)
        {
            Debug.LogError("Cannot create room: Wallet not connected");
            return;
        }
        
        PhotonManager.Instance.CreateRoom(WalletManager.Instance.WalletPublicKey);
    }

    private void OnRefreshClicked()
    {
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }
        
        PhotonManager.Instance.RefreshRoomList();
    }

    private void OnBackClicked()
    {
        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null)
        {
            menuManager.ShowModeSelection();
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

    #region Wallet Event Handlers

    private void HandleWalletConnected(string publicKey)
    {
        connectionStatusText.text = "Connected!";
        walletAddressText.text = WalletManager.Instance.GetFormattedWalletAddress();
        connectWalletButton.interactable = true;
        
        HideAllPanels();
        
        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null)
        {
            menuManager.ShowMainMenu();
        }
        else
        {
            Debug.LogError("MenuManager not found!");
            ShowLobbyListPanel();
        }
    }
    
    private void HandleWalletDisconnected()
    {
        connectionStatusText.text = "Disconnected";
        walletAddressText.text = "Not Connected";
        connectWalletButton.interactable = true;
        ShowWalletPanel();
    }
    
    private void HandleConnectionError(string errorMessage)
    {
        connectionStatusText.text = $"Error: {errorMessage}";
        connectWalletButton.interactable = true;
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
        
        if (Photon.Pun.PhotonNetwork.CurrentRoom != null && 
            Photon.Pun.PhotonNetwork.CurrentRoom.Players.ContainsKey(1))
        {
            string hostWalletAddress = Photon.Pun.PhotonNetwork.CurrentRoom.Players[1].NickName;
            
            if (isMasterClient)
            {
                statusText.text = "Waiting for opponent...";
                UpdateHostInfo(FormatWalletAddress(hostWalletAddress), 0, 0);
                clientNameText.text = "Waiting for player...";
                clientStatsText.text = "";
            }
            else
            {
                hostNameText.text = FormatWalletAddress(hostWalletAddress);
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = FormatWalletAddress(WalletManager.Instance.WalletPublicKey);
                clientStatsText.text = "Wins: 0 Losses: 0";
                statusText.text = "Waiting for players to ready up...";
            }
        }
        else
        {
            if (isMasterClient)
            {
                statusText.text = "Waiting for opponent...";
                UpdateHostInfo(FormatWalletAddress(WalletManager.Instance.WalletPublicKey), 0, 0);
                clientNameText.text = "Waiting for player...";
                clientStatsText.text = "";
            }
            else
            {
                hostNameText.text = "Host";
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = FormatWalletAddress(WalletManager.Instance.WalletPublicKey);
                clientStatsText.text = "Wins: 0 Losses: 0";
                statusText.text = "Waiting for players to ready up...";
            }
        }
    }

    public void UpdateRoomList(List<RoomInfo> roomList)
    {
        Debug.Log($"Updating room list with {roomList.Count} rooms");
        
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                cachedRoomList.Remove(info.Name);
                Debug.Log($"Room removed: {info.Name}");
            }
            else
            {
                cachedRoomList[info.Name] = info;
                Debug.Log($"Room added/updated: {info.Name} - Players: {info.PlayerCount}/{info.MaxPlayers}");
            }
        }

        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        string currentModeString = GameModeManager.Instance.CurrentMode.ToString();
        
        Debug.Log($"Displaying rooms for mode: {currentModeString}");
        foreach (RoomInfo room in cachedRoomList.Values)
        {
            if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
            {
                if (room.CustomProperties.TryGetValue("GameMode", out object gameMode))
                {
                    if (gameMode.ToString() == currentModeString)
                    {
                        GameObject entryObj = Instantiate(lobbyEntryPrefab, lobbyListContent);
                        var entryUI = entryObj.GetComponent<LobbyEntryUI>();
                        if (entryUI != null)
                        {
                            string hostWalletAddress = "Unknown";
                            if (room.CustomProperties.TryGetValue("HostName", out object hostNameObj))
                            {
                                hostWalletAddress = hostNameObj.ToString();
                            }
                            
                            string formattedHostAddress = FormatWalletAddress(hostWalletAddress);
                            string displayName = $"[{gameMode}] {formattedHostAddress}";
                            entryUI.Initialize(room.Name, displayName, () => OnJoinRoomClicked(room.Name));
                        }
                    }
                }
            }
        }
        
        if (lobbyListContent.TryGetComponent<VerticalLayoutGroup>(out var layout))
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyListContent as RectTransform);
        }
    }

    public bool IsLobbyListActive()
    {
        return lobbyListPanel != null && lobbyListPanel.activeSelf;
    }
    
    public void ForceRefreshRoomList()
    {
        if (lobbyListContent != null)
        {
            foreach (Transform child in lobbyListContent)
            {
                Destroy(child.gameObject);
            }
        }
        
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.RefreshRoomList();
        }
    }

    private void OnJoinRoomClicked(string roomName)
    {
        if (!WalletManager.Instance.IsConnected)
        {
            Debug.LogError("Cannot join room: Wallet not connected");
            return;
        }
        
        PhotonManager.Instance.JoinRoom(roomName, WalletManager.Instance.WalletPublicKey);
    }

    public void OnPlayerJoinedRoom(Player newPlayer)
    {
        if (matchLobbyPanel.activeSelf)
        {
            clientNameText.text = FormatWalletAddress(newPlayer.NickName);
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
        ShowWalletPanel();
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

    private void UpdateHostInfo(string walletAddress, int wins, int losses)
    {
        hostNameText.text = walletAddress;
        hostStatsText.text = $"Wins: {wins} Losses: {losses}";
    }
    
    private string FormatWalletAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return "Unknown";
        
        if (address.Length > 10)
        {
            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }
        return address;
    }
}