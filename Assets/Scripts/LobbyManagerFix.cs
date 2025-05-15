using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class LobbyManagerFix : MonoBehaviourPunCallbacks
{
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private PhotonManager photonManager;
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    
    [Tooltip("Auto-refresh interval in seconds")]
    [SerializeField] private float autoRefreshInterval = 3f;
    [SerializeField] private int maxRefreshAttempts = 3;
    
    private float lastRefreshTime = 0f;
    private bool isRefreshing = false;
    private int refreshAttempts = 0;
    private bool isInitialized = false;
    private Coroutine refreshCoroutine;

    private void Start()
    {
        // Find references if not assigned
        if (lobbyUI == null)
        {
            lobbyUI = FindFirstObjectByType<LobbyUI>();
        }
        
        if (photonManager == null)
        {
            photonManager = FindFirstObjectByType<PhotonManager>();
        }
        
        isInitialized = (lobbyUI != null && photonManager != null);
        
        if (!isInitialized)
        {
            Debug.LogError("[LobbyManagerFix] Could not find required references!");
            return;
        }
        
        // Add listener to refresh button if assigned
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(OnRefreshButtonClicked);
        }
        
        // Start the auto-refresh coroutine
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
        }
        refreshCoroutine = StartCoroutine(AutoRefreshLobbyList());
    }
    
    private void OnEnable()
    {
        // When this object becomes active, check connection state
        if (isInitialized)
        {
            StartCoroutine(CheckConnectionStateDelayed());
        }
    }
    
    private IEnumerator CheckConnectionStateDelayed()
    {
        // Wait a moment to allow Photon to initialize
        yield return new WaitForSeconds(0.5f);
        
        // Check Photon connection state
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[LobbyManagerFix] Not connected to Photon - connecting now");
            if (photonManager != null)
            {
                photonManager.ConnectToPhoton();
            }
        }
        else if (!PhotonNetwork.InLobby)
        {
            Debug.Log("[LobbyManagerFix] Connected but not in lobby - joining lobby now");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            Debug.Log("[LobbyManagerFix] Already in Photon lobby");
            RefreshRoomList();
        }
        
        // Update status text if assigned
        UpdateConnectionStatus();
    }
    
    public void OnRefreshButtonClicked()
    {
        RefreshRoomList();
    }
    
    public void RefreshRoomList()
    {
        if (isRefreshing) return;
        
        isRefreshing = true;
        refreshAttempts = 0;
        lastRefreshTime = Time.time;
        
        Debug.Log("[LobbyManagerFix] Refreshing room list");
        
        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.InLobby)
            {
                Debug.Log("[LobbyManagerFix] Already in lobby, performing refresh");
                // First leave then rejoin to force refresh
                StartCoroutine(ForceRefreshLobby());
            }
            else
            {
                Debug.Log("[LobbyManagerFix] Not in lobby, joining now");
                PhotonNetwork.JoinLobby();
            }
        }
        else
        {
            Debug.Log("[LobbyManagerFix] Not connected, attempting to connect");
            if (photonManager != null)
            {
                photonManager.ConnectToPhoton();
            }
            isRefreshing = false;
        }
        
        UpdateConnectionStatus();
    }
    
    private IEnumerator ForceRefreshLobby()
    {
        // Leave the current lobby
        PhotonNetwork.LeaveLobby();
        
        // Wait a short moment
        yield return new WaitForSeconds(0.3f);
        
        // Join the lobby again
        PhotonNetwork.JoinLobby();
        
        // Allow refresh again after a delay
        yield return new WaitForSeconds(0.5f);
        isRefreshing = false;
    }
    
    private IEnumerator AutoRefreshLobbyList()
    {
        while (true)
        {
            // Wait for the auto-refresh interval
            yield return new WaitForSeconds(autoRefreshInterval);
            
            // Only auto-refresh if we're not manually refreshing and are on the lobby screen
            if (!isRefreshing && lobbyUI != null && gameObject.activeInHierarchy)
            {
                // Check if the lobby UI is active
                bool lobbyActive = lobbyUI.IsLobbyListActive();
                
                if (lobbyActive)
                {
                    Debug.Log("[LobbyManagerFix] Auto-refreshing lobby list");
                    RefreshRoomList();
                }
            }
        }
    }
    
    private void UpdateConnectionStatus()
    {
        if (connectionStatusText != null)
        {
            string status = "Unknown";
            
            if (!PhotonNetwork.IsConnected)
            {
                status = "Disconnected";
            }
            else if (!PhotonNetwork.InLobby)
            {
                status = "Connected, Joining Lobby...";
            }
            else
            {
                status = $"Connected: {PhotonNetwork.CloudRegion}";
            }
            
            connectionStatusText.text = $"Status: {status}";
        }
    }
    
    // PUN Callbacks
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("[LobbyManagerFix] Connected to master server, joining lobby");
        PhotonNetwork.JoinLobby();
        UpdateConnectionStatus();
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("[LobbyManagerFix] Joined lobby");
        isRefreshing = false;
        UpdateConnectionStatus();
    }
    
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"[LobbyManagerFix] Room list updated with {roomList.Count} rooms");
        
        // We successfully received a room list update
        isRefreshing = false;
        refreshAttempts = 0;
        
        // Update the connection status
        UpdateConnectionStatus();
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"[LobbyManagerFix] Disconnected from Photon: {cause}");
        isRefreshing = false;
        
        // Try to reconnect if this isn't an intentional disconnect
        if (cause != DisconnectCause.DisconnectByClientLogic && 
            cause != DisconnectCause.ApplicationQuit)
        {
            if (photonManager != null)
            {
                photonManager.ConnectToPhoton();
            }
        }
        
        UpdateConnectionStatus();
    }
    
    private void OnDestroy()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
        }
        
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(OnRefreshButtonClicked);
        }
    }
}