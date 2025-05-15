using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance;
    
    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private float reconnectDelay = 2f;
    [SerializeField] private LobbyUI lobbyUI;
    
    private bool isConnecting = false;
    private bool isInLobby = false;
    private Coroutine connectionCoroutine;
    private bool attemptingJoinLobby = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Configure Photon settings
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.SerializationRate = 10;
            PhotonNetwork.SendRate = 20;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (lobbyUI == null)
        {
            lobbyUI = FindFirstObjectByType<LobbyUI>();
        }
        
        // Connect automatically with a delay for safety
        StartCoroutine(DelayedConnect(0.5f));
    }
    
    private IEnumerator DelayedConnect(float delay)
    {
        yield return new WaitForSeconds(delay);
        ConnectToPhoton();
    }
    
    public void EnsureConnected()
    {
        if (!PhotonNetwork.IsConnected)
        {
            if (!isConnecting)
            {
                ConnectToPhoton();
            }
        }
        else if (!PhotonNetwork.InLobby && !attemptingJoinLobby)
        {
            attemptingJoinLobby = true;
            StartCoroutine(SafeJoinLobby());
        }
    }
    
    public void ConnectToPhoton()
    {
        if (isConnecting) return;
        
        Debug.Log("[PhotonManager] Attempting to connect to Photon...");
        isConnecting = true;
        
        // Cancel any existing connection attempts
        if (connectionCoroutine != null)
        {
            StopCoroutine(connectionCoroutine);
        }
        
        // Start new connection attempt
        connectionCoroutine = StartCoroutine(ConnectCoroutine());
    }
    
    private IEnumerator ConnectCoroutine()
    {
        // Ensure we're disconnected first
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            float disconnectWait = 0f;
            while (PhotonNetwork.IsConnected && disconnectWait < 5f)
            {
                disconnectWait += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Set app version
        PhotonNetwork.GameVersion = gameVersion;
        
        // Try to connect
        bool success = PhotonNetwork.ConnectUsingSettings();
        
        if (!success)
        {
            Debug.LogError("[PhotonManager] Failed to connect to Photon.");
            isConnecting = false;
            
            // Retry after delay
            yield return new WaitForSeconds(reconnectDelay);
            ConnectToPhoton();
        }
        
        // Wait for connection to complete
        yield return new WaitUntil(() => PhotonNetwork.IsConnected || !isConnecting);
    }
    
    private IEnumerator SafeJoinLobby()
    {
        Debug.Log("[PhotonManager] Safely joining lobby...");
        
        // Wait to ensure we're fully connected
        yield return new WaitUntil(() => PhotonNetwork.IsConnected);
        
        // Wait an additional short delay for stability
        yield return new WaitForSeconds(0.5f);
        
        // Only join if we're not already in lobby
        if (!PhotonNetwork.InLobby)
        {
            Debug.Log("[PhotonManager] Calling JoinLobby()");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            Debug.Log("[PhotonManager] Already in lobby");
            isInLobby = true;
            attemptingJoinLobby = false;
            RefreshRoomList();
        }
    }
    
    public void CreateRoom(string playerName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("[PhotonManager] Cannot create room: Not connected to Photon");
            ConnectToPhoton();
            return;
        }
        
        // Get current game mode
        string currentModeString = "Practice";
        if (GameModeManager.Instance != null)
        {
            currentModeString = GameModeManager.Instance.CurrentMode.ToString();
        }
        
        // Set up room options
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };
        
        // Add custom properties
        ExitGames.Client.Photon.Hashtable customProps = new ExitGames.Client.Photon.Hashtable
        {
            { "GameMode", currentModeString },
            { "HostName", playerName }
        };
        options.CustomRoomProperties = customProps;
        options.CustomRoomPropertiesForLobby = new string[] { "GameMode", "HostName" };
        
        // Create a unique room name
        string roomName = $"Room_{playerName}_{System.DateTime.UtcNow.Ticks}";
        
        // Create the room
        Debug.Log($"[PhotonManager] Creating room: {roomName}, Mode: {currentModeString}");
        PhotonNetwork.CreateRoom(roomName, options);
    }
    
    public void JoinRoom(string roomName, string playerName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("[PhotonManager] Cannot join room: Not connected to Photon");
            return;
        }
        
        // Set nickname and join
        PhotonNetwork.NickName = playerName;
        PhotonNetwork.JoinRoom(roomName);
    }
    
    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }
    
    public void RefreshRoomList()
    {
        Debug.Log("[PhotonManager] RefreshRoomList called");
        
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[PhotonManager] Cannot refresh: Not connected");
            ConnectToPhoton();
            return;
        }
        
        if (!PhotonNetwork.InLobby)
        {
            Debug.Log("[PhotonManager] Cannot refresh: Not in lobby");
            StartCoroutine(SafeJoinLobby());
            return;
        }
        
        // Force room list update by leaving and rejoining
        StartCoroutine(ForceRoomListRefresh());
    }
    
    private IEnumerator ForceRoomListRefresh()
    {
        Debug.Log("[PhotonManager] Forcing room list refresh");
        
        // Set flag to avoid multiple refreshes
        isInLobby = false;
        
        // Leave and rejoin the lobby
        PhotonNetwork.LeaveLobby();
        
        // Wait a bit
        yield return new WaitForSeconds(0.3f);
        
        // Ensure we're connected
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[PhotonManager] Rejoining lobby");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            Debug.LogWarning("[PhotonManager] Connection lost during refresh");
            ConnectToPhoton();
        }
    }
    
    public void SetPlayerReady(bool isReady)
    {
        if (!PhotonNetwork.InRoom) return;
        
        var props = new ExitGames.Client.Photon.Hashtable { { "IsReady", isReady } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        
        // If both players are ready and we're the host, start the game
        if (isReady && PhotonNetwork.IsMasterClient)
        {
            bool allReady = true;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player.CustomProperties.TryGetValue("IsReady", out object readyObj))
                {
                    bool playerReady = (bool)readyObj;
                    if (!playerReady)
                    {
                        allReady = false;
                        break;
                    }
                }
                else
                {
                    allReady = false;
                    break;
                }
            }
            
            if (allReady && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
            {
                StartGame();
            }
        }
    }
    
    private void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.LoadLevel("BattleScene");
        }
    }
    
    #region Photon Callbacks
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("[PhotonManager] Connected to Photon master server");
        isConnecting = false;
        
        // Join the lobby
        if (!PhotonNetwork.InLobby && !attemptingJoinLobby)
        {
            attemptingJoinLobby = true;
            StartCoroutine(SafeJoinLobby());
        }
        
        // Notify UI
        if (lobbyUI != null)
        {
            lobbyUI.OnPhotonConnected();
        }
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("[PhotonManager] Joined Photon lobby successfully");
        isInLobby = true;
        attemptingJoinLobby = false;
        
        // Refresh room list
        if (lobbyUI != null)
        {
            RefreshRoomList();
        }
    }
    
    public override void OnLeftLobby()
    {
        Debug.Log("[PhotonManager] Left Photon lobby");
        isInLobby = false;
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"[PhotonManager] Disconnected from Photon: {cause}");
        isConnecting = false;
        isInLobby = false;
        attemptingJoinLobby = false;
        
        // Notify UI
        if (lobbyUI != null)
        {
            lobbyUI.OnDisconnected();
        }
        
        // Reconnect if it wasn't intentional
        if (cause != DisconnectCause.DisconnectByClientLogic &&
            cause != DisconnectCause.ApplicationQuit)
        {
            StartCoroutine(DelayedReconnect());
        }
    }
    
    private IEnumerator DelayedReconnect()
    {
        yield return new WaitForSeconds(reconnectDelay);
        ConnectToPhoton();
    }
    
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"[PhotonManager] Room list updated with {roomList.Count} rooms");
        
        // Update UI
        if (lobbyUI != null)
        {
            lobbyUI.UpdateRoomList(roomList);
        }
    }
    
    public override void OnCreatedRoom()
    {
        Debug.Log("[PhotonManager] Room created successfully");
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonManager] Room creation failed: {message} ({returnCode})");
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log("[PhotonManager] Joined room successfully");
        
        // Update UI
        if (lobbyUI != null)
        {
            lobbyUI.OnRoomJoined(PhotonNetwork.IsMasterClient);
        }
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonManager] Failed to join room: {message} ({returnCode})");
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[PhotonManager] Player joined: {newPlayer.NickName}");
        
        // Update UI
        if (lobbyUI != null)
        {
            lobbyUI.OnPlayerJoinedRoom(newPlayer);
        }
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[PhotonManager] Player left: {otherPlayer.NickName}");
        
        // Update UI
        if (lobbyUI != null)
        {
            lobbyUI.OnPlayerLeftRoom(otherPlayer);
        }
    }
    
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            bool isReady = (bool)changedProps["IsReady"];
            
            // Update UI
            if (lobbyUI != null)
            {
                lobbyUI.UpdatePlayerReadyState(targetPlayer, isReady);
            }
            
            // Check if all players are ready
            if (PhotonNetwork.IsMasterClient)
            {
                bool allReady = true;
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    if (player.CustomProperties.TryGetValue("IsReady", out object readyObj))
                    {
                        bool playerReady = (bool)readyObj;
                        if (!playerReady)
                        {
                            allReady = false;
                            break;
                        }
                    }
                    else
                    {
                        allReady = false;
                        break;
                    }
                }
                
                if (allReady && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                {
                    StartGame();
                }
            }
        }
    }
    
    public override void OnLeftRoom()
    {
        Debug.Log("[PhotonManager] Left room");
        
        // Update UI
        if (lobbyUI != null)
        {
            lobbyUI.OnRoomLeft();
        }
    }
    
    #endregion
}