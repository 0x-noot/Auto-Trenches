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
    private bool hasShownMatchResults = false;

    private void Awake()
    {
        panelCanvasGroup = resultsPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = resultsPanel.AddComponent<CanvasGroup>();

        HidePanel();
        ValidateReferences();
        
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
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
        // Subscribe to events
        if (BattleRoundManager.Instance != null)
        {
            // Unsubscribe first to prevent duplicates
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd -= HandleMatchEnd;
            
            BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd += HandleMatchEnd;
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
        
        // Start match end detection
        StartCoroutine(CheckMatchEndCondition());
    }
    
    private IEnumerator CheckMatchEndCondition()
    {
        yield return new WaitForSeconds(2f); // Initial delay
        
        while (true)
        {
            if (BattleRoundManager.Instance != null)
            {
                float playerAHP = BattleRoundManager.Instance.GetPlayerAHP();
                float playerBHP = BattleRoundManager.Instance.GetPlayerBHP();
                
                // If either player has 0 HP, it's match end
                if ((playerAHP <= 0 || playerBHP <= 0) && !isMatchEnd && !hasShownMatchResults)
                {
                    // Determine winner based on HP
                    bool localPlayerWon;
                    if (PhotonNetwork.IsMasterClient)
                    {
                        localPlayerWon = playerAHP > playerBHP;
                    }
                    else
                    {
                        localPlayerWon = playerBHP > playerAHP;
                    }
                    
                    string resultText = localPlayerWon ? "Victory!" : "Defeat!";
                    
                    // Force match end through RPC
                    photonView.RPC("RPCEmergencyMatchEnd", RpcTarget.All, resultText);
                }
                
                // If match has ended but results aren't showing
                if (isMatchEnd && !hasShownMatchResults && !resultsPanel.activeSelf)
                {
                    string resultText = wonMatch ? "Victory!" : "Defeat!";
                    ForceShowMatchResults(resultText);
                }
            }
            
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnDestroy()
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
            BattleRoundManager.Instance.OnMatchEnd -= HandleMatchEnd;
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
        
        StopAllCoroutines();
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
        // Skip if match has ended
        if (isMatchEnd) return;

        StopAllCoroutines();
        StartCoroutine(ShowRoundResults(resultText, originalSurvivingUnits));
        StartCoroutine(CheckMatchEndCondition());
    }
    
    private void HandleGameOver(string winner)
    {
        // Skip if already handled
        if (isMatchEnd) return;
        
        // Mark match as ended
        isMatchEnd = true;
        
        // Convert winner to local perspective
        bool localPlayerWon;
        if (PhotonNetwork.IsMasterClient)
        {
            localPlayerWon = winner == "player";
        }
        else
        {
            localPlayerWon = winner == "enemy";
        }
        
        wonMatch = localPlayerWon;
        string resultText = localPlayerWon ? "Victory!" : "Defeat!";
        
        // Ensure both clients show results
        photonView.RPC("RPCShowMatchEnd", RpcTarget.All, resultText);
    }

    private void HandleMatchEnd(string resultText)
    {
        // Skip if already handled
        if (isMatchEnd) return;
        
        // Mark match as ended
        isMatchEnd = true;
        wonMatch = resultText == "Victory!";
        
        // Ensure both clients show results
        photonView.RPC("RPCShowMatchEnd", RpcTarget.All, resultText);
    }
    
    [PunRPC]
    private void RPCEmergencyMatchEnd(string resultText)
    {
        // Mark match as ended unconditionally
        isMatchEnd = true;
        wonMatch = resultText == "Victory!";
        
        // Handle ranked mode ELO
        HandleRankedMode();
        
        // Force show results
        ForceShowMatchResults(resultText);
    }
    
    [PunRPC]
    private void RPCShowMatchEnd(string resultText)
    {
        // Mark match as ended unconditionally
        isMatchEnd = true;
        wonMatch = resultText == "Victory!";
        
        // Handle ranked mode ELO
        HandleRankedMode();
        
        // Stop any existing coroutines
        StopAllCoroutines();
        
        // Force show match results
        ForceShowMatchResults(resultText);
        
        // Start failsafe checker
        StartCoroutine(CheckMatchEndCondition());
    }
    
    private void HandleRankedMode()
    {
        if (GameModeManager.Instance != null && 
            GameModeManager.Instance.CurrentMode == GameMode.Ranked)
        {
            pendingScoreSubmission = true;
            CalculatePendingEloChange();
        }
        else
        {
            pendingScoreSubmission = false;
        }
    }
    
    private void ForceShowMatchResults(string resultText)
    {
        // Forcefully cancel any fades and make panel fully visible
        StopAllCoroutines();
        
        // Ensure panel components are set up
        resultsPanel.SetActive(true);
        
        // Configure canvas group - ensure full visibility
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        
        // Configure texts
        winnerText.text = $"Match {resultText}";
        winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        battleStatsText.text = GenerateMatchStats();
        
        // Show continue button
        continueButton.gameObject.SetActive(true);
        continueButton.interactable = true;
        
        if (continueButtonText != null)
        {
            continueButtonText.text = pendingScoreSubmission ? 
                "Submit Score & Continue" : "Return to Menu";
        }
        
        // Mark that we've shown match results
        hasShownMatchResults = true;
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
        // Ensure we're not showing match end
        if (isMatchEnd)
        {
            string matchResult = wonMatch ? "Victory!" : "Defeat!";
            ForceShowMatchResults(matchResult);
            yield break;
        }
        
        resultsPanel.SetActive(true);
        
        winnerText.text = $"Round {BattleRoundManager.Instance.GetCurrentRound()}: {resultText}";
        winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        
        battleStatsText.text = GenerateRoundStats(resultText, survivingUnits);
        
        continueButton.gameObject.SetActive(false);
        
        yield return StartCoroutine(FadeInPanel());
        
        // Check for match end again
        if (isMatchEnd)
        {
            string matchResult = wonMatch ? "Victory!" : "Defeat!";
            ForceShowMatchResults(matchResult);
            yield break;
        }
        
        yield return new WaitForSeconds(transitionDelay);
        
        // Check for match end again
        if (isMatchEnd)
        {
            string matchResult = wonMatch ? "Victory!" : "Defeat!";
            ForceShowMatchResults(matchResult);
            yield break;
        }
        
        // Continue with round end
        yield return StartCoroutine(FadeOutPanel());
        
        resultsPanel.SetActive(false);
        
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.StartNewRound();
        }
    }

    private void OnContinueClicked()
    {
        if (isTransitioning) return;
        
        continueButton.interactable = false;
        isTransitioning = true;
        
        if (pendingScoreSubmission && !transactionSubmitted)
        {
            SubmitScoreAndReturnAsync();
        }
        else
        {
            SetReturnToMenuPrefs();
            
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
            else
            {
                StartCoroutine(TransitionToMainMenu());
            }
        }
    }

    private void SetReturnToMenuPrefs()
    {
        PlayerPrefs.SetInt("ShowMainMenu", 1);
        PlayerPrefs.SetInt("KeepWalletConnected", 1);
        PlayerPrefs.SetInt("ReturningFromGame", 1);
        PlayerPrefs.Save();
    }

    private async void SubmitScoreAndReturnAsync()
    {
        if (continueButtonText != null)
        {
            continueButtonText.text = "Submitting Score...";
        }
        
        bool success = await SubmitScore();
        transactionSubmitted = true;
        transactionSuccess = success;
        
        if (continueButtonText != null)
        {
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
        }
        
        SetReturnToMenuPrefs();
        
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            StartCoroutine(TransitionToMainMenu());
        }
    }

    public override void OnLeftRoom()
    {
        StartCoroutine(TransitionToMainMenu());
    }

    private async System.Threading.Tasks.Task<bool> SubmitScore()
    {
        try
        {
            var soarManager = FindFirstObjectByType<SoarManager>();
            if (soarManager != null && newElo > 0)
            {
                if (WalletManager.Instance == null || !WalletManager.Instance.IsConnected)
                {
                    return false;
                }
                
                bool success = await soarManager.SubmitScoreToLeaderboard((ulong)newElo);
                if (success && ProfileManager.Instance != null)
                {
                    await ProfileManager.Instance.LoadProfileData();
                }
                return success;
            }
            return false;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private IEnumerator FadeInPanel()
    {
        if (panelCanvasGroup == null) yield break;
        
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        float elapsedTime = 0f;
        float duration = panelFadeInDuration;
        
        while (elapsedTime < duration)
        {
            // Check if match has ended during fade
            if (isMatchEnd)
            {
                panelCanvasGroup.alpha = 1f;
                yield break;
            }
            
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / duration);
            yield return null;
        }
        
        panelCanvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutPanel()
    {
        if (panelCanvasGroup == null) yield break;
        
        // Don't fade out if match has ended
        if (isMatchEnd)
        {
            panelCanvasGroup.alpha = 1f;
            yield break;
        }
        
        float elapsedTime = 0f;
        float duration = panelFadeInDuration;
        
        float startAlpha = panelCanvasGroup.alpha;
        
        while (elapsedTime < duration)
        {
            // Check if match has ended during fade
            if (isMatchEnd)
            {
                panelCanvasGroup.alpha = 1f;
                yield break;
            }
            
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / duration);
            yield return null;
        }
        
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator TransitionToMainMenu()
    {
        // Skip fade out for faster transition
        yield return null; // Wait one frame
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
        if (BattleRoundManager.Instance == null)
        {
            return "Match Complete!\nError: Could not retrieve match statistics.";
        }
        
        string stats = $"Match Complete!\n" +
               $"Final HP:\n" +
               $"Player A: {BattleRoundManager.Instance.GetPlayerAHP():F0}\n" +
               $"Player B: {BattleRoundManager.Instance.GetPlayerBHP():F0}\n" +
               $"Total Rounds: {BattleRoundManager.Instance.GetCurrentRound()}";
               
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