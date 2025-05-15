using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Photon.Pun;

public class BattleRoundManager : MonoBehaviourPunCallbacks, IPunObservable
{
    public static BattleRoundManager Instance { get; private set; }

    [SerializeField] private PlayerHP playerAHP;
    [SerializeField] private PlayerHP playerBHP;
    [SerializeField] private PlacementManager placementManager;

    private int currentRound = 1;
    private bool isRoundActive = false;

    public event Action<int> OnRoundStart;
    public event Action<string, int> OnRoundEnd;
    public event Action<string> OnMatchEnd;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (playerAHP == null)
        {
            GameObject playerAObj = GameObject.Find("PlayerAHP");
            if (playerAObj != null)
                playerAHP = playerAObj.GetComponent<PlayerHP>();
        }
        if (playerBHP == null)
        {
            GameObject playerBObj = GameObject.Find("PlayerBHP");
            if (playerBObj != null)
                playerBHP = playerBObj.GetComponent<PlayerHP>();
        }
        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }

        Debug.Log($"BattleRoundManager initialized: PlayerAHP: {playerAHP != null}, PlayerBHP: {playerBHP != null}");
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        Debug.Log($"BattleRoundManager: Game state changed to {newState}");
        if (newState == GameState.BattleActive) isRoundActive = true;
        else if (newState == GameState.PlayerAPlacement) isRoundActive = false;
        else if (newState == GameState.BattleEnd)
        {
            // This ensures we trigger match end for both players
            if (!PhotonNetwork.IsMasterClient) return;
            
            string winner = playerAHP.GetCurrentHP() > playerBHP.GetCurrentHP() ? "player" : "enemy";
            photonView.RPC("RPCTriggerMatchEnd", RpcTarget.All, winner);
        }
    }

    private void HandleGameOver(string winner)
    {
        // This is from GameManager's final result
        if (!PhotonNetwork.IsMasterClient) return;
        
        int survivingUnits = CountSurvivingUnits(winner);
        Debug.Log($"Final game over. Winner: {winner}, Surviving units: {survivingUnits}");
        
        // Directly trigger match end for everyone
        photonView.RPC("RPCTriggerMatchEnd", RpcTarget.All, winner);
    }

    [PunRPC]
    private void RPCTriggerMatchEnd(string winner)
    {
        // Convert "player"/"enemy" to local victory/defeat message
        string localResultText;
        
        // "player" means host/MasterClient won, "enemy" means client won
        if (PhotonNetwork.IsMasterClient)
        {
            // I'm the host
            localResultText = winner == "player" ? "Victory!" : "Defeat!";
        }
        else
        {
            // I'm the client
            localResultText = winner == "enemy" ? "Victory!" : "Defeat!";
        }
        
        Debug.Log($"Match end triggered. Local result: {localResultText}");
        OnMatchEnd?.Invoke(localResultText);
    }

    public void StartNewRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log($"Starting round {currentRound}");
        photonView.RPC("RPCStartNewRound", RpcTarget.All);
    }

    [PunRPC]
    private void RPCStartNewRound()
    {
        ForceHPUpdate();
        OnRoundStart?.Invoke(currentRound);
        Debug.Log($"Round {currentRound} started");
    }

    private void ForceHPUpdate()
    {
        if (playerAHP != null)
        {
            playerAHP.TriggerHPChanged();
            Debug.Log($"Player A HP: {playerAHP.GetCurrentHP()}");
        }
        if (playerBHP != null)
        {
            playerBHP.TriggerHPChanged();
            Debug.Log($"Player B HP: {playerBHP.GetCurrentHP()}");
        }
    }

    // Handle round end (called by GameManager)
    private void HandleRoundEnd(string winner)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int survivingUnits = CountSurvivingUnits(winner);
        Debug.Log($"Round ended. Winner: {winner}, Surviving units: {survivingUnits}");
        
        // Check if this round will end the match
        bool isMatchEnd = false;
        if (winner == "player" && playerBHP != null && (playerBHP.GetCurrentHP() - survivingUnits) <= 0)
        {
            isMatchEnd = true;
        }
        else if (winner == "enemy" && playerAHP != null && (playerAHP.GetCurrentHP() - survivingUnits) <= 0)
        {
            isMatchEnd = true;
        }
        
        // Send both the round end and potentially match end information
        photonView.RPC("RPCHandleRoundEnd", RpcTarget.All, winner, survivingUnits, isMatchEnd);
    }

    [PunRPC]
    private void RPCHandleRoundEnd(string winner, int survivingUnits, bool isMatchEnd)
    {
        // Convert "player"/"enemy" to local victory/defeat message
        string localResultText;
        string localTeam;
        string winningTeam;
        bool isLocalPlayerWinner;

        // "player" means host/MasterClient won, "enemy" means client won
        if (PhotonNetwork.IsMasterClient)
        {
            // I'm the host
            isLocalPlayerWinner = winner == "player";
            localTeam = "TeamA";
            winningTeam = winner == "player" ? "TeamA" : "TeamB";
        }
        else
        {
            // I'm the client
            isLocalPlayerWinner = winner == "enemy";
            localTeam = "TeamB";
            winningTeam = winner == "enemy" ? "TeamB" : "TeamA";
        }

        localResultText = isLocalPlayerWinner ? "Victory!" : "Defeat!";
        
        Debug.Log($"Battle result: {winner} won. Local player ({(PhotonNetwork.IsMasterClient ? "Host" : "Client")}): {localResultText}");

        // Apply damage and win streaks
        if (winner == "player")
        {
            playerAHP.IncrementWinStreak();
            playerBHP.ResetWinStreak();
            playerBHP.TakeDamage(survivingUnits);
            
            if (isMatchEnd || playerBHP.IsDead())
            {
                // This ensures both host and client know this is a match end
                OnMatchEnd?.Invoke(localResultText);
                return;
            }
        }
        else
        {
            playerBHP.IncrementWinStreak();
            playerAHP.ResetWinStreak();
            playerAHP.TakeDamage(survivingUnits);
            
            if (isMatchEnd || playerAHP.IsDead())
            {
                // This ensures both host and client know this is a match end
                OnMatchEnd?.Invoke(localResultText);
                return;
            }
        }

        ForceHPUpdate();
        OnRoundEnd?.Invoke(localResultText, survivingUnits);
        currentRound++;
        PrepareNextRound();
    }
    
    private int CountSurvivingUnits(string winner)
    {
        if (GameManager.Instance == null) return 0;

        List<BaseUnit> unitsToCount;
        if (winner == "player")
        {
            unitsToCount = GameManager.Instance.GetPlayerUnits();
            Debug.Log("Counting player surviving units after victory");
        }
        else
        {
            unitsToCount = GameManager.Instance.GetEnemyUnits();
            Debug.Log("Counting enemy surviving units after victory");
        }

        int count = unitsToCount.Count(u => 
            u != null && 
            u.GetCurrentState() != UnitState.Dead &&
            u.gameObject.activeInHierarchy
        );
        
        Debug.Log($"Surviving units count for {winner}: {count}");
        return count;
    }

    public void PrepareNextRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log("Preparing next round");

        if (placementManager != null)
        {
            placementManager.ClearTeamUnits("TeamA");
            placementManager.ClearTeamUnits("TeamB");
        }

        photonView.RPC("RPCPrepareNextRound", RpcTarget.All);
    }

    [PunRPC]
    private void RPCPrepareNextRound()
    {
        Debug.Log("Executing round preparation");
        isRoundActive = false;

        var orphanedHealthSystems = FindObjectsOfType<HealthSystem>()
            .Where(hs => hs.transform.parent == null)
            .ToArray();

        foreach (var healthSystem in orphanedHealthSystems)
        {
            Debug.Log($"Cleaning up orphaned health bar: {healthSystem.gameObject.name}");
            Destroy(healthSystem.gameObject);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateGameState(GameState.PlayerAPlacement);
        }
    }

    public int GetCurrentRound() => currentRound;
    public float GetPlayerAHP() => playerAHP != null ? playerAHP.GetCurrentHP() : 0f;
    public float GetPlayerBHP() => playerBHP != null ? playerBHP.GetCurrentHP() : 0f;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentRound);
            stream.SendNext(isRoundActive);
        }
        else
        {
            this.currentRound = (int)stream.ReceiveNext();
            this.isRoundActive = (bool)stream.ReceiveNext();
        }
    }
}
