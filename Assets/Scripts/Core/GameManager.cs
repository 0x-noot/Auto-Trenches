using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;

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
    private bool isInitialized = false;
    private Dictionary<BaseUnit, bool> pendingDeaths = new Dictionary<BaseUnit, bool>();
    private Dictionary<string, float> lastDeathTime = new Dictionary<string, float>()
    {
        { "player", 0f },
        { "enemy", 0f }
    };

    public event Action<GameState> OnGameStateChanged;
    public event Action<BaseUnit> OnUnitDied;
    public event Action<string> OnGameOver;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Clear all pending RPCs and network messages
        if (PhotonNetwork.IsMessageQueueRunning)
            PhotonNetwork.IsMessageQueueRunning = false;
    }

    private void OnDestroy()
    {
        CleanupUnits();
    }

    private void Start()
    {
        if (!isInitialized)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("Not connected to Photon Network!");
            return;
        }

        // Make sure message queue is running
        if (!PhotonNetwork.IsMessageQueueRunning)
        {
            PhotonNetwork.IsMessageQueueRunning = true;
        }

        UpdateGameState(GameState.Setup);
        
        if (placementManager == null)
        {
            Debug.LogError("GameManager: Missing placement manager reference!");
            return;
        }

        // Short delay before transitioning to placement
        StartCoroutine(TransitionToPlacement());
    }
    
    private IEnumerator TransitionToPlacement()
    {
        // Wait a short moment to ensure everything is ready
        yield return new WaitForSeconds(0.5f);
        
        // Transition to placement phase
        UpdateGameState(GameState.PlayerAPlacement);
        isInitialized = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear existing units and reset state
        CleanupUnits();
        
        // Initialize for battle scene
        if (scene.name == "BattleScene")
        {
            StartCoroutine(InitializeAfterSceneLoad());
        }
    }
    
    private IEnumerator InitializeAfterSceneLoad()
    {
        // Wait a frame for everything to setup
        yield return null;

        // Enable message queue if it was disabled
        if (!PhotonNetwork.IsMessageQueueRunning)
        {
            PhotonNetwork.IsMessageQueueRunning = true;
        }

        // Wait until message queue is running
        while (!PhotonNetwork.IsMessageQueueRunning)
        {
            yield return null;
        }

        // Now initialize
        Initialize();
    }

    private void CleanupUnits()
    {
        foreach (var unit in playerUnits.ToList())
        {
            if (unit != null)
            {
                unit.OnUnitDeath -= HandleUnitDeath;
            }
        }
        foreach (var unit in enemyUnits.ToList())
        {
            if (unit != null)
            {
                unit.OnUnitDeath -= HandleUnitDeath;
            }
        }

        playerUnits.Clear();
        enemyUnits.Clear();
        pendingDeaths.Clear();
        isBattleEnding = false;
        
        // Reset death times
        lastDeathTime["player"] = 0f;
        lastDeathTime["enemy"] = 0f;
    }

    private void CleanupProjectiles()
    {
        // Find all active projectiles
        ArrowProjectile[] arrows = FindObjectsOfType<ArrowProjectile>();
        MagicProjectile[] spells = FindObjectsOfType<MagicProjectile>();
        
        // Clean up arrows
        foreach (var arrow in arrows)
        {
            if (arrow != null && arrow.gameObject.activeInHierarchy)
            {
                Destroy(arrow.gameObject);
            }
        }
        
        // Clean up spells
        foreach (var spell in spells)
        {
            if (spell != null && spell.gameObject.activeInHierarchy)
            {
                Destroy(spell.gameObject);
            }
        }
    }

    public void PrepareNextRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Clean up any leftover projectiles
        CleanupProjectiles();
        
        // Reset death times for the next round
        lastDeathTime["player"] = 0f;
        lastDeathTime["enemy"] = 0f;
        
        photonView.RPC("RPCPrepareNextRound", RpcTarget.All);
    }

    [PunRPC]
    private void RPCPrepareNextRound()
    {
        Debug.Log("Executing round preparation");
        
        // Reset death times on all clients
        lastDeathTime["player"] = 0f;
        lastDeathTime["enemy"] = 0f;
        
        // Clean up any orphaned health bars
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

    public void RegisterPlayerUnit(BaseUnit unit)
    {
        if (!playerUnits.Contains(unit))
        {
            playerUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
        }
    }

    public void RegisterEnemyUnit(BaseUnit unit)
    {
        if (!enemyUnits.Contains(unit))
        {
            enemyUnits.Add(unit);
            unit.OnUnitDeath += HandleUnitDeath;
        }
    }

    public void HandleUnitDeath(BaseUnit unit)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.IsMessageQueueRunning) return;
        
        if (currentGameState != GameState.BattleActive || unit == null) return;
        
        photonView.RPC("RPCHandleUnitDeath", RpcTarget.All, unit.photonView.ViewID);
    }

    [PunRPC]
    private void RPCHandleUnitDeath(int unitViewID)
    {
        PhotonView unitView = PhotonView.Find(unitViewID);
        if (unitView == null) return;

        BaseUnit unit = unitView.GetComponent<BaseUnit>();
        if (unit == null) return;

        OnUnitDied?.Invoke(unit);

        if (!pendingDeaths.ContainsKey(unit))
        {
            pendingDeaths[unit] = true;
            
            // Track the time of death for this team
            string team = playerUnits.Contains(unit) ? "player" : "enemy";
            lastDeathTime[team] = Time.time;
            Debug.Log($"Unit death recorded for team {team} at time {lastDeathTime[team]}");
            
            StartCoroutine(HandleDeathAfterAnimation(unit));
        }
    }

    private IEnumerator HandleDeathAfterAnimation(BaseUnit unit)
    {
        yield return new WaitForSeconds(unit.GetDeathAnimationDuration());

        if (unit != null && unit.gameObject != null)
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

        if (currentGameState == GameState.BattleActive && 
            !isBattleEnding && 
            PhotonNetwork.IsMasterClient && 
            PhotonNetwork.IsMessageQueueRunning)
        {
            CheckBattleEnd();
        }
    }

    private int CountAliveUnits(List<BaseUnit> units)
    {
        // Remove null references first - more efficient than filtering during counting
        units.RemoveAll(u => u == null);

        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BaseUnit unit = units[i];
            if (unit == null) continue;

            bool isReallyAlive = unit.GetCurrentState() != UnitState.Dead && 
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
        // Wait for all pending deaths to finish
        if (HasPendingDeaths(playerUnits) || HasPendingDeaths(enemyUnits))
        {
            return;
        }

        int alivePlayers = CountAliveUnits(playerUnits);
        int aliveEnemies = CountAliveUnits(enemyUnits);

        Debug.Log($"CheckBattleEnd: Alive players: {alivePlayers}, Alive enemies: {aliveEnemies}");

        if (alivePlayers == 0 && aliveEnemies == 0)
        {
            // Both teams were eliminated - use time of death to determine winner
            // Team whose last unit died later loses (other team wins)
            float playerLastDeath = lastDeathTime["player"];
            float enemyLastDeath = lastDeathTime["enemy"];
            
            Debug.Log($"All units eliminated. Player last death: {playerLastDeath}, Enemy last death: {enemyLastDeath}");
            
            if (Mathf.Approximately(playerLastDeath, enemyLastDeath))
            {
                // If deaths occurred at exactly the same time (very unlikely)
                // Default to player (host) winning
                Debug.Log("Death times identical - player wins by default");
                EndBattle("player");
            }
            else if (playerLastDeath > enemyLastDeath)
            {
                // Player's last unit died after enemy's last unit - enemy wins
                Debug.Log("Player's units died last - enemy wins");
                EndBattle("enemy");
            }
            else
            {
                // Enemy's last unit died after player's last unit - player wins
                Debug.Log("Enemy's units died last - player wins");
                EndBattle("player");
            }
        }
        else if (alivePlayers == 0 && aliveEnemies > 0)
        {
            EndBattle("enemy");
        }
        else if (aliveEnemies == 0 && alivePlayers > 0)
        {
            EndBattle("player");
        }
    }

    public void StartBattle()
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.IsMessageQueueRunning) return;

        if (currentGameState != GameState.PlayerAPlacement && currentGameState != GameState.PlayerBPlacement)
        {
            return;
        }

        photonView.RPC("RPCStartBattle", RpcTarget.All);
    }

    [PunRPC]
    private void RPCStartBattle()
    {
        isBattleEnding = false;
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
            if (unit != null && unit.gameObject != null && unit.gameObject.activeInHierarchy)
            {
                EnableUnitCombat(unit);
                yield return new WaitForSeconds(unitActivationInterval);
            }
        }
        
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

    private void CleanupAllEffects()
    {
        // Try to find and destroy all effect objects by their tags
        string[] effectTags = { "StunEffect", "HealEffect", "StrengthEffect" };
        
        foreach (string tag in effectTags)
        {
            try
            {
                GameObject[] effects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject effect in effects)
                {
                    PhotonView view = effect.GetComponent<PhotonView>();
                    if (view != null && PhotonNetwork.IsMasterClient)
                    {
                        PhotonNetwork.Destroy(effect);
                        Debug.Log($"Master cleaning up orphaned {tag}: {effect.name}");
                    }
                    else if (view != null && view.IsMine)
                    {
                        PhotonNetwork.Destroy(effect);
                        Debug.Log($"Client cleaning up owned {tag}: {effect.name}");
                    }
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning($"Tag '{tag}' not defined. Skipping tag-based cleanup.");
            }
        }
    }

    private void EndBattle(string winner)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.IsMessageQueueRunning) return;

        if (isBattleEnding)
        {
            return;
        }

        // Clean up any projectiles before ending
        CleanupProjectiles();
        CleanupAllEffects();
        // Use AllBuffered to ensure all clients get the end battle message
        photonView.RPC("RPCEndBattle", RpcTarget.AllBuffered, winner);
    }

    [PunRPC]
    private void RPCEndBattle(string winner)
    {
        isBattleEnding = true;
        UpdateGameState(GameState.BattleEnd);
        DisableAllUnits();
        CleanupAllEffects();
        OnGameOver?.Invoke(winner);
    }

    private void DisableAllUnits()
    {
        foreach (var unit in playerUnits.Concat(enemyUnits))
        {
            if (unit != null && unit.gameObject != null && unit.gameObject.activeInHierarchy)
            {
                DisableUnitCombat(unit);
            }
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
    
    protected virtual void Update()
    {
        if (!PhotonNetwork.IsMasterClient || 
            currentGameState != GameState.BattleActive || 
            isBattleEnding ||
            !PhotonNetwork.IsMessageQueueRunning) 
            return;
            
        // Throttle battle end checks to reduce CPU usage
        if (Time.frameCount % 10 != 0) return; // Only check every 10 frames
            
        CheckBattleEnd();
    }
    
    public void UpdateGameState(GameState newState)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.IsMessageQueueRunning) 
        {
            return;
        }
        photonView.RPC("RPCUpdateGameState", RpcTarget.All, (int)newState);
    }

    [PunRPC]
    private void RPCUpdateGameState(int newStateInt)
    {
        GameState newState = (GameState)newStateInt;
        currentGameState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        
        // Clean up all units
        CleanupUnits();
        
        // Clean up projectiles when disconnecting
        CleanupProjectiles();
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