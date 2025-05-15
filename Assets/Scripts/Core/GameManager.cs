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
        
        if (PhotonNetwork.IsMessageQueueRunning)
            PhotonNetwork.IsMessageQueueRunning = false;
    }

    private void OnDestroy()
    {
        CleanupUnits();

        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnMatchEnd -= HandleMatchEnd;
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnMatchEnd += HandleMatchEnd;
        }
    }

    private void Initialize()
    {
        if (!PhotonNetwork.IsConnected)
        {
            return;
        }

        if (!PhotonNetwork.IsMessageQueueRunning)
        {
            PhotonNetwork.IsMessageQueueRunning = true;
        }

        UpdateGameState(GameState.Setup);
        
        if (placementManager == null)
        {
            return;
        }

        StartCoroutine(TransitionToPlacement());
    }
    
    private IEnumerator TransitionToPlacement()
    {
        yield return new WaitForSeconds(0.5f);
        
        UpdateGameState(GameState.PlayerAPlacement);
        isInitialized = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CleanupUnits();
        
        if (scene.name == "BattleScene")
        {
            StartCoroutine(InitializeAfterSceneLoad());
        }
    }
    
    private IEnumerator InitializeAfterSceneLoad()
    {
        yield return null;
        
        placementManager = FindFirstObjectByType<PlacementManager>();

        if (!PhotonNetwork.IsMessageQueueRunning)
        {
            PhotonNetwork.IsMessageQueueRunning = true;
        }

        while (!PhotonNetwork.IsMessageQueueRunning)
        {
            yield return null;
        }

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
        
        lastDeathTime["player"] = 0f;
        lastDeathTime["enemy"] = 0f;
    }

    private void CleanupProjectiles()
    {
        ArrowProjectile[] arrows = FindObjectsOfType<ArrowProjectile>();
        MagicProjectile[] spells = FindObjectsOfType<MagicProjectile>();
        
        foreach (var arrow in arrows)
        {
            if (arrow != null && arrow.gameObject.activeInHierarchy)
            {
                Destroy(arrow.gameObject);
            }
        }
        
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

        CleanupProjectiles();
        
        lastDeathTime["player"] = 0f;
        lastDeathTime["enemy"] = 0f;
        
        photonView.RPC("RPCPrepareNextRound", RpcTarget.All);
    }

    [PunRPC]
    private void RPCPrepareNextRound()
    {
        lastDeathTime["player"] = 0f;
        lastDeathTime["enemy"] = 0f;
        
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

    private void HandleMatchEnd(string result)
    {
        UpdateGameState(GameState.GameOver);
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
            
            string team = playerUnits.Contains(unit) ? "player" : "enemy";
            lastDeathTime[team] = Time.time;
            
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

    private void HandleRoundFinished(string winner)
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.HandleRoundEnd(winner);
        }
        else
        {
            EndBattle(winner);
        }
    }

    private void CheckBattleEnd()
    {
        if (HasPendingDeaths(playerUnits) || HasPendingDeaths(enemyUnits))
        {
            return;
        }

        int alivePlayers = CountAliveUnits(playerUnits);
        int aliveEnemies = CountAliveUnits(enemyUnits);

        if (alivePlayers == 0 || aliveEnemies == 0)
        {
            string roundWinner;
            
            if (alivePlayers == 0 && aliveEnemies == 0)
            {
                float playerLastDeath = lastDeathTime["player"];
                float enemyLastDeath = lastDeathTime["enemy"];
                
                if (Mathf.Approximately(playerLastDeath, enemyLastDeath))
                {
                    roundWinner = "player";
                }
                else if (playerLastDeath > enemyLastDeath)
                {
                    roundWinner = "enemy";
                }
                else
                {
                    roundWinner = "player";
                }
            }
            else if (alivePlayers == 0 && aliveEnemies > 0)
            {
                roundWinner = "enemy";
            }
            else
            {
                roundWinner = "player";
            }
            
            HandleRoundFinished(roundWinner);
            
            isBattleEnding = true;
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
                if (unit.photonView.IsMine)
                {
                    unit.ApplyDefaultStats();
                }
                
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
                    }
                    else if (view != null && view.IsMine)
                    {
                        PhotonNetwork.Destroy(effect);
                    }
                }
            }
            catch (UnityException)
            {
            }
        }
    }

    private void CleanupAllNetworkObjects()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        var arrows = FindObjectsOfType<ArrowProjectile>();
        foreach (var arrow in arrows)
        {
            if (arrow != null && arrow.photonView != null && arrow.photonView.IsMine)
            {
                PhotonNetwork.Destroy(arrow.gameObject);
            }
        }
        
        var spells = FindObjectsOfType<MagicProjectile>();
        foreach (var spell in spells)
        {
            if (spell != null && spell.photonView != null && spell.photonView.IsMine)
            {
                PhotonNetwork.Destroy(spell.gameObject);
            }
        }
        
        string[] effectTags = { "StunEffect", "HealEffect", "StrengthEffect", "ShieldEffect", "ExplosionEffect" };
        foreach (string tag in effectTags)
        {
            try
            {
                GameObject[] effects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject effect in effects)
                {
                    PhotonView view = effect.GetComponent<PhotonView>();
                    if (view != null && view.IsMine)
                    {
                        PhotonNetwork.Destroy(effect);
                    }
                }
            }
            catch (System.Exception) { }
        }
    }

    private void CleanupNetworkObjects()
    {
        Debug.Log("[GameManager] Cleaning up network objects");
        
        // Find and destroy arrow projectiles
        ArrowProjectile[] arrows = FindObjectsOfType<ArrowProjectile>();
        foreach (var arrow in arrows)
        {
            if (arrow != null && arrow.gameObject != null)
            {
                PhotonView view = arrow.GetComponent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    Debug.Log($"[GameManager] Destroying arrow: {arrow.gameObject.name}");
                    PhotonNetwork.Destroy(arrow.gameObject);
                }
            }
        }
        
        // Find and destroy magic projectiles
        MagicProjectile[] spells = FindObjectsOfType<MagicProjectile>();
        foreach (var spell in spells)
        {
            if (spell != null && spell.gameObject != null)
            {
                PhotonView view = spell.GetComponent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    Debug.Log($"[GameManager] Destroying spell: {spell.gameObject.name}");
                    PhotonNetwork.Destroy(spell.gameObject);
                }
            }
        }
        
        // Find and destroy effects
        string[] effectTags = { "StunEffect", "HealEffect", "StrengthEffect", "ShieldEffect", "ExplosionEffect" };
        foreach (string tag in effectTags)
        {
            try
            {
                GameObject[] effects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject effect in effects)
                {
                    if (effect != null)
                    {
                        PhotonView view = effect.GetComponent<PhotonView>();
                        if (view != null && view.IsMine)
                        {
                            Debug.Log($"[GameManager] Destroying effect: {effect.name}");
                            PhotonNetwork.Destroy(effect);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameManager] Error cleaning up {tag}: {ex.Message}");
            }
        }
    }

    public void EndBattle(string winner)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.IsMessageQueueRunning) return;

        if (isBattleEnding)
        {
            return;
        }

        isBattleEnding = true;
        
        // Clean up projectiles and effects
        CleanupProjectiles();
        CleanupAllEffects();
        CleanupAllNetworkObjects(); // Add this method from previous example
        
        // Get the battle results UI
        BattleResultsUI resultsUI = FindFirstObjectByType<BattleResultsUI>();
        if (resultsUI != null)
        {
            // Show victory on host if player won
            string hostResult = winner == "player" ? "Victory!" : "Defeat!";
            resultsUI.RPCShowMatchResults(hostResult);
            
            // Show defeat on client if player won (meaning client lost)
            resultsUI.photonView.RPC("RPCShowMatchResults", RpcTarget.Others, 
                winner == "player" ? "Defeat!" : "Victory!");
        }
        
        photonView.RPC("RPCEndBattle", RpcTarget.All, winner);
    }

    [PunRPC]
    public void RPCEndBattle(string winner)
    {
        if (isBattleEnding) return;
        
        isBattleEnding = true;
        UpdateGameState(GameState.BattleEnd);
        DisableAllUnits();
        CleanupAllEffects();
        
        if (ProfileManager.Instance != null)
        {
            bool localPlayerWon = (PhotonNetwork.IsMasterClient && winner == "player") || 
                                (!PhotonNetwork.IsMasterClient && winner == "enemy");
            
            ProfileManager.Instance.RecordMatch(localPlayerWon);
        }
        
        BattleResultsUI resultsUI = FindFirstObjectByType<BattleResultsUI>();
        if (resultsUI != null)
        {
            string resultText = (PhotonNetwork.IsMasterClient && winner == "player") || 
                                (!PhotonNetwork.IsMasterClient && winner == "enemy") ? 
                                "Victory!" : "Defeat!";
            
            resultsUI.RPCShowMatchResults(resultText);
        }
        
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
            
        if (Time.frameCount % 10 != 0) return;
            
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
        
        CleanupUnits();
        
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