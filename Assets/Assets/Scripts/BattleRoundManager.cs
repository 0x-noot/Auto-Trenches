using UnityEngine;
using System;

public class BattleRoundManager : MonoBehaviour
{
    public static BattleRoundManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int roundsToWin = 2;
    
    private int currentRound = 1;
    private int playerAWins = 0;
    private int playerBWins = 0;

    public event Action<int> OnRoundStart;
    public event Action<string, int> OnRoundEnd; // winner, round number
    public event Action<string, int, int> OnMatchEnd; // winner, playerAScore, playerBScore

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("BattleRoundManager: Instance initialized");
        }
        else
        {
            Debug.LogWarning("BattleRoundManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Debug.Log("BattleRoundManager: Start called");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleRoundEnd;
            Debug.Log("BattleRoundManager: Subscribed to GameManager events");
        }
        else
        {
            Debug.LogError("BattleRoundManager: GameManager instance not found!");
        }
    }

    private void OnDestroy()
    {
        Debug.Log("BattleRoundManager: OnDestroy called");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleRoundEnd;
        }
    }

    public void StartNewRound()
    {
        Debug.Log($"BattleRoundManager: Starting round {currentRound}");
        OnRoundStart?.Invoke(currentRound);
    }

    private void HandleRoundEnd(string winner)
    {
        Debug.Log($"BattleRoundManager: Round {currentRound} ended. Winner: {winner}");
        
        if (winner == "player")
        {
            playerAWins++;
            Debug.Log($"BattleRoundManager: Player A wins increased to {playerAWins}");
        }
        else
        {
            playerBWins++;
            Debug.Log($"BattleRoundManager: Player B wins increased to {playerBWins}");
        }

        OnRoundEnd?.Invoke(winner, currentRound);

        if (playerAWins >= roundsToWin || playerBWins >= roundsToWin)
        {
            string matchWinner = playerAWins > playerBWins ? "player" : "enemy";
            Debug.Log($"BattleRoundManager: Match ended. Overall winner: {matchWinner}");
            OnMatchEnd?.Invoke(matchWinner, playerAWins, playerBWins);
        }
        else
        {
            currentRound++;
            Debug.Log($"BattleRoundManager: Advancing to round {currentRound}");
            PrepareNextRound();
        }
    }

    private void PrepareNextRound()
    {
        Debug.Log("BattleRoundManager: Preparing next round");
        
        // Clear existing units
        PlacementManager placementManager = FindFirstObjectByType<PlacementManager>();
        if (placementManager != null)
        {
            placementManager.ClearUnits();
            Debug.Log("BattleRoundManager: Units cleared");
        }
        else
        {
            Debug.LogError("BattleRoundManager: PlacementManager not found!");
        }

        // Reset game state for next round
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PrepareNextRound();
            Debug.Log("BattleRoundManager: Game state reset for next round");
        }
        else
        {
            Debug.LogError("BattleRoundManager: GameManager not found!");
        }
    }

    public int GetCurrentRound() => currentRound;
    public int GetPlayerAWins() => playerAWins;
    public int GetPlayerBWins() => playerBWins;
    public int GetRoundsToWin() => roundsToWin;
}