using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance { get; private set; }

    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private string battleSceneName = "BattleScene";
    [SerializeField] private LobbyUI lobbyUI;
    
    private bool isConnecting = false;
    private bool isReconnecting = false;
    private Dictionary<string, bool> playerReadyStatus = new Dictionary<string, bool>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        ConnectToPhoton();
    }
    
    private void Update()
    {
        if (Time.frameCount % 300 == 0)
        {
            Debug.Log($"Photon Diagnostics: Connected={PhotonNetwork.IsConnected}, " +
                      $"Region={PhotonNetwork.CloudRegion}, " +
                      $"Server={PhotonNetwork.Server}, " +
                      $"InLobby={PhotonNetwork.InLobby}, " +
                      $"AppVersion={PhotonNetwork.AppVersion}");
                      
            if (PhotonNetwork.InLobby)
            {
                Debug.Log($"In Lobby: {PhotonNetwork.CurrentLobby.Name}, Type: {PhotonNetwork.CurrentLobby.Type}");
            }
        }
    }

    public void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected && !isConnecting)
        {
            isConnecting = true;
            PhotonNetwork.GameVersion = gameVersion;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void EnsureConnected()
    {
        if (!PhotonNetwork.IsConnected && !isConnecting && !isReconnecting)
        {
            Debug.Log("[PhotonManager] EnsureConnected called - not connected, connecting now");
            isReconnecting = true;
            StartCoroutine(ReconnectWithDelay());
        }
        else if (PhotonNetwork.IsConnected && !PhotonNetwork.InLobby)
        {
            Debug.Log("[PhotonManager] EnsureConnected called - connected but not in lobby, joining lobby");
            TypedLobby mainLobby = new TypedLobby("MainLobby", LobbyType.Default);
            PhotonNetwork.JoinLobby(mainLobby);
        }
        else
        {
            Debug.Log("[PhotonManager] EnsureConnected called - already connected and in lobby");
        }
    }

    private IEnumerator ReconnectWithDelay()
    {
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[PhotonManager] Attempting to reconnect to Photon");
        ConnectToPhoton();
        
        yield return new WaitForSeconds(1.0f);
        isReconnecting = false;
    }

    public void CreateRoom(string walletAddress)
    {
        if (!PhotonNetwork.IsConnected) return;

        PhotonNetwork.LocalPlayer.NickName = walletAddress;
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            PublishUserId = true,
            IsVisible = true,
            IsOpen = true
        };
        
        ExitGames.Client.Photon.Hashtable customProps = new ExitGames.Client.Photon.Hashtable();
        customProps.Add("HostName", walletAddress);
        customProps.Add("GameMode", GameModeManager.Instance.CurrentMode.ToString());
        roomOptions.CustomRoomProperties = customProps;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { "HostName", "GameMode" };

        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public void JoinRoom(string roomName, string walletAddress)
    {
        if (!PhotonNetwork.IsConnected) return;

        PhotonNetwork.LocalPlayer.NickName = walletAddress;
        PhotonNetwork.JoinRoom(roomName);
    }

    public void LeaveRoom()
    {
        playerReadyStatus.Clear();
        PhotonNetwork.LeaveRoom();
    }

    public void SetPlayerReady(bool isReady)
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.CurrentRoom == null) return;

        string playerId = PhotonNetwork.LocalPlayer.UserId;
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { "IsReady", isReady }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);

        if (PhotonNetwork.CurrentRoom.PlayerCount == 2 && CheckAllPlayersReady())
        {
            StartGame();
        }
    }

    // This is the main method to refresh room lists - only called manually
    public void RefreshRoomList()
    {
        Debug.Log($"RefreshRoomList called. Connected={PhotonNetwork.IsConnected}, InLobby={PhotonNetwork.InLobby}");
        
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("Cannot refresh room list: Not connected to Photon");
            ConnectToPhoton();
            return;
        }
        
        if (PhotonNetwork.InLobby)
        {
            Debug.Log("In lobby, forcing direct room list update with LeaveLobby/JoinLobby");
            StartCoroutine(ForceLobbyRefresh());
        }
        else
        {
            Debug.Log("Not in lobby, joining lobby to get room list");
            TypedLobby mainLobby = new TypedLobby("MainLobby", LobbyType.Default);
            PhotonNetwork.JoinLobby(mainLobby);
        }
    }
    
    private IEnumerator ForceLobbyRefresh()
    {
        // Store current lobby before leaving
        TypedLobby currentLobby = PhotonNetwork.CurrentLobby;
        
        // Leave the lobby
        PhotonNetwork.LeaveLobby();
        
        // Wait a bit to ensure the leave operation completes
        yield return new WaitForSeconds(0.3f);
        
        // Join the same lobby again
        PhotonNetwork.JoinLobby(currentLobby);
        
        Debug.Log("Forced lobby refresh with leave/join sequence");
    }

    private bool CheckAllPlayersReady()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount != 2) return false;

        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            object isReadyObj;
            if (!player.CustomProperties.TryGetValue("IsReady", out isReadyObj) || !(bool)isReadyObj)
            {
                return false;
            }
        }
        return true;
    }

    private void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(battleSceneName);
        }
    }
    
    private void UpdateRoomHostName()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
        
        ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
        roomProps.Add("HostName", PhotonNetwork.LocalPlayer.NickName);
        
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
    }

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        isConnecting = false;
        
        TypedLobby mainLobby = new TypedLobby("MainLobby", LobbyType.Default);
        PhotonNetwork.JoinLobby(mainLobby);
        
        lobbyUI?.OnPhotonConnected();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log($"Joined Photon lobby: {PhotonNetwork.CurrentLobby.Name}, Type: {PhotonNetwork.CurrentLobby.Type}");
        lobbyUI?.OnPhotonConnected();
        
        // Just update the UI when joining a lobby
        if (lobbyUI != null)
        {
            lobbyUI.ForceRefreshRoomList();
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from server: {cause}");
        isConnecting = false;
        lobbyUI?.OnDisconnected();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        
        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomHostName();
        }
        
        lobbyUI?.OnRoomJoined(PhotonNetwork.IsMasterClient);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"Room list updated with {roomList.Count} rooms");
        lobbyUI?.UpdateRoomList(roomList);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player Joined: {newPlayer.NickName}");
        lobbyUI?.OnPlayerJoinedRoom(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player Left: {otherPlayer.NickName}");
        playerReadyStatus.Remove(otherPlayer.UserId);
        lobbyUI?.OnPlayerLeftRoom(otherPlayer);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
        lobbyUI?.OnRoomLeft();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            bool isReady = (bool)changedProps["IsReady"];
            playerReadyStatus[targetPlayer.UserId] = isReady;
            lobbyUI?.UpdatePlayerReadyState(targetPlayer, isReady);

            if (PhotonNetwork.CurrentRoom.PlayerCount == 2 && CheckAllPlayersReady())
            {
                StartGame();
            }
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber == newMasterClient.ActorNumber)
        {
            UpdateRoomHostName();
        }
    }

    #endregion
}