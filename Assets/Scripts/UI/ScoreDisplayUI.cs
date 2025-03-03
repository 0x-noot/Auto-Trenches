using UnityEngine;
using TMPro;
using System.Linq;
using Photon.Pun;

public class ScoreDisplayUI : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI currentRoundText;
    [SerializeField] private PlayerHealthUI playerAHealthUI;
    [SerializeField] private PlayerHealthUI playerBHealthUI;
    [SerializeField] private GameObject persistentScorePanel;

    [Header("Player HP References")]
    [SerializeField] private GameObject playerAHPObject;
    [SerializeField] private GameObject playerBHPObject;

    private PlayerHP playerAHP;
    private PlayerHP playerBHP;

    private void Start()
    {
        Debug.Log("ScoreDisplayUI: Start method called");

        if (BattleRoundManager.Instance != null)
        {
            // Set colors based on whether this client is Player A or B
            bool isPlayerA = PhotonNetwork.IsMasterClient;
            playerAHealthUI.SetPlayerColor(isPlayerA);
            playerBHealthUI.SetPlayerColor(!isPlayerA);
            
            // Get references to specific PlayerHP components
            playerAHP = playerAHPObject?.GetComponent<PlayerHP>();
            playerBHP = playerBHPObject?.GetComponent<PlayerHP>();

            Debug.Log($"ScoreDisplayUI: PlayerA HP Reference: {playerAHP != null}");
            Debug.Log($"ScoreDisplayUI: PlayerB HP Reference: {playerBHP != null}");

            // Subscribe to HP change events
            if (playerAHP != null)
            {
                playerAHP.OnHPChanged += UpdatePlayerAHP;
                UpdatePlayerAHP(); // Initial update
            }
            if (playerBHP != null)
            {
                playerBHP.OnHPChanged += UpdatePlayerBHP;
                UpdatePlayerBHP(); // Initial update
            }

            UpdateDisplay();
            BattleRoundManager.Instance.OnRoundStart += HandleRoundStart;
            BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (playerAHP != null)
            playerAHP.OnHPChanged -= UpdatePlayerAHP;
        if (playerBHP != null)
            playerBHP.OnHPChanged -= UpdatePlayerBHP;

        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundStart -= HandleRoundStart;
            BattleRoundManager.Instance.OnRoundEnd -= HandleRoundEnd;
        }
    }

    private void UpdatePlayerAHP()
    {
        if (BattleRoundManager.Instance != null)
        {
            float playerAHP = BattleRoundManager.Instance.GetPlayerAHP();
            Debug.Log($"ScoreDisplayUI: Updating Player A HP: {playerAHP}");
            playerAHealthUI.SetHP(playerAHP, 100f);
        }
    }

    private void UpdatePlayerBHP()
    {
        if (BattleRoundManager.Instance != null)
        {
            float playerBHP = BattleRoundManager.Instance.GetPlayerBHP();
            Debug.Log($"ScoreDisplayUI: Updating Player B HP: {playerBHP}");
            playerBHealthUI.SetHP(playerBHP, 100f);
        }
    }

    private void HandleRoundStart(int round)
    {
        Debug.Log($"ScoreDisplayUI: Round Start - Round {round}");
        UpdateDisplay();
    }

    private void HandleRoundEnd(string winner, int survivingUnits)
    {
        Debug.Log($"ScoreDisplayUI: Round End - Winner: {winner}, Surviving Units: {survivingUnits}");
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (BattleRoundManager.Instance == null) return;

        int currentRound = BattleRoundManager.Instance.GetCurrentRound();
        float playerAHP = BattleRoundManager.Instance.GetPlayerAHP();
        float playerBHP = BattleRoundManager.Instance.GetPlayerBHP();

        Debug.Log($"ScoreDisplayUI: UpdateDisplay - Round: {currentRound}, PlayerA HP: {playerAHP}, PlayerB HP: {playerBHP}");

        // Show round number
        currentRoundText.text = $"Round {currentRound}";
        
        // Update HP displays - note that the display order is the same for both players,
        // but the colors indicate which is the local player
        playerAHealthUI.SetHP(playerAHP, 100f);
        playerBHealthUI.SetHP(playerBHP, 100f);
    }
}