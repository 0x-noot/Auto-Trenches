using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
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
    [SerializeField] private string mainMenuScene = "MainMenu";
    
    private CanvasGroup panelCanvasGroup;
    private bool isTransitioning = false;
    private bool pendingScoreSubmission = false;
    private bool wonMatch = false;
    private int pendingEloChange = 0;
    private int newElo = 0;
    private bool transactionSubmitted = false;
    private bool hasShownMatchResults = false;
    private bool isShowingRoundResults = false;

    private void Awake()
    {
        panelCanvasGroup = resultsPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = resultsPanel.AddComponent<CanvasGroup>();

        HidePanel();
        
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    private void Start()
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
            BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
        
        InvokeRepeating("CheckPlayerHP", 2f, 0.25f);
    }

    private void OnDestroy()
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
        
        CancelInvoke();
        StopAllCoroutines();
    }

    private void CheckPlayerHP()
    {
        if (hasShownMatchResults || isShowingRoundResults) return;
        
        if (BattleRoundManager.Instance != null)
        {
            float playerAHP = BattleRoundManager.Instance.GetPlayerAHP();
            float playerBHP = BattleRoundManager.Instance.GetPlayerBHP();
            
            if (playerAHP <= 0 || playerBHP <= 0)
            {
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
                
                if (PhotonNetwork.IsMasterClient)
                {
                    string winner = playerAHP > playerBHP ? "player" : "enemy";
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.EndBattle(winner);
                    }
                }
                
                ShowMatchResults(resultText);
            }
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
    
    private void HandleRoundEnd(string resultText, int survivingUnits)
    {
        if (hasShownMatchResults) return;
        
        if (BattleRoundManager.Instance != null)
        {
            float playerAHP = BattleRoundManager.Instance.GetPlayerAHP();
            float playerBHP = BattleRoundManager.Instance.GetPlayerBHP();
            
            if (playerAHP <= 0 || playerBHP <= 0)
            {
                return;
            }
        }
        
        StopAllCoroutines();
        StartCoroutine(ShowRoundResults(resultText, survivingUnits));
    }
    
    private void HandleGameOver(string winner)
    {
        if (hasShownMatchResults) return;
        
        bool localPlayerWon;
        if (PhotonNetwork.IsMasterClient)
        {
            localPlayerWon = winner == "player";
        }
        else
        {
            localPlayerWon = winner == "enemy";
        }
        
        string resultText = localPlayerWon ? "Victory!" : "Defeat!";
        ShowMatchResults(resultText);
    }

    [PunRPC]
    public void RPCShowMatchResults(string resultText)
    {
        ShowMatchResults(resultText);
    }
    
    [PunRPC]
    public void RPCShowRoundResults(string resultText, int survivingUnits)
    {
        if (hasShownMatchResults) return;
        
        StartCoroutine(ShowRoundResults(resultText, survivingUnits));
    }
    
    private void ShowMatchResults(string resultText)
    {
        if (hasShownMatchResults) return;
        
        if (isShowingRoundResults)
        {
            StopAllCoroutines();
            isShowingRoundResults = false;
        }
        
        StopAllCoroutines();
        CancelInvoke();
        
        hasShownMatchResults = true;
        wonMatch = resultText == "Victory!";
        
        bool isRankedMode = false;
        
        if (GameModeManager.Instance != null) 
        {
            isRankedMode = GameModeManager.Instance.CurrentMode == GameMode.Ranked;
            Debug.Log($"[BattleResultsUI] Game mode from GameModeManager: {GameModeManager.Instance.CurrentMode}, isRankedMode: {isRankedMode}");
        }
        else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameMode", out object gameModeObj))
            {
                string gameModeStr = gameModeObj.ToString();
                isRankedMode = gameModeStr == "Ranked";
                Debug.Log($"[BattleResultsUI] Game mode from room properties: {gameModeStr}, isRankedMode: {isRankedMode}");
            }
        }
        
        pendingScoreSubmission = isRankedMode;
        Debug.Log($"[BattleResultsUI] Final pendingScoreSubmission: {pendingScoreSubmission}");
        
        if (pendingScoreSubmission)
        {
            CalculatePendingEloChange();
        }
        
        resultsPanel.SetActive(true);
        
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        
        winnerText.text = $"Match {resultText}";
        winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        battleStatsText.text = GenerateMatchStats();
        
        continueButton.gameObject.SetActive(true);
        continueButton.interactable = true;
        
        if (continueButtonText != null)
        {
            continueButtonText.text = pendingScoreSubmission ? 
                "Submit Score & Continue" : "Return to Menu";
        }
        
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("RPCShowMatchResults", RpcTarget.Others, resultText == "Victory!" ? "Defeat!" : "Victory!");
        }
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
        isShowingRoundResults = true;
        
        if (hasShownMatchResults)
        {
            isShowingRoundResults = false;
            yield break;
        }
        
        resultsPanel.SetActive(true);
        
        if (BattleRoundManager.Instance != null)
        {
            int displayRound = BattleRoundManager.Instance.GetCurrentRound();
            if (PhotonNetwork.IsMasterClient)
            {
                displayRound = Mathf.Max(1, displayRound - 1);
            }
            
            winnerText.text = $"Round {displayRound}: {resultText}";
            winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        }
        else
        {
            winnerText.text = $"Round: {resultText}";
            winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        }
        
        battleStatsText.text = GenerateRoundStats(resultText, survivingUnits);
        
        continueButton.gameObject.SetActive(false);
        
        yield return StartCoroutine(FadeInPanel());
        
        yield return new WaitForSeconds(3f);
        
        if (hasShownMatchResults)
        {
            isShowingRoundResults = false;
            yield break;
        }
        
        yield return StartCoroutine(FadeOutPanel());
        
        resultsPanel.SetActive(false);
        
        isShowingRoundResults = false;
        
        if (!hasShownMatchResults && BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.StartNewRound();
        }
    }

    private void OnContinueClicked()
    {
        if (isTransitioning) return;
        
        Debug.Log($"[BattleResultsUI] OnContinueClicked - pendingScoreSubmission: {pendingScoreSubmission}, transactionSubmitted: {transactionSubmitted}");
        
        continueButton.interactable = false;
        isTransitioning = true;
        
        if (pendingScoreSubmission && !transactionSubmitted)
        {
            Debug.Log("[BattleResultsUI] Starting score submission process...");
            SubmitScoreAndReturnAsync();
        }
        else
        {
            Debug.Log("[BattleResultsUI] No score submission needed, returning to menu");
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
        Debug.Log("[BattleResultsUI] SubmitScoreAndReturnAsync started");
        
        if (continueButtonText != null)
        {
            continueButtonText.text = "Submitting Score...";
        }
        
        bool success = false;
        try
        {
            success = await SubmitScore();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BattleResultsUI] Exception in SubmitScoreAndReturnAsync: {ex.Message}");
            success = false;
        }
        
        transactionSubmitted = true;
        
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
            Debug.Log("[BattleResultsUI] Starting score submission");
            
            WalletManager walletManager = WalletManager.Instance;
            if (walletManager == null)
            {
                Debug.LogWarning("[BattleResultsUI] WalletManager.Instance is null, trying to find it...");
                walletManager = FindObjectOfType<WalletManager>();
                
                if (walletManager == null)
                {
                    Debug.LogError("[BattleResultsUI] Could not find WalletManager anywhere!");
                    
                    GameObject[] dontDestroyObjects = GetDontDestroyOnLoadObjects();
                    foreach (var obj in dontDestroyObjects)
                    {
                        walletManager = obj.GetComponentInChildren<WalletManager>();
                        if (walletManager != null)
                        {
                            Debug.Log("[BattleResultsUI] Found WalletManager in DontDestroyOnLoad!");
                            break;
                        }
                    }
                    
                    if (walletManager == null)
                    {
                        Debug.LogError("[BattleResultsUI] WalletManager not found even in DontDestroyOnLoad!");
                        return false;
                    }
                }
            }
            
            bool isConnected = await walletManager.EnsureConnected();
            if (!isConnected)
            {
                Debug.LogError("[BattleResultsUI] Failed to ensure wallet connection!");
                
                Debug.Log("[BattleResultsUI] Attempting to reconnect wallet...");
                bool reconnected = await walletManager.ConnectWallet();
                if (!reconnected)
                {
                    Debug.LogError("[BattleResultsUI] Failed to reconnect wallet!");
                    return false;
                }
            }
            
            if (!walletManager.IsConnected)
            {
                Debug.LogError("[BattleResultsUI] Wallet still not connected after reconnect attempt!");
                return false;
            }
            
            SoarManager soarManager = SoarManager.Instance;
            if (soarManager == null)
            {
                Debug.LogWarning("[BattleResultsUI] SoarManager.Instance is null, trying to find it...");
                soarManager = FindObjectOfType<SoarManager>();
                
                if (soarManager == null)
                {
                    GameObject[] dontDestroyObjects = GetDontDestroyOnLoadObjects();
                    foreach (var obj in dontDestroyObjects)
                    {
                        soarManager = obj.GetComponentInChildren<SoarManager>();
                        if (soarManager != null)
                        {
                            Debug.Log("[BattleResultsUI] Found SoarManager in DontDestroyOnLoad!");
                            break;
                        }
                    }
                    
                    if (soarManager == null)
                    {
                        Debug.LogError("[BattleResultsUI] SoarManager not found!");
                        return false;
                    }
                }
            }
            
            ulong scoreToSubmit = 1200;
            Debug.Log($"[BattleResultsUI] Submitting score: {scoreToSubmit}");
            
            bool success = await soarManager.SubmitScoreToLeaderboard(scoreToSubmit);
            
            if (success)
            {
                Debug.Log("[BattleResultsUI] Score submission successful!");
                if (ProfileManager.Instance != null)
                {
                    await ProfileManager.Instance.LoadProfileData();
                }
            }
            else
            {
                Debug.LogError("[BattleResultsUI] Score submission failed!");
            }
            
            return success;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BattleResultsUI] Exception during score submission: {ex.Message}");
            Debug.LogError($"[BattleResultsUI] Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private GameObject[] GetDontDestroyOnLoadObjects()
    {
        GameObject temp = null;
        try
        {
            temp = new GameObject();
            DontDestroyOnLoad(temp);
            UnityEngine.SceneManagement.Scene dontDestroyOnLoad = temp.scene;
            DestroyImmediate(temp);
            temp = null;
            
            return dontDestroyOnLoad.GetRootGameObjects();
        }
        finally
        {
            if (temp != null)
                DestroyImmediate(temp);
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
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / duration);
            yield return null;
        }
        
        panelCanvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutPanel()
    {
        if (panelCanvasGroup == null) yield break;
        
        float elapsedTime = 0f;
        float duration = panelFadeInDuration;
        
        float startAlpha = panelCanvasGroup.alpha;
        
        while (elapsedTime < duration)
        {
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
        yield return null;
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