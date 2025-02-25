using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Photon.Pun;

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
        panelCanvasGroup = resultsPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = resultsPanel.AddComponent<CanvasGroup>();

        HidePanel();
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (resultsPanel == null) Debug.LogError("resultsPanel is null!");
        if (winnerText == null) Debug.LogError("winnerText is null!");
        if (battleStatsText == null) Debug.LogError("battleStatsText is null!");
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

        // Since resultText is now directly "Victory!" or "Defeat!" from BattleRoundManager
        // we can use it directly to determine if the local player won
        bool isVictory = resultText == "Victory!";
        
        List<BaseUnit> losingTeamUnits = isVictory 
            ? GameManager.Instance.GetEnemyUnits() 
            : GameManager.Instance.GetPlayerUnits();

        int losingTeamUnitCount = losingTeamUnits.Count(u => 
            u != null && 
            u.GetCurrentState() != UnitState.Dead);

        // Use the current losing team unit count or fall back to original surviving units
        int unitsToDisplay = losingTeamUnitCount > 0 ? losingTeamUnitCount : originalSurvivingUnits;

        StartCoroutine(ShowRoundResults(resultText, unitsToDisplay));
    }

    private void HandleMatchEnd(string resultText)
    {
        StartCoroutine(ShowMatchResults(resultText));
    }

    private IEnumerator ShowRoundResults(string resultText, int survivingUnits)
    {
        resultsPanel.SetActive(true);
        
        winnerText.text = $"Round {BattleRoundManager.Instance.GetCurrentRound()}: {resultText}";
        winnerText.color = resultText == "Victory!" ? Color.green : Color.red;
        
        battleStatsText.text = GenerateRoundStats(resultText, survivingUnits);
        
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
        
        yield return StartCoroutine(FadeInPanel());
        yield return new WaitForSeconds(transitionDelay);
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
        yield return StartCoroutine(FadeOutPanel());
        SceneManager.LoadScene(mainMenuScene);
        isTransitioning = false;
    }

    private string GenerateRoundStats(string resultText, int survivingUnits)
    {
        bool isVictory = resultText == "Victory!";
        float damage = 8f + (1.5f * survivingUnits);  // Match the new formula
        string enemyUnits = isVictory ? 
            "Enemy Units Remaining: 0" : 
            $"Enemy Units Remaining: {survivingUnits}";
                
        return $"Round Results:\n" +
            $"{enemyUnits}\n" +
            $"Damage Dealt: {damage:F1}";
    }

    private string GenerateMatchStats()
    {
        return $"Match Complete!\n" +
               $"Final HP:\n" +
               $"Player A: {BattleRoundManager.Instance.GetPlayerAHP():F0}\n" +
               $"Player B: {BattleRoundManager.Instance.GetPlayerBHP():F0}\n" +
               $"Total Rounds: {BattleRoundManager.Instance.GetCurrentRound()}";
    }
}