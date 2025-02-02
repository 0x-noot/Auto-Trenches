using UnityEngine;
using TMPro;

public class ScoreDisplayUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI currentRoundText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject persistentScorePanel;

    private void Start()
    {
        if (BattleRoundManager.Instance != null)
        {
            UpdateDisplay();
            BattleRoundManager.Instance.OnRoundStart += HandleRoundStart;
            BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
        }
    }

    private void OnDestroy()
    {
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundStart -= HandleRoundStart;
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
        }
    }

    private void HandleRoundStart(int round)
    {
        UpdateDisplay();
    }

    private void HandleRoundEnd(string winner, int round)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (BattleRoundManager.Instance == null) return;

        int currentRound = BattleRoundManager.Instance.GetCurrentRound();
        int playerAWins = BattleRoundManager.Instance.GetPlayerAWins();
        int playerBWins = BattleRoundManager.Instance.GetPlayerBWins();
        int roundsToWin = BattleRoundManager.Instance.GetRoundsToWin();

        currentRoundText.text = $"Round {currentRound}";
        scoreText.text = $"Score: {playerAWins} - {playerBWins} (First to {roundsToWin})";
    }
}