using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;

public class LobbyUI : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    [SerializeField] private GameObject walletPanel;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject matchLobbyPanel;
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private GameObject usernamePanel;

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
        if (usernamePanel == null && SoarManager.Instance != null)
        {
            usernamePanel = GameObject.Find("UsernamePanel");
        }
        
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
        
        if (WalletManager.Instance != null)
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

    private bool ValidateWalletConnection()
    {
        bool isConnected = WalletManager.Instance != null && WalletManager.Instance.IsConnected;
        
        if (!isConnected)
        {
            ShowWalletPanel();
            return false;
        }
        
        return true;
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

    private void ShowConnectingPanel(bool show)
    {
        if (connectingPanel != null)
        {
            connectingPanel.SetActive(show);
        }
    }

    public void ShowWalletPanel()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        walletPanel.SetActive(true);
        lobbyListPanel.SetActive(false);
        matchLobbyPanel.SetActive(false);
    }

    public void ShowLobbyListPanel()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        if (!ValidateWalletConnection())
        {
            return;
        }
        
        walletPanel.SetActive(false);
        lobbyListPanel.SetActive(true);
        matchLobbyPanel.SetActive(false);
    }

    public void ShowMatchLobbyPanel()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
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
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
        }
    }

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
        if (!ValidateWalletConnection())
        {
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

    private async Task<string> GetDisplayNameForWallet(string walletAddress)
    {
        if (WalletManager.Instance != null)
        {
            string username = await WalletManager.Instance.GetUsernameForWallet(walletAddress);
            if (!string.IsNullOrEmpty(username))
            {
                return username;
            }
        }
        
        return FormatWalletAddress(walletAddress);
    }

    private void HandleWalletConnected(string publicKey)
    {
        connectionStatusText.text = "Connected!";
        walletAddressText.text = WalletManager.Instance.GetFormattedWalletAddress();
        connectWalletButton.interactable = true;
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        HideAllPanels();
        
        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null)
        {
            menuManager.ShowMainMenu();
        }
        else
        {
            ShowLobbyListPanel();
        }
    }
    
    private void HandleWalletDisconnected()
    {
        connectionStatusText.text = "Disconnected";
        walletAddressText.text = "Not Connected";
        connectWalletButton.interactable = true;
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        ShowWalletPanel();
    }
    
    private void HandleConnectionError(string errorMessage)
    {
        connectionStatusText.text = $"Error: {errorMessage}";
        connectWalletButton.interactable = true;
    }

    public void OnPhotonConnected()
    {
        ShowConnectingPanel(false);
    }

    public async void OnRoomJoined(bool isMasterClient)
    {
        isInRoom = true;
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        ShowMatchLobbyPanel();
        
        leaveLobbyButton.gameObject.SetActive(true);
        readyButton.interactable = true;
        
        string localDisplayName = WalletManager.Instance.GetDisplayName();
        
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players.ContainsKey(1))
        {
            string hostWalletAddress = PhotonNetwork.CurrentRoom.Players[1].NickName;
            
            if (isMasterClient)
            {
                statusText.text = "Waiting for opponent...";
                hostNameText.text = localDisplayName;
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = "Waiting for player...";
                clientStatsText.text = "";
            }
            else
            {
                string hostDisplayName = await GetDisplayNameForWallet(hostWalletAddress);
                hostNameText.text = hostDisplayName;
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = localDisplayName;
                clientStatsText.text = "Wins: 0 Losses: 0";
                statusText.text = "Waiting for players to ready up...";
            }
        }
        else
        {
            if (isMasterClient)
            {
                statusText.text = "Waiting for opponent...";
                hostNameText.text = localDisplayName;
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = "Waiting for player...";
                clientStatsText.text = "";
            }
            else
            {
                hostNameText.text = "Host";
                hostStatsText.text = "Wins: 0 Losses: 0";
                clientNameText.text = localDisplayName;
                clientStatsText.text = "Wins: 0 Losses: 0";
                statusText.text = "Waiting for players to ready up...";
            }
        }
    }

    public async void UpdateRoomList(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                cachedRoomList.Remove(info.Name);
            }
            else
            {
                cachedRoomList[info.Name] = info;
            }
        }

        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        string currentModeString = GameModeManager.Instance.CurrentMode.ToString();
        
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
                            
                            string hostDisplayName = await GetDisplayNameForWallet(hostWalletAddress);
                            string displayName = $"[{gameMode}] {hostDisplayName}";
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
        if (!ValidateWalletConnection())
        {
            return;
        }
        
        PhotonManager.Instance.JoinRoom(roomName, WalletManager.Instance.WalletPublicKey);
    }

    public async void OnPlayerJoinedRoom(Player newPlayer)
    {
        if (matchLobbyPanel.activeSelf)
        {
            string playerDisplayName = await GetDisplayNameForWallet(newPlayer.NickName);
            clientNameText.text = playerDisplayName;
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
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        ShowLobbyListPanel();
    }

    public void OnDisconnected()
    {
        isInRoom = false;
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
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