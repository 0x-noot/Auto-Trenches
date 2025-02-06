using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public class BattleRoundManager : MonoBehaviour
{
    public static BattleRoundManager Instance { get; private set; }

    [Header("Player References")]
    [SerializeField] private PlayerHP playerAHP;
    [SerializeField] private PlayerHP playerBHP;

    private int currentRound = 1;
    private bool isRoundActive = false;

    public event Action<int> OnRoundStart;
    public event Action<string, int> OnRoundEnd; // winner, surviving units
    public event Action<string> OnMatchEnd; // winner

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Find PlayerHP components if not assigned in inspector
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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            GameManager.Instance.OnGameOver += HandleRoundEnd;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameOver -= HandleRoundEnd;
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.BattleActive) isRoundActive = true;
        else if (newState == GameState.PlayerAPlacement) isRoundActive = false;
    }

    public void StartNewRound()
    {
        // Force HP update when starting a new round
        ForceHPUpdate();
        OnRoundStart?.Invoke(currentRound);
    }

    private void ForceHPUpdate()
    {
        // Manually trigger HP update through BattleRoundManager methods
        if (playerAHP != null)
        {
            float currentAHP = playerAHP.GetCurrentHP();
            playerAHP.TriggerHPChanged();
        }
        if (playerBHP != null)
        {
            float currentBHP = playerBHP.GetCurrentHP();
            playerBHP.TriggerHPChanged();
        }
    }

    private void HandleRoundEnd(string winner)
    {
        int survivingUnits = CountSurvivingUnits(winner);

        if (winner == "player")
        {
            playerAHP.IncrementWinStreak();
            playerBHP.ResetWinStreak();
            playerBHP.TakeDamage(survivingUnits);
            
            if (playerBHP.IsDead())
            {
                OnMatchEnd?.Invoke("player");
                return;
            }
        }
        else
        {
            playerBHP.IncrementWinStreak();
            playerAHP.ResetWinStreak();
            playerAHP.TakeDamage(survivingUnits);
            
            if (playerAHP.IsDead())
            {
                OnMatchEnd?.Invoke("enemy");
                return;
            }
        }

        // Force HP update after damage calculation
        ForceHPUpdate();

        OnRoundEnd?.Invoke(winner, survivingUnits);
        currentRound++;
        PrepareNextRound();
    }

    private int CountSurvivingUnits(string winner)
    {
        if (GameManager.Instance == null) return 0;

        // We want to return the surviving units of the WINNING team
        // This is used to calculate damage to the losing player
        List<BaseUnit> unitsToCount;
        if (winner == "player")
        {
            // Player won, count PLAYER surviving units
            unitsToCount = GameManager.Instance.GetPlayerUnits();
            Debug.Log($"Counting player surviving units after victory");
        }
        else
        {
            // Enemy won, count ENEMY surviving units
            unitsToCount = GameManager.Instance.GetEnemyUnits();
            Debug.Log($"Counting enemy surviving units after victory");
        }

        int count = unitsToCount.Count(u => 
            u != null && 
            u.GetCurrentState() != UnitState.Dead &&
            u.gameObject.activeInHierarchy
        );
        
        Debug.Log($"Surviving units count for {winner}: {count}");
        return count;
    }

    private void PrepareNextRound()
    {
        PlacementManager placementManager = FindFirstObjectByType<PlacementManager>();
        if (placementManager != null) placementManager.ClearUnits();

        if (GameManager.Instance != null) GameManager.Instance.PrepareNextRound();
    }

    public int GetCurrentRound() => currentRound;
    public float GetPlayerAHP() => playerAHP.GetCurrentHP();
    public float GetPlayerBHP() => playerBHP.GetCurrentHP();
}