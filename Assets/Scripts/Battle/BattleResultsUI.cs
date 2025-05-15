using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class BattleResultsUI : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI battleStatsText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI continueButtonText;
    
    [SerializeField] private float panelFadeInDuration = 1f;
    [SerializeField] private float statsRevealDelay = 0.5f;
    [SerializeField] private float transitionDelay = 3f;
    [SerializeField] private string mainMenuScene = "MainMenu";
    
    private CanvasGroup panelCanvasGroup;
    private bool isTransitioning = false;
    private bool pendingScoreSubmission = false;
    private bool wonMatch = false;
    private int pendingEloChange = 0;
    private int newElo = 0;
    private bool transactionSubmitted = false;
    private bool transactionSuccess = false;
    private bool isMatchEnd = false;

    private void Awake()
    {
        panelCanvasGroup = resultsPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = resultsPanel.AddComponent<CanvasGroup>();

        HidePanel();
        ValidateReferences();
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    private void ValidateReferences()
    {
        if (resultsPanel == null) Debug.LogError("resultsPanel is null!");
        if (winnerText == null) Debug.LogError("winnerText is null!");
        if (battleStatsText == null) Debug.LogError("battleStatsText is null!");
        if (continueButton == null) Debug.LogError("continueButton is null!");
        if (continueButtonText == null) Debug.LogError("continueButtonText is null!");
    }

    private void Start()
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd += HandleMatchEnd;
        }
    }

    private void OnDestroy()
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd -= HandleMatchEnd;
        }
    }

    private void HidePanel()
    {
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

    private void HandleRoundEnd(string resultText, int originalSurvivingUnits)
    {
        if (BattleRoundManager.Instance == null || GameManager.Instance == null) return;
        
        // Don't process round end if we're already in match end
        if (isMatchEnd) return;

        bool isVictory = resultText == "Victory!";
        
        List<BaseUnit> losingTeamUnits = isVictory 
            ? GameManager.Instance.GetEnemyUnits() 
            : GameManager.Instance.GetPlayerUnits();

        int losingTeamUnitCount = losingTeamUnits.Count(u => 
            u != null && 
            u.GetCurrentState() != UnitState.Dead);

        int unitsToDisplay = losingTeamUnitCount > 0 ? losingTeamUnitCount : originalSurvivingUnits;

        StartCoroutine(ShowRoundResults(resultText, unitsToDisplay));
    }

    private void HandleMatchEnd(string resultText)
    {
        Debug.Log("BattleResultsUI: Match end received with result: " + resultText);
        isMatchEnd = true;
        
        // Store match result for score submission
        wonMatch = resultText == "Victory!";
        
        // Check if this is a ranked match
        if (GameModeManager.Instance != null && 
            GameModeManager.Instance.CurrentMode == GameMode.Ranked)
        {
            pendingScoreSubmission = true;
            // Calculate ELO change
            CalculatePendingEloChange();
        }
        else
        {
            // Make sure it's false for Practice mode
            pendingScoreSubmission = false;
        }
        
        StartCoroutine(ShowMatchResults(resultText));
    }

    private void CalculatePendingEloChange()
    {
        if (ProfileManager.Instance == null || ELOManager.Instance == null) return;
        
        var currentProfile = ProfileManager.Instance.GetCurrentProfile();
        if (currentProfile == null) return;
        
        int currentELO = currentProfile.eloRating;
        int opponentELO = currentELO;
        
        pendingEloChange = ELOManager.Instance.CalculateELOChange(currentELO, opponentELO, wonMatch);
        newElo = ELOManager.Instance.GetNewELO(currentELO, pendingEloChange);
    }

    private IEnumerator ShowRoundResults(string resultText, int survivingUnits)
    {
        resultsPanel.SetActive(true);
        
        winnerText.text = $"Round {BattleRoundManager.Instance.GetCurrentRound()}: {resultText}";
        winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        
        battleStatsText.text = GenerateRoundStats(resultText, survivingUnits);
        
        // Hide the continue button for round results - we automatically continue
        continueButton.gameObject.SetActive(false);
        
        yield return StartCoroutine(FadeInPanel());
        yield return new WaitForSeconds(transitionDelay);
        yield return StartCoroutine(FadeOutPanel());
        
        resultsPanel.SetActive(false);
        BattleRoundManager.Instance.StartNewRound();
    }

    private IEnumerator ShowMatchResults(string resultText)
    {
        resultsPanel.SetActive(true);
        
        winnerText.text = $"Match {resultText}";
        winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        
        battleStatsText.text = GenerateMatchStats();
        
        // Show continue button for match end
        continueButton.gameObject.SetActive(true);
        
        // Set button text based on whether we need to submit score
        if (pendingScoreSubmission)
        {
            continueButtonText.text = "Submit Score & Continue";
        }
        else
        {
            continueButtonText.text = "Return to Menu";
        }
        continueButton.interactable = true;
        
        yield return StartCoroutine(FadeInPanel());
    }

    private void OnContinueClicked()
    {
        if (isTransitioning) return;
        
        // Disable button to prevent double clicks
        continueButton.interactable = false;
        
        if (pendingScoreSubmission && !transactionSubmitted)
        {
            // Start the async submission process
            SubmitScoreAndReturnAsync();
        }
        else
        {
            StartCoroutine(TransitionToMainMenu());
        }
    }

    // Separate async method to handle the score submission
    private async void SubmitScoreAndReturnAsync()
    {
        isTransitioning = true;
        continueButtonText.text = "Submitting Score...";
        
        // Submit the score
        bool success = await SubmitScore();
        transactionSubmitted = true;
        transactionSuccess = success;
        
        if (success)
        {
            continueButtonText.text = "Score Submitted!";
            await System.Threading.Tasks.Task.Delay(1000);
        }
        else
        {
            continueButtonText.text = "Submission Failed - Returning...";
            await System.Threading.Tasks.Task.Delay(2000);
        }
        
        // Return to main menu using coroutine
        StartCoroutine(TransitionToMainMenu());
    }

    private async System.Threading.Tasks.Task<bool> SubmitScore()
    {
        try
        {
            var soarManager = FindFirstObjectByType<SoarManager>();
            if (soarManager != null && newElo > 0)
            {
                Debug.Log($"Attempting to submit score: {newElo} (change: {pendingEloChange:+#;-#;0})");
                
                if (WalletManager.Instance == null || !WalletManager.Instance.IsConnected)
                {
                    Debug.LogError("Cannot submit score: Wallet not connected");
                    return false;
                }
                
                bool success = await soarManager.SubmitScoreToLeaderboard((ulong)newElo);
                if (success)
                {
                    Debug.Log($"ELO submitted successfully: {newElo} (change: {pendingEloChange:+#;-#;0})");
                    
                    if (ProfileManager.Instance != null)
                    {
                        await ProfileManager.Instance.LoadProfileData();
                    }
                }
                return success;
            }
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error submitting score: {ex.Message}");
            return false;
        }
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
        yield return StartCoroutine(FadeOutPanel());
        
        PlayerPrefs.SetInt("ShowMainMenu", 1);
        PlayerPrefs.Save();
        
        SceneManager.LoadScene(mainMenuScene);
        isTransitioning = false;
    }

    private string GenerateRoundStats(string resultText, int survivingUnits)
    {
        bool isVictory = resultText == "Victory!";
        float damage = 8f + (1.5f * survivingUnits);
        string enemyUnits = isVictory ? 
            "Enemy Units Remaining: 0" : 
            $"Enemy Units Remaining: {survivingUnits}";
                
        return $"Round Results:\n" +
            $"{enemyUnits}\n" +
            $"Damage Dealt: {damage:F1}";
    }

    private string GenerateMatchStats()
    {
        string stats = $"Match Complete!\n" +
               $"Final HP:\n" +
               $"Player A: {BattleRoundManager.Instance.GetPlayerAHP():F0}\n" +
               $"Player B: {BattleRoundManager.Instance.GetPlayerBHP():F0}\n" +
               $"Total Rounds: {BattleRoundManager.Instance.GetCurrentRound()}";
               
        // Add ELO change info for ranked matches
        if (pendingScoreSubmission && pendingEloChange != 0)
        {
            stats += $"\n\nELO Change: {pendingEloChange:+#;-#;0}";
            var currentProfile = ProfileManager.Instance.GetCurrentProfile();
            if (currentProfile != null)
            {
                stats += $"\n{currentProfile.eloRating} → {newElo}";
            }
        }
        
        return stats;
    }
}
