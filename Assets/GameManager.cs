using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private int maxUnitsPerPlayer = 3;
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
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        UpdateGameState(GameState.Setup);
        
        if (placementManager == null)
        {
            Debug.LogError("Missing placement manager reference!");
            return;
        }

        RegisterExistingEnemyUnits();
        UpdateGameState(GameState.UnitPlacement);
    }

    private void RegisterExistingEnemyUnits()
    {
        BaseUnit[] sceneUnits = FindObjectsOfType<BaseUnit>();
        foreach (BaseUnit unit in sceneUnits)
        {
            if (!playerUnits.Contains(unit))
            {
                RegisterEnemyUnit(unit);
            }
        }
        Debug.Log($"[GameManager] Registered {enemyUnits.Count} existing enemy units");
    }

    public void RegisterPlayerUnit(BaseUnit unit)
    {
        if (!playerUnits.Contains(unit))
        {
            playerUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
            unit.SetTeam("PlayerTeam");
            Debug.Log($"[GameManager] Registered player unit {unit.name}. Total player units: {playerUnits.Count}");
        }
    }

    public void RegisterEnemyUnit(BaseUnit unit)
    {
        if (!enemyUnits.Contains(unit))
        {
            enemyUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
            unit.SetTeam("EnemyTeam");
            Debug.Log($"[GameManager] Registered enemy unit {unit.name}. Total enemy units: {enemyUnits.Count}");
        }
    }

    private void OnDestroy()
    {
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
        if (currentGameState != GameState.BattleActive || unit == null) return;
        
        Debug.Log($"[GameManager] Unit Death - Name: {unit.name}, Team: {unit.GetTeamId()}, Current State: {unit.GetCurrentState()}");
        
        OnUnitDied?.Invoke(unit);

        if (!pendingDeaths.ContainsKey(unit))
        {
            pendingDeaths[unit] = true;
            StartCoroutine(HandleDeathAfterAnimation(unit));
        }
    }

    private IEnumerator HandleDeathAfterAnimation(BaseUnit unit)
    {
        // Wait for death animation
        yield return new WaitForSeconds(unit.GetDeathAnimationDuration());

        if (unit != null)
        {
            if (playerUnits.Contains(unit))
            {
                playerUnits.Remove(unit);
                Debug.Log($"[GameManager] Player unit death complete. Remaining player units: {playerUnits.Count}");
            }
            else if (enemyUnits.Contains(unit))
            {
                enemyUnits.Remove(unit);
                Debug.Log($"[GameManager] Enemy unit death complete. Remaining enemy units: {enemyUnits.Count}");
            }

            pendingDeaths.Remove(unit);
        }

        // Clean up null references
        playerUnits.RemoveAll(u => u == null);
        enemyUnits.RemoveAll(u => u == null);

        if (currentGameState == GameState.BattleActive)
        {
            CheckBattleEnd();
        }
    }

    private void CheckBattleEnd()
    {
        if (currentGameState != GameState.BattleActive || isBattleEnding) return;

        // First check if there are any pending deaths
        bool playersPending = HasPendingDeaths(playerUnits);
        bool enemiesPending = HasPendingDeaths(enemyUnits);

        // Don't check for battle end if there are any pending deaths
        if (playersPending || enemiesPending)
        {
            Debug.Log("[GameManager] Battle check skipped - deaths still pending");
            return;
        }

        int alivePlayers = CountAliveUnits(playerUnits);
        int aliveEnemies = CountAliveUnits(enemyUnits);

        Debug.Log($"[GameManager] Battle Check - Alive Players: {alivePlayers}, Alive Enemies: {aliveEnemies}");

        if (alivePlayers == 0)
        {
            EndBattle("enemy");
        }
        else if (aliveEnemies == 0)
        {
            EndBattle("player");
        }
    }

    private int CountAliveUnits(List<BaseUnit> units)
    {
        int aliveCount = 0;
        foreach (var unit in units)
        {
            if (unit != null && 
                unit.GetCurrentState() != UnitState.Dead && 
                !pendingDeaths.ContainsKey(unit))
            {
                aliveCount++;
            }
        }

        Debug.Log($"[GameManager] Alive units count for {(units == playerUnits ? "Player" : "Enemy")}: {aliveCount}");
        return aliveCount;
    }

    private bool HasPendingDeaths(List<BaseUnit> units)
    {
        int pendingCount = units.Count(u => pendingDeaths.ContainsKey(u));
        Debug.Log($"[GameManager] Checking pending deaths - Found {pendingCount} pending deaths");
        return pendingCount > 0;
    }

    public void StartBattle()
    {
        if (currentGameState != GameState.UnitPlacement) return;

        if (playerUnits.Count < maxUnitsPerPlayer)
        {
            Debug.LogWarning("[GameManager] Not all player units have been placed!");
            return;
        }

        Debug.Log("[GameManager] Starting Battle");
        UpdateGameState(GameState.BattleStart);
        StartCoroutine(BattleStartSequence());
    }

    private IEnumerator BattleStartSequence()
    {
        Debug.Log($"[GameManager] Battle start sequence beginning");
        
        // Gradually enable units
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

        yield return new WaitForSeconds(battleStartDelay);
        
        Debug.Log("[GameManager] Battle start sequence complete, activating battle");
        UpdateGameState(GameState.BattleActive);
    }

    private void EnableUnitCombat(BaseUnit unit)
    {
        var targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.StartTargeting();
        }
    }

    private void EndBattle(string winner)
    {
        if (isBattleEnding) return;

        Debug.Log($"[GameManager] Ending battle - Winner: {winner}");
        isBattleEnding = true;
        UpdateGameState(GameState.BattleEnd);
        DisableAllUnits();
        StartCoroutine(GameOverSequence(winner));
    }

    private void DisableAllUnits()
    {
        foreach (var unit in playerUnits.Concat(enemyUnits))
        {
            if (unit != null) DisableUnitCombat(unit);
        }
    }

    private void DisableUnitCombat(BaseUnit unit)
    {
        var targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.StopTargeting();
        }
    }

    private IEnumerator GameOverSequence(string winner)
    {
        yield return new WaitForSeconds(endGameDelay);
        UpdateGameState(GameState.GameOver);
        OnGameOver?.Invoke(winner);
        isBattleEnding = false;
    }

    private void UpdateGameState(GameState newState)
    {
        currentGameState = newState;
        OnGameStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] Game State changed to: {newState}");
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