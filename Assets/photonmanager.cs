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
        // Set up Photon settings
        PhotonNetwork.AutomaticallySyncScene = true;
        ConnectToPhoton();
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

    public void CreateRoom(string playerName)
    {
        if (!PhotonNetwork.IsConnected) return;

        PhotonNetwork.LocalPlayer.NickName = playerName;
        
        // Create room options
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            PublishUserId = true
        };

        PhotonNetwork.CreateRoom(null, roomOptions); // null for random room name
    }

    public void JoinRoom(string roomName, string playerName)
    {
        if (!PhotonNetwork.IsConnected) return;

        PhotonNetwork.LocalPlayer.NickName = playerName;
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

    public void RefreshRoomList()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InLobby)
        {
            Debug.Log("Refreshing room list");
            // When in a lobby, OnRoomListUpdate is automatically called periodically
            // We don't need to explicitly request updates
        }
        else if (PhotonNetwork.IsConnected && !PhotonNetwork.InLobby)
        {
            Debug.Log("Joining lobby to get room list");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            Debug.LogWarning("Cannot refresh room list: Not connected to Photon");
        }
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

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        isConnecting = false;
        PhotonNetwork.JoinLobby();
        lobbyUI?.OnPhotonConnected();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Photon lobby");
        lobbyUI?.OnPhotonConnected();
        
        // This will automatically trigger OnRoomListUpdate with the current list of rooms
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
        // Handle master client switching if needed
    }

    #endregion
}