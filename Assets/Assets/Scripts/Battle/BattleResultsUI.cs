using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class BattleResultsUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI battleStatsText;
    
    [Header("Animation Settings")]
    [SerializeField] private float panelFadeInDuration = 1f;
    [SerializeField] private float statsRevealDelay = 0.5f;
    [SerializeField] private float transitionDelay = 3f;
    [SerializeField] private string mainMenuScene = "MainMenu";
    
    private CanvasGroup panelCanvasGroup;
    private bool isTransitioning = false;
    
    private void Awake()
    {
        Debug.Log("BattleResultsUI: Awake called");
        
        // Ensure panel starts hidden
        panelCanvasGroup = resultsPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            Debug.Log("BattleResultsUI: Adding CanvasGroup component");
            panelCanvasGroup = resultsPanel.AddComponent<CanvasGroup>();
        }

        HidePanel();
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (resultsPanel == null) Debug.LogError("BattleResultsUI: resultsPanel is null!");
        if (winnerText == null) Debug.LogError("BattleResultsUI: winnerText is null!");
        if (battleStatsText == null) Debug.LogError("BattleResultsUI: battleStatsText is null!");
    }

    private void Start()
    {
        Debug.Log("BattleResultsUI: Start called");

        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd += HandleMatchEnd;
            Debug.Log("BattleResultsUI: Subscribed to BattleRoundManager events");
        }
        else
        {
            Debug.LogError("BattleResultsUI: BattleRoundManager.Instance is null in Start!");
        }
    }

    private void OnDestroy()
    {
        Debug.Log("BattleResultsUI: OnDestroy called");
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd -= HandleMatchEnd;
        }
    }

    private void HidePanel()
    {
        Debug.Log("BattleResultsUI: Hiding panel");
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }
    }

    private void HandleRoundEnd(string winner, int round)
    {
        StartCoroutine(ShowRoundResults(winner, round));
    }

    private void HandleMatchEnd(string winner, int playerAScore, int playerBScore)
    {
        StartCoroutine(ShowMatchResults(winner, playerAScore, playerBScore));
    }

    private IEnumerator ShowRoundResults(string winner, int round)
    {
        Debug.Log($"BattleResultsUI: Showing round {round} results");
        resultsPanel.SetActive(true);
        
        // Set up round results text
        winnerText.text = $"Round {round}: {(winner == "player" ? "Victory!" : "Defeat!")}";
        winnerText.color = winner == "player" ? Color.green : Color.red;
        
        // Show round statistics
        battleStatsText.text = GenerateRoundStats();
        
        // Fade in panel
        yield return StartCoroutine(FadeInPanel());
        
        yield return new WaitForSeconds(transitionDelay);
        
        // Check if the match should continue
        if (BattleRoundManager.Instance.GetPlayerAWins() < BattleRoundManager.Instance.GetRoundsToWin() && 
            BattleRoundManager.Instance.GetPlayerBWins() < BattleRoundManager.Instance.GetRoundsToWin())
        {
            // Fade out panel
            yield return StartCoroutine(FadeOutPanel());
            resultsPanel.SetActive(false);
            
            // Start next round
            BattleRoundManager.Instance.StartNewRound();
        }
    }

    private IEnumerator ShowMatchResults(string winner, int playerAScore, int playerBScore)
    {
        Debug.Log("BattleResultsUI: Showing match results");
        resultsPanel.SetActive(true);
        
        // Set up match results text
        winnerText.text = $"Match {(winner == "player" ? "Victory!" : "Defeat!")}";
        winnerText.color = winner == "player" ? Color.green : Color.red;
        
        // Show match statistics
        battleStatsText.text = GenerateMatchStats(playerAScore, playerBScore);
        
        // Fade in panel
        yield return StartCoroutine(FadeInPanel());
        
        yield return new WaitForSeconds(transitionDelay);
        
        // Return to main menu
        StartCoroutine(TransitionToMainMenu());
    }

    private IEnumerator FadeInPanel()
    {
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        float elapsedTime = 0f;
        while (elapsedTime < panelFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = elapsedTime / panelFadeInDuration;
            yield return null;
        }
        panelCanvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutPanel()
    {
        float elapsedTime = 0f;
        while (elapsedTime < panelFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = 1 - (elapsedTime / panelFadeInDuration);
            yield return null;
        }
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator TransitionToMainMenu()
    {
        Debug.Log("BattleResultsUI: Starting transition to main menu");
        yield return StartCoroutine(FadeOutPanel());

        // Load the main menu scene
        Debug.Log("BattleResultsUI: Loading main menu scene");
        SceneManager.LoadScene(mainMenuScene);
        isTransitioning = false;
    }

    private string GenerateRoundStats()
    {
        if (GameManager.Instance == null) return "";

        var playerUnits = GameManager.Instance.GetPlayerUnits();
        var enemyUnits = GameManager.Instance.GetEnemyUnits();

        int playerSurvivors = CountAliveCombatants(playerUnits);
        int enemySurvivors = CountAliveCombatants(enemyUnits);

        return $"Round Results:\n" +
               $"Friendly Units Remaining: {playerSurvivors}\n" +
               $"Enemy Units Remaining: {enemySurvivors}";
    }

    private string GenerateMatchStats(int playerAScore, int playerBScore)
    {
        return $"Match Complete!\n" +
               $"Rounds Won: {playerAScore}\n" +
               $"Rounds Lost: {playerBScore}\n" +
               $"Total Rounds: {BattleRoundManager.Instance.GetCurrentRound()}";
    }

    private int CountAliveCombatants(System.Collections.Generic.List<BaseUnit> units)
    {
        int count = 0;
        foreach (var unit in units)
        {
            if (unit != null && unit.GetCurrentState() != UnitState.Dead)
            {
                count++;
            }
        }
        return count;
    }
}