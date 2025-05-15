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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
        
        InvokeRepeating("CheckPlayerHP", 2f, 0.25f);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
        
        CancelInvoke();
        StopAllCoroutines();
    }

    private void CheckPlayerHP()
    {
        if (hasShownMatchResults) return;
        
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
                
                // If we're MasterClient, inform GameManager to properly end the battle
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
    
    private void ShowMatchResults(string resultText)
    {
        if (hasShownMatchResults) return;
        
        StopAllCoroutines();
        CancelInvoke();
        
        hasShownMatchResults = true;
        wonMatch = resultText == "Victory!";
        
        if (GameModeManager.Instance != null && 
            GameModeManager.Instance.CurrentMode == GameMode.Ranked)
        {
            pendingScoreSubmission = true;
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
        
        // Make sure the other player also sees their result
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

    private IEnumerator TransitionToMainMenu()
    {
        yield return null;
        SceneManager.LoadScene(mainMenuScene);
        isTransitioning = false;
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