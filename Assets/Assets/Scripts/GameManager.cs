using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private float battleStartDelay = 0.1f;
    [SerializeField] private float endGameDelay = 5f;
    [SerializeField] private float unitActivationInterval = 0.1f;

    [Header("References")]
    [SerializeField] private PlacementManager placementManager;

    private GameState currentGameState;
    private List<BaseUnit> playerUnits = new List<BaseUnit>();
    private List<BaseUnit> enemyUnits = new List<BaseUnit>();
    private bool isBattleEnding = false;
    private Dictionary<BaseUnit, bool> pendingDeaths = new Dictionary<BaseUnit, bool>();

    public event Action<GameState> OnGameStateChanged;
    public event Action<BaseUnit> OnUnitDied;
    public event Action<string> OnGameOver;

    private void Awake()
    {
        Debug.Log("GameManager: Awake called");
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("GameManager: Instance set");
        }
        else
        {
            Debug.Log("GameManager: Duplicate instance found, destroying");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Debug.Log("GameManager: Start called");
        Initialize();
    }

    private void Initialize()
    {
        Debug.Log("GameManager: Initializing");
        
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("Not connected to Photon Network!");
            return;
        }

        UpdateGameState(GameState.Setup);
        
        if (placementManager == null)
        {
            Debug.LogError("GameManager: Missing placement manager reference!");
            return;
        }

        // Game starts with simultaneous placement
        UpdateGameState(GameState.PlayerAPlacement);
        Debug.Log("GameManager: Initialization complete");
    }

    public void PrepareNextRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("GameManager: Preparing next round");
        
        photonView.RPC("RPCPrepareNextRound", RpcTarget.All);
    }

    [PunRPC]
    private void RPCPrepareNextRound()
    {
        // Clear all units
        playerUnits.Clear();
        enemyUnits.Clear();
        pendingDeaths.Clear();
        isBattleEnding = false;

        // Reset game state for placement
        UpdateGameState(GameState.PlayerAPlacement);
    }

    public void RegisterPlayerUnit(BaseUnit unit)
    {
        if (!playerUnits.Contains(unit))
        {
            Debug.Log($"GameManager: Registering player unit: {unit.gameObject.name}");
            playerUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
        }
    }

    public void RegisterEnemyUnit(BaseUnit unit)
    {
        if (!enemyUnits.Contains(unit))
        {
            Debug.Log($"GameManager: Registering enemy unit: {unit.gameObject.name}");
            enemyUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
        }
    }

    private void OnDestroy()
    {
        Debug.Log("GameManager: OnDestroy called");
        foreach (var unit in playerUnits)
        {
            if (unit != null) unit.OnUnitDeath -= HandleUnitDeath;
        }
        foreach (var unit in enemyUnits)
        {
            if (unit != null) unit.OnUnitDeath -= HandleUnitDeath;
        }
    }

    public void HandleUnitDeath(BaseUnit unit)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        Debug.Log($"GameManager: HandleUnitDeath called for unit: {unit.gameObject.name}");
        if (currentGameState != GameState.BattleActive || unit == null) return;
        
        photonView.RPC("RPCHandleUnitDeath", RpcTarget.All, unit.photonView.ViewID);
    }

    [PunRPC]
    private void RPCHandleUnitDeath(int unitViewId)
    {
        PhotonView unitView = PhotonView.Find(unitViewId);
        if (unitView == null) return;

        BaseUnit unit = unitView.GetComponent<BaseUnit>();
        if (unit == null) return;

        OnUnitDied?.Invoke(unit);

        if (!pendingDeaths.ContainsKey(unit))
        {
            Debug.Log($"GameManager: Adding unit to pending deaths: {unit.gameObject.name}");
            pendingDeaths[unit] = true;
            StartCoroutine(HandleDeathAfterAnimation(unit));
        }
    }

    private IEnumerator HandleDeathAfterAnimation(BaseUnit unit)
    {
        Debug.Log($"GameManager: Starting death animation for unit: {unit.gameObject.name}");
        yield return new WaitForSeconds(unit.GetDeathAnimationDuration());

        if (unit != null)
        {
            if (playerUnits.Contains(unit))
            {
                playerUnits.Remove(unit);
                Debug.Log($"GameManager: Removed unit {unit.gameObject.name} from player units");
            }
            else if (enemyUnits.Contains(unit))
            {
                enemyUnits.Remove(unit);
                Debug.Log($"GameManager: Removed unit {unit.gameObject.name} from enemy units");
            }

            pendingDeaths.Remove(unit);
            Debug.Log($"GameManager: Removed {unit.gameObject.name} from pending deaths");
        }

        // Clean up null references
        playerUnits.RemoveAll(u => u == null);
        enemyUnits.RemoveAll(u => u == null);

        if (currentGameState == GameState.BattleActive && !isBattleEnding && PhotonNetwork.IsMasterClient)
        {
            Debug.Log("GameManager: Checking battle end after unit death");
            CheckBattleEnd();
        }
    }

    private int CountAliveUnits(List<BaseUnit> units)
    {
        units.RemoveAll(u => u == null);

        int count = 0;
        foreach (var unit in units)
        {
            if (unit == null) continue;

            bool isReallyAlive = unit != null && 
                                unit.GetCurrentState() != UnitState.Dead && 
                                !pendingDeaths.ContainsKey(unit) &&
                                unit.gameObject != null &&
                                unit.gameObject.activeInHierarchy;

            if (isReallyAlive)
            {
                count++;
            }
        }

        return count;
    }

    private bool HasPendingDeaths(List<BaseUnit> units)
    {
        return units.Count(u => pendingDeaths.ContainsKey(u)) > 0;
    }

    private void CheckBattleEnd()
    {
        if (!PhotonNetwork.IsMasterClient || currentGameState != GameState.BattleActive || isBattleEnding)
        {
            return;
        }

        // Wait for all pending deaths to finish
        if (HasPendingDeaths(playerUnits) || HasPendingDeaths(enemyUnits))
        {
            Debug.Log("Waiting for pending deaths to complete...");
            return;
        }

        int alivePlayers = CountAliveUnits(playerUnits);
        int aliveEnemies = CountAliveUnits(enemyUnits);

        Debug.Log($"Battle check - Player Units Alive: {alivePlayers}, Enemy Units Alive: {aliveEnemies}");

        if (alivePlayers == 0 && aliveEnemies > 0)
        {
            Debug.Log($"Enemy wins with {aliveEnemies} units remaining");
            EndBattle("enemy");
        }
        else if (aliveEnemies == 0 && alivePlayers > 0)
        {
            Debug.Log($"Player wins with {alivePlayers} units remaining");
            EndBattle("player");
        }
    }

    public void StartBattle()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("GameManager: StartBattle called");
        if (currentGameState != GameState.PlayerAPlacement && currentGameState != GameState.PlayerBPlacement)
        {
            Debug.Log($"GameManager: Cannot start battle in current state: {currentGameState}");
            return;
        }

        photonView.RPC("RPCStartBattle", RpcTarget.All);
    }

    [PunRPC]
    private void RPCStartBattle()
    {
        Debug.Log("GameManager: Battle sequence beginning");
        isBattleEnding = false;
        UpdateGameState(GameState.BattleStart);
        StartCoroutine(BattleStartSequence());
    }

    private IEnumerator BattleStartSequence()
    {
        Debug.Log("GameManager: Battle start sequence beginning");
        List<BaseUnit> allUnits = new List<BaseUnit>();
        allUnits.AddRange(playerUnits);
        allUnits.AddRange(enemyUnits);

        foreach (var unit in allUnits)
        {
            if (unit != null)
            {
                EnableUnitCombat(unit);
                yield return new WaitForSeconds(unitActivationInterval);
            }
        }
        
        Debug.Log("GameManager: Battle start sequence complete");
        UpdateGameState(GameState.BattleActive);
    }

    private void EnableUnitCombat(BaseUnit unit)
    {
        Debug.Log($"GameManager: Enabling combat for unit: {unit.gameObject.name}");
        var targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.StartTargeting();
        }
    }

    private void EndBattle(string winner)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"GameManager: EndBattle called with winner: {winner}");
        if (isBattleEnding)
        {
            Debug.Log("GameManager: Battle already ending, returning");
            return;
        }

        photonView.RPC("RPCEndBattle", RpcTarget.All, winner);
    }

    [PunRPC]
    private void RPCEndBattle(string winner)
    {
        isBattleEnding = true;
        Debug.Log("GameManager: Setting battle end state and triggering OnGameOver");
        UpdateGameState(GameState.BattleEnd);
        DisableAllUnits();
        OnGameOver?.Invoke(winner);
    }

    private void DisableAllUnits()
    {
        Debug.Log("GameManager: Disabling all units");
        foreach (var unit in playerUnits.Concat(enemyUnits))
        {
            if (unit != null) DisableUnitCombat(unit);
        }
    }

    private void DisableUnitCombat(BaseUnit unit)
    {
        Debug.Log($"GameManager: Disabling combat for unit: {unit.gameObject.name}");
        var targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.StopTargeting();
        }
    }

    public void UpdateGameState(GameState newState)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPCUpdateGameState", RpcTarget.All, (int)newState);
    }

    [PunRPC]
    private void RPCUpdateGameState(int newStateInt)
    {
        GameState newState = (GameState)newStateInt;
        Debug.Log($"GameManager: Changing state from {currentGameState} to {newState}");
        currentGameState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    public GameState GetCurrentState()
    {
        return currentGameState;
    }

    public List<BaseUnit> GetPlayerUnits()
    {
        return new List<BaseUnit>(playerUnits);
    }

    public List<BaseUnit> GetEnemyUnits()
    {
        return new List<BaseUnit>(enemyUnits);
    }
}