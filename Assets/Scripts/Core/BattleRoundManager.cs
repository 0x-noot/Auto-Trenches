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
    [SerializeField] private float endGameDelay = 2f;

    private int currentRound = 1;
    private bool isRoundActive = false;
    private bool isMatchEndTriggered = false;
    private float lastHpCheckTime = 0f;
    private const float HP_CHECK_INTERVAL = 0.5f;

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

        if (playerAHP != null)
        {
            playerAHP.ResetForNewMatch();
        }
        if (playerBHP != null)
        {
            playerBHP.ResetForNewMatch();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Existing time check code
        if (Time.time - lastHpCheckTime >= HP_CHECK_INTERVAL)
        {
            lastHpCheckTime = Time.time;
            CheckMatchEndHpCondition();
            
            // Also force PlayerHP objects to check their condition
            if (playerAHP != null) playerAHP.CheckDeathCondition();
            if (playerBHP != null) playerBHP.CheckDeathCondition();
        }
    }

    private void CheckMatchEndHpCondition()
    {
        if (isMatchEndTriggered) return;
        
        if (playerAHP != null && playerBHP != null)
        {
            float playerAHpValue = playerAHP.GetCurrentHP();
            float playerBHpValue = playerBHP.GetCurrentHP();
            
            if (playerAHpValue <= 0 || playerBHpValue <= 0)
            {
                // Determine the winner based on HP values
                string winner = playerAHpValue > playerBHpValue ? "player" : "enemy";
                
                // Flag that we've triggered match end
                isMatchEndTriggered = true;
                
                // End the battle through GameManager
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EndBattle(winner);
                }
                
                // Also show results directly to ensure both clients see them
                BattleResultsUI resultsUI = FindFirstObjectByType<BattleResultsUI>();
                if (resultsUI != null)
                {
                    // Show appropriate results to each player
                    if (PhotonNetwork.IsMasterClient)
                    {
                        // This is the host
                        string localResult = winner == "player" ? "Victory!" : "Defeat!";
                        resultsUI.RPCShowMatchResults(localResult);
                        
                        // Also tell the client what to show
                        resultsUI.photonView.RPC("RPCShowMatchResults", RpcTarget.Others, 
                            winner == "player" ? "Defeat!" : "Victory!"); 
                    }
                }
            }
        }
    }

    [PunRPC]
    public void RPCForceMatchEnd()
    {
        if (isMatchEndTriggered) return;
        
        CheckMatchEndHpCondition();
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
        if (newState == GameState.BattleActive) isRoundActive = true;
        else if (newState == GameState.PlayerAPlacement) isRoundActive = false;
        else if (newState == GameState.BattleEnd)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            string winner = playerAHP.GetCurrentHP() > playerBHP.GetCurrentHP() ? "player" : "enemy";
            TriggerMatchEnd(winner);
        }
    }

    private void HandleGameOver(string winner)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        TriggerMatchEnd(winner);
    }

    private void TriggerMatchEnd(string winner)
    {
        if (isMatchEndTriggered) return;
        isMatchEndTriggered = true;
        
        photonView.RPC("RPCTriggerMatchEnd", RpcTarget.AllBuffered, winner);
    }

    [PunRPC]
    private void RPCTriggerMatchEnd(string winner)
    {
        if (isMatchEndTriggered) return;
        isMatchEndTriggered = true;

        string localResultText;
        
        if (PhotonNetwork.IsMasterClient)
        {
            localResultText = winner == "player" ? "Victory!" : "Defeat!";
        }
        else
        {
            localResultText = winner == "enemy" ? "Victory!" : "Defeat!";
        }
        
        OnMatchEnd?.Invoke(localResultText);

        // Force the BattleResultsUI to show results
        BattleResultsUI resultsUI = FindFirstObjectByType<BattleResultsUI>();
        if (resultsUI != null)
        {
            resultsUI.photonView.RPC("RPCShowMatchResults", RpcTarget.All, localResultText);
        }

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DelayedEndMatch(5f));
        }
    }

    private System.Collections.IEnumerator DelayedEndMatch(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Add a flag to ensure both clients get back to the main menu
        photonView.RPC("RPCPrepareForMainMenu", RpcTarget.All);
    }

    [PunRPC]
    private void RPCPrepareForMainMenu()
    {
        // Set the flag for returning to main menu
        PlayerPrefs.SetInt("ReturningFromGame", 1);
        PlayerPrefs.SetInt("ShowMainMenu", 1);
        PlayerPrefs.SetInt("KeepWalletConnected", 1);
        PlayerPrefs.Save();
        
        // If we're in a room, leave it
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    public void StartNewRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPCStartNewRound", RpcTarget.All);
    }

    [PunRPC]
    private void RPCStartNewRound()
    {
        ForceHPUpdate();
        OnRoundStart?.Invoke(currentRound);
    }

    private void ForceHPUpdate()
    {
        if (playerAHP != null)
        {
            playerAHP.TriggerHPChanged();
        }
        if (playerBHP != null)
        {
            playerBHP.TriggerHPChanged();
        }
    }

    public void HandleRoundEnd(string winner)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Check for a match-ending condition
        if (playerAHP != null && playerBHP != null)
        {
            if (playerAHP.GetCurrentHP() <= 0 || playerBHP.GetCurrentHP() <= 0)
            {
                TriggerMatchEnd(winner);
                return;
            }
        }

        int survivingUnits = CountSurvivingUnits(winner);
        
        bool isMatchEnd = false;
        float damage = CalculateDamage(survivingUnits);
        
        if (winner == "player" && playerBHP != null)
        {
            float hpAfterDamage = playerBHP.GetCurrentHP() - damage;
            isMatchEnd = hpAfterDamage <= 0;
        }
        else if (winner == "enemy" && playerAHP != null)
        {
            float hpAfterDamage = playerAHP.GetCurrentHP() - damage;
            isMatchEnd = hpAfterDamage <= 0;
        }
        
        photonView.RPC("RPCHandleRoundEnd", RpcTarget.All, winner, survivingUnits, isMatchEnd);
    }

    private float CalculateDamage(int survivingUnits)
    {
        float baseDamage = 10f;
        float damagePerUnit = 1.5f;
        float minDamage = 12f;
        
        float damage = baseDamage + (damagePerUnit * survivingUnits);
        return Mathf.Max(damage, minDamage);
    }

    [PunRPC]
    private void RPCHandleRoundEnd(string winner, int survivingUnits, bool isMatchEnd)
    {
        string localResultText;
        string localTeam;
        string winningTeam;
        bool isLocalPlayerWinner;

        if (PhotonNetwork.IsMasterClient)
        {
            isLocalPlayerWinner = winner == "player";
            localTeam = "TeamA";
            winningTeam = winner == "player" ? "TeamA" : "TeamB";
        }
        else
        {
            isLocalPlayerWinner = winner == "enemy";
            localTeam = "TeamB";
            winningTeam = winner == "enemy" ? "TeamB" : "TeamA";
        }

        localResultText = isLocalPlayerWinner ? "Victory!" : "Defeat!";

        float playerAInitialHP = playerAHP != null ? playerAHP.GetCurrentHP() : 0;
        float playerBInitialHP = playerBHP != null ? playerBHP.GetCurrentHP() : 0;

        if (winner == "player")
        {
            playerAHP.IncrementWinStreak();
            playerBHP.ResetWinStreak();
            playerBHP.TakeDamage(survivingUnits);
            
            if (isMatchEnd || playerBHP.IsDead())
            {
                TriggerMatchEnd(winner);
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
                TriggerMatchEnd(winner);
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
        }
        else
        {
            unitsToCount = GameManager.Instance.GetEnemyUnits();
        }

        int count = unitsToCount.Count(u => 
            u != null && 
            u.GetCurrentState() != UnitState.Dead &&
            u.gameObject.activeInHierarchy
        );
        
        return count;
    }

    public void PrepareNextRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

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
        isRoundActive = false;

        var orphanedHealthSystems = FindObjectsOfType<HealthSystem>()
            .Where(hs => hs.transform.parent == null)
            .ToArray();

        foreach (var healthSystem in orphanedHealthSystems)
        {
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
    public bool IsMatchEndTriggered() => isMatchEndTriggered;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentRound);
            stream.SendNext(isRoundActive);
            stream.SendNext(isMatchEndTriggered);
        }
        else
        {
            this.currentRound = (int)stream.ReceiveNext();
            this.isRoundActive = (bool)stream.ReceiveNext();
            this.isMatchEndTriggered = (bool)stream.ReceiveNext();
        }
    }
}