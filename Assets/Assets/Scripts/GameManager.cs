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
        Debug.Log($"Registered {enemyUnits.Count} existing enemy units");
    }

    public void RegisterPlayerUnit(BaseUnit unit)
    {
        if (!playerUnits.Contains(unit))
        {
            playerUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
            unit.SetTeam("PlayerTeam");
        }
    }

    public void RegisterEnemyUnit(BaseUnit unit)
    {
        if (!enemyUnits.Contains(unit))
        {
            enemyUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
            unit.SetTeam("EnemyTeam");
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
            }
            else if (enemyUnits.Contains(unit))
            {
                enemyUnits.Remove(unit);
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
        if (playersPending || enemiesPending) return;

        int alivePlayers = CountAliveUnits(playerUnits);
        int aliveEnemies = CountAliveUnits(enemyUnits);

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
        if (currentGameState != GameState.UnitPlacement) return;

        if (playerUnits.Count < maxUnitsPerPlayer)
        {
            Debug.LogWarning("Not all player units have been placed!");
            return;
        }

        UpdateGameState(GameState.BattleStart);
        StartCoroutine(BattleStartSequence());
    }

    private IEnumerator BattleStartSequence()
    {
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
        
        UpdateGameState(GameState.BattleActive);
        yield return null;
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