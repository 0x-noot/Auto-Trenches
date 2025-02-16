using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance { get; private set; }

    [SerializeField] private byte maxPlayersPerRoom = 2;
    private LobbyUI lobbyUI;

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
        }
    }

    private void Start()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();
        ConnectToPhoton();
    }

    private void ConnectToPhoton()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Connecting to Photon...");
        }
    }

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server!");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Photon Lobby!");
        if (lobbyUI != null)
        {
            lobbyUI.OnPhotonConnected();
        }
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"Created room: {PhotonNetwork.CurrentRoom.Name}");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        if (lobbyUI != null)
        {
            lobbyUI.OnRoomJoined(PhotonNetwork.IsMasterClient);
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"Room list updated. {roomList.Count} rooms available.");
        if (lobbyUI != null)
        {
            lobbyUI.UpdateRoomList(roomList);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player joined: {newPlayer.NickName}");
        if (lobbyUI != null)
        {
            lobbyUI.OnPlayerJoinedRoom(newPlayer);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left: {otherPlayer.NickName}");
        if (lobbyUI != null)
        {
            lobbyUI.OnPlayerLeftRoom(otherPlayer);
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
        if (lobbyUI != null)
        {
            lobbyUI.OnRoomLeft();
        }
    }

    #endregion

    #region Room Management

    public void CreateRoom(string username)
    {
        PhotonNetwork.NickName = username;
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        string roomName = "Room_" + Random.Range(0, 10000);
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoom(string roomName, string username)
    {
        PhotonNetwork.NickName = username;
        PhotonNetwork.JoinRoom(roomName);
    }

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    public void SetPlayerReady(bool isReady)
    {
        if (!PhotonNetwork.IsMessageQueueRunning) return;

        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { "IsReady", isReady }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);

        // Check if all players are ready
        if (isReady && PhotonNetwork.IsMasterClient)
        {
            CheckAllPlayersReady();
        }
    }

    private void CheckAllPlayersReady()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        bool allReady = true;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            object isPlayerReady;
            if (player.CustomProperties.TryGetValue("IsReady", out isPlayerReady))
            {
                if (!(bool)isPlayerReady)
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

        if (allReady && PhotonNetwork.PlayerList.Length == maxPlayersPerRoom)
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Lock the room
            PhotonNetwork.CurrentRoom.IsOpen = false;
            
            // Load the battle scene
            PhotonNetwork.LoadLevel("BattleScene");
        }
    }

    #endregion

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected from Photon: {cause}");
        if (lobbyUI != null)
        {
            lobbyUI.OnDisconnected();
        }
    }
}