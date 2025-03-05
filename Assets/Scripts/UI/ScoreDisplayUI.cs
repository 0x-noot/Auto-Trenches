// Fix for ScoreDisplayUI.cs - this appears to be the source of errors
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
    private bool isInitialized = false;

    private void Start()
    {
        Debug.Log("ScoreDisplayUI: Start method called");
        
        // Add guard to prevent this from executing during scene transitions
        if (!gameObject.scene.isLoaded || !gameObject.activeInHierarchy)
        {
            Debug.Log("ScoreDisplayUI: Scene not fully loaded or object inactive, delaying initialization");
            return;
        }

        // Wrap initialization in try/catch to prevent WebGL crashes
        try
        {
            InitializeDisplay();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error initializing ScoreDisplayUI: {ex.Message}");
        }
    }
    
    private void OnEnable()
    {
        // Try initialization again if it failed during Start
        if (!isInitialized && gameObject.activeInHierarchy)
        {
            StartCoroutine(DelayedInitialization());
        }
    }
    
    private System.Collections.IEnumerator DelayedInitialization()
    {
        // Wait for scene to be fully loaded
        yield return new WaitForSeconds(0.5f);
        
        if (!isInitialized && BattleRoundManager.Instance != null)
        {
            try
            {
                InitializeDisplay();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in delayed initialization: {ex.Message}");
            }
        }
    }
    
    private void InitializeDisplay()
    {
        if (BattleRoundManager.Instance != null)
        {
            // Set colors based on whether this client is Player A or B
            bool isPlayerA = PhotonNetwork.IsMasterClient;
            
            // Safety check on UI references
            if (playerAHealthUI != null && playerBHealthUI != null)
            {
                playerAHealthUI.SetPlayerColor(isPlayerA);
                playerBHealthUI.SetPlayerColor(!isPlayerA);
            }
            
            // Safely get HP objects
            SafeGetHPReferences();
            
            UpdateDisplay();
            
            // Subscribe to events AFTER initialization
            if (!isInitialized)
            {
                if (BattleRoundManager.Instance != null)
                {
                    BattleRoundManager.Instance.OnRoundStart += HandleRoundStart;
                    BattleRoundManager.Instance.OnRoundEnd += HandleRoundEnd;
                }
                
                isInitialized = true;
            }
        }
    }
    
    private void SafeGetHPReferences()
    {
        // Get references to specific PlayerHP components
        if (playerAHPObject != null)
        {
            playerAHP = playerAHPObject.GetComponent<PlayerHP>();
            if (playerAHP != null)
            {
                playerAHP.OnHPChanged += UpdatePlayerAHP;
                UpdatePlayerAHP(); // Initial update
            }
        }
        
        if (playerBHPObject != null)
        {
            playerBHP = playerBHPObject.GetComponent<PlayerHP>();
            if (playerBHP != null)
            {
                playerBHP.OnHPChanged += UpdatePlayerBHP;
                UpdatePlayerBHP(); // Initial update
            }
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
    
    // Add a safety method to ensure we don't call BattleRoundManager while it's not ready
    private bool IsRoundManagerReady()
    {
        return BattleRoundManager.Instance != null &&
               BattleRoundManager.Instance.gameObject != null &&
               BattleRoundManager.Instance.gameObject.activeInHierarchy;
    }

    private void UpdatePlayerAHP()
    {
        if (IsRoundManagerReady() && playerAHealthUI != null)
        {
            try {
                float playerAHP = BattleRoundManager.Instance.GetPlayerAHP();
                playerAHealthUI.SetHP(playerAHP, 100f);
            }
            catch (System.Exception ex) {
                Debug.LogError($"Error updating Player A HP: {ex.Message}");
            }
        }
    }

    private void UpdatePlayerBHP()
    {
        if (IsRoundManagerReady() && playerBHealthUI != null)
        {
            try {
                float playerBHP = BattleRoundManager.Instance.GetPlayerBHP();
                playerBHealthUI.SetHP(playerBHP, 100f);
            }
            catch (System.Exception ex) {
                Debug.LogError($"Error updating Player B HP: {ex.Message}");
            }
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
        if (!IsRoundManagerReady()) return;

        try {
            int currentRound = BattleRoundManager.Instance.GetCurrentRound();
            float playerAHPValue = BattleRoundManager.Instance.GetPlayerAHP();
            float playerBHPValue = BattleRoundManager.Instance.GetPlayerBHP();

            // Show round number (if text component exists)
            if (currentRoundText != null)
                currentRoundText.text = $"Round {currentRound}";
            
            // Update HP displays safely
            if (playerAHealthUI != null)
                playerAHealthUI.SetHP(playerAHPValue, 100f);
                
            if (playerBHealthUI != null)
                playerBHealthUI.SetHP(playerBHPValue, 100f);
        }
        catch (System.Exception ex) {
            Debug.LogError($"Error in UpdateDisplay: {ex.Message}");
        }
    }
}