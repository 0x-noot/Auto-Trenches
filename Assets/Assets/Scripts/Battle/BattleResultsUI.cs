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
        
        if (GameManager.Instance != null)
        {
            Debug.Log("BattleResultsUI: Subscribing to GameManager events");
            GameManager.Instance.OnGameOver += ShowBattleResults;
        }
        else
        {
            Debug.LogError("BattleResultsUI: GameManager.Instance is null in Start!");
        }
    }

    private void OnDestroy()
    {
        Debug.Log("BattleResultsUI: OnDestroy called");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= ShowBattleResults;
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

    public void ShowBattleResults(string winner)
    {
        if (isTransitioning) return;
        
        Debug.Log($"BattleResultsUI: ShowBattleResults called with winner: {winner}");
        StartCoroutine(BattleResultsSequence(winner));
    }

    private IEnumerator BattleResultsSequence(string winner)
    {
        isTransitioning = true;
        Debug.Log("BattleResultsUI: Starting battle results sequence");
        
        // Activate panel
        resultsPanel.SetActive(true);
        Debug.Log("BattleResultsUI: Panel activated");

        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        // Fade in panel
        float elapsedTime = 0f;
        while (elapsedTime < panelFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = elapsedTime / panelFadeInDuration;
            yield return null;
        }
        panelCanvasGroup.alpha = 1f;
        Debug.Log("BattleResultsUI: Panel fade complete");

        // Show winner
        winnerText.text = winner == "player" ? "Victory!" : "Defeat!";
        winnerText.color = winner == "player" ? Color.green : Color.red;
        Debug.Log($"BattleResultsUI: Winner text set to {winnerText.text}");

        yield return new WaitForSeconds(statsRevealDelay);

        // Show battle stats
        battleStatsText.text = GenerateBattleStats();
        Debug.Log("BattleResultsUI: Battle stats displayed");
        
        // Wait before transitioning
        yield return new WaitForSeconds(transitionDelay);

        // Start scene transition
        Debug.Log("BattleResultsUI: Starting scene transition");
        StartCoroutine(TransitionToMainMenu());
    }

    private IEnumerator TransitionToMainMenu()
    {
        // Fade out the panel
        float elapsedTime = 0f;
        while (elapsedTime < panelFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            panelCanvasGroup.alpha = 1 - (elapsedTime / panelFadeInDuration);
            yield return null;
        }

        // Load the main menu scene
        Debug.Log("BattleResultsUI: Loading main menu scene");
        SceneManager.LoadScene(mainMenuScene);
        isTransitioning = false;
    }

    private string GenerateBattleStats()
    {
        if (GameManager.Instance == null) return "";

        var playerUnits = GameManager.Instance.GetPlayerUnits();
        var enemyUnits = GameManager.Instance.GetEnemyUnits();

        int playerSurvivors = CountAliveCombatants(playerUnits);
        int enemySurvivors = CountAliveCombatants(enemyUnits);

        return $"Battle Results:\n" +
               $"Friendly Units Remaining: {playerSurvivors}\n" +
               $"Enemy Units Remaining: {enemySurvivors}";
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