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
        UpdateGameState(GameState.Setup);
        
        if (placementManager == null)
        {
            Debug.LogError("GameManager: Missing placement manager reference!");
            return;
        }

        RegisterExistingEnemyUnits();
        UpdateGameState(GameState.UnitPlacement);
        Debug.Log("GameManager: Initialization complete");
    }

    private void RegisterExistingEnemyUnits()
    {
        Debug.Log("GameManager: Registering existing enemy units");
        BaseUnit[] sceneUnits = FindObjectsOfType<BaseUnit>();
        foreach (BaseUnit unit in sceneUnits)
        {
            if (!playerUnits.Contains(unit))
            {
                RegisterEnemyUnit(unit);
            }
        }
        Debug.Log($"GameManager: Registered {enemyUnits.Count} existing enemy units");
    }

    public void RegisterPlayerUnit(BaseUnit unit)
    {
        if (!playerUnits.Contains(unit))
        {
            Debug.Log($"GameManager: Registering player unit: {unit.gameObject.name}");
            playerUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
            unit.SetTeam("PlayerTeam");
        }
    }

    public void RegisterEnemyUnit(BaseUnit unit)
    {
        if (!enemyUnits.Contains(unit))
        {
            Debug.Log($"GameManager: Registering enemy unit: {unit.gameObject.name}");
            enemyUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
            unit.SetTeam("EnemyTeam");
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
        Debug.Log($"GameManager: HandleUnitDeath called for unit: {unit.gameObject.name}");
        if (currentGameState != GameState.BattleActive || unit == null) return;
        
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
                Debug.Log("GameManager: Removed unit from player units");
            }
            else if (enemyUnits.Contains(unit))
            {
                enemyUnits.Remove(unit);
                Debug.Log("GameManager: Removed unit from enemy units");
            }

            pendingDeaths.Remove(unit);
        }

        // Clean up null references
        playerUnits.RemoveAll(u => u == null);
        enemyUnits.RemoveAll(u => u == null);

        if (currentGameState == GameState.BattleActive)
        {
            Debug.Log("GameManager: Checking battle end after unit death");
            CheckBattleEnd();
        }
    }

    private void CheckBattleEnd()
    {
        if (currentGameState != GameState.BattleActive || isBattleEnding)
        {
            Debug.Log($"GameManager: CheckBattleEnd early return - currentState: {currentGameState}, isBattleEnding: {isBattleEnding}");
            return;
        }

        // First check if there are any pending deaths
        bool playersPending = HasPendingDeaths(playerUnits);
        bool enemiesPending = HasPendingDeaths(enemyUnits);

        Debug.Log($"GameManager: Pending deaths - Players: {playersPending}, Enemies: {enemiesPending}");

        // Don't check for battle end if there are any pending deaths
        if (playersPending || enemiesPending) return;

        int alivePlayers = CountAliveUnits(playerUnits);
        int aliveEnemies = CountAliveUnits(enemyUnits);

        Debug.Log($"GameManager: Alive units - Players: {alivePlayers}, Enemies: {aliveEnemies}");

        if (alivePlayers == 0)
        {
            Debug.Log("GameManager: No players alive, enemy wins");
            EndBattle("enemy");
        }
        else if (aliveEnemies == 0)
        {
            Debug.Log("GameManager: No enemies alive, player wins");
            EndBattle("player");
        }
    }

    private int CountAliveUnits(List<BaseUnit> units)
    {
        return units.Count(unit => 
            unit != null && 
            unit.GetCurrentState() != UnitState.Dead && 
            !pendingDeaths.ContainsKey(unit)
        );
    }

    private bool HasPendingDeaths(List<BaseUnit> units)
    {
        return units.Count(u => pendingDeaths.ContainsKey(u)) > 0;
    }

    public void StartBattle()
    {
        Debug.Log("GameManager: StartBattle called");
        if (currentGameState != GameState.UnitPlacement)
        {
            Debug.Log($"GameManager: Cannot start battle in current state: {currentGameState}");
            return;
        }

        if (playerUnits.Count < maxUnitsPerPlayer)
        {
            Debug.LogWarning("GameManager: Not all player units have been placed!");
            return;
        }

        Debug.Log("GameManager: Starting battle sequence");
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
        yield return null;
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
        Debug.Log($"GameManager: EndBattle called with winner: {winner}");
        if (isBattleEnding)
        {
            Debug.Log("GameManager: Battle already ending, returning");
            return;
        }

        isBattleEnding = true;
        Debug.Log("GameManager: Setting battle end state");
        UpdateGameState(GameState.BattleEnd);
        DisableAllUnits();
        StartCoroutine(GameOverSequence(winner));
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

    private IEnumerator GameOverSequence(string winner)
    {
        Debug.Log($"GameManager: Starting game over sequence for winner: {winner}");
        yield return new WaitForSeconds(endGameDelay);
        
        Debug.Log("GameManager: Updating game state to GameOver");
        UpdateGameState(GameState.GameOver);
        
        Debug.Log($"GameManager: Invoking OnGameOver with winner: {winner}");
        OnGameOver?.Invoke(winner);
        
        isBattleEnding = false;
        Debug.Log("GameManager: Game over sequence complete");
    }

    private void UpdateGameState(GameState newState)
    {
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