using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using Photon.Pun;

public class UnitSelectionUI : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class UnitButton
    {
        public UnitType type;
        public Button button;
        public TextMeshProUGUI costText;  // Added to display unit cost
        public Image buttonImage;  // For visual feedback
    }

    [Header("UI References")]
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private List<UnitButton> unitButtons;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI readyStatusText;
    [SerializeField] private TextMeshProUGUI currentTurnText;
    [SerializeField] private GameObject placementPanel;

    [Header("Visual Settings")]
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color unaffordableColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color cancelColor = Color.red;

    private string currentTeam;
    private UnitType selectedUnitType;

    private void Awake()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    void Start()
    {
        Debug.Log($"UnitSelectionUI Start - IsMasterClient: {PhotonNetwork.IsMasterClient}");
        
        // Get references if not set
        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
            if (placementManager == null)
            {
                Debug.LogError("No PlacementManager found in scene!");
                return;
            }
        }

        // Set current team
        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        selectedUnitType = UnitType.Berserker;  // Default selection (was Fighter)

        // Subscribe to events
        placementManager.OnUnitsChanged += UpdateUI;
        placementManager.OnCommandPointsChanged += HandleCommandPointsChanged;

        // Initialize buttons
        InitializeButtons();
        UpdateUI();

        // Set initial turn text
        if (currentTurnText != null)
        {
            currentTurnText.text = "Placement Phase";
            Debug.Log("Setting initial turn text: Placement Phase");
        }
        else
        {
            Debug.LogError("CurrentTurnText is null!");
        }
    }

    private void OnDestroy()
    {
        if (placementManager != null)
        {
            placementManager.OnUnitsChanged -= UpdateUI;
            placementManager.OnCommandPointsChanged -= HandleCommandPointsChanged;
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        Debug.Log($"UnitSelectionUI: Handling state change to {newState} - IsMasterClient: {PhotonNetwork.IsMasterClient}");
        
        switch (newState)
        {
            case GameState.PlayerAPlacement:
            case GameState.PlayerBPlacement:
                Debug.Log($"Setting placementPanel active: {placementPanel != null}");
                placementPanel.SetActive(true);
                UpdateUI();
                break;
                
            case GameState.BattleStart:
            case GameState.BattleActive:
                placementPanel.SetActive(false);
                break;
        }
    }

    private void HandleCommandPointsChanged(string team, int points, int maxPoints)
    {
        if (team == currentTeam)
        {
            UpdateButtonAffordability();
        }
    }

    private void InitializeButtons()
    {
        Debug.Log("Initializing unit selection buttons");
        foreach (var unitButton in unitButtons)
        {
            if (unitButton.button != null)
            {
                UnitType type = unitButton.type;
                unitButton.button.onClick.RemoveAllListeners();
                unitButton.button.onClick.AddListener(() => SelectUnitType(type));
                
                // Display unit cost if costText is assigned
                if (unitButton.costText != null && placementManager != null)
                {
                    int cost = placementManager.GetUnitCost(unitButton.type);
                    unitButton.costText.text = cost.ToString();
                }
            }
            else
            {
                Debug.LogError($"Button for unit type {unitButton.type} is null!");
            }
        }
        
        // Set up ready button
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            
            // Set initial text
            if (readyButtonText != null)
            {
                readyButtonText.text = "Ready";
            }
        }
        
        // Initially select the first unit type
        if (unitButtons.Count > 0)
        {
            SelectUnitType(unitButtons[0].type);
        }
    }

    private void OnReadyButtonClicked()
    {
        if (placementManager == null) return;
        
        bool currentReadyState = placementManager.IsLocalPlayerReady();
        Debug.Log($"Ready button clicked. Current state: {currentReadyState}");
        
        // Toggle the ready state
        placementManager.SetTeamReady(currentTeam, !currentReadyState);
        
        // Force update of all UI
        UpdateUI();
    }

    private void UpdateReadyButtonState(bool isReady)
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Cancel" : "Ready";
        }
        
        // Change button color based on state
        if (readyButton != null)
        {
            ColorBlock colors = readyButton.colors;
            colors.normalColor = isReady ? cancelColor : readyColor;
            readyButton.colors = colors;
        }
    }

    public void SelectUnitType(UnitType type)
    {
        selectedUnitType = type;
        if (placementManager != null)
        {
            placementManager.SelectUnitType(type);
        }
        
        // Update button visuals
        foreach (var unitButton in unitButtons)
        {
            bool isSelected = unitButton.type == type;
            if (unitButton.buttonImage != null)
            {
                unitButton.buttonImage.color = isSelected ? selectedColor : 
                    (placementManager.CanPlaceUnit(currentTeam, unitButton.type) ? affordableColor : unaffordableColor);
            }
        }
    }

    private void UpdateUI()
    {
        UpdateButtonAffordability();
        UpdateReadyButtonState();
        UpdateReadyStatus();
    }

    private void UpdateButtonAffordability()
    {
        if (placementManager == null) return;
        
        // Update each button's visual state based on affordability
        foreach (var unitButton in unitButtons)
        {
            bool isSelected = unitButton.type == selectedUnitType;
            bool canAfford = placementManager.CanPlaceUnit(currentTeam, unitButton.type);
            
            // Update button interactability
            if (unitButton.button != null)
            {
                unitButton.button.interactable = canAfford;
            }
            
            // Update button visual
            if (unitButton.buttonImage != null)
            {
                unitButton.buttonImage.color = isSelected ? selectedColor : 
                    (canAfford ? affordableColor : unaffordableColor);
            }
            
            // Update cost text color
            if (unitButton.costText != null)
            {
                unitButton.costText.color = canAfford ? affordableColor : unaffordableColor;
            }
        }
    }

    private void UpdateReadyButtonState()
    {
        if (readyButton != null && placementManager != null)
        {
            // Only enable the ready button if there's at least one unit placed
            int unitCount = placementManager.GetTeamUnits(currentTeam).Count;
            readyButton.interactable = unitCount > 0;
            
            // Update button state based on current readiness
            bool isReady = placementManager.IsLocalPlayerReady();
            UpdateReadyButtonState(isReady);
        }
    }

    private void UpdateReadyStatus()
    {
        if (readyStatusText != null && placementManager != null)
        {
            // Debug logs to help troubleshoot
            Debug.Log($"UpdateReadyStatus: Local team: {currentTeam}, IsLocalReady: {placementManager.IsLocalPlayerReady()}");
            Debug.Log($"TeamA ready: {placementManager.IsTeamReady("TeamA")}, TeamB ready: {placementManager.IsTeamReady("TeamB")}");
            
            // Get local player's team
            string localTeam = currentTeam; // This is already set to the local player's team
            
            // Get the opponent's team
            string opponentTeam = localTeam == "TeamA" ? "TeamB" : "TeamA";
            
            // Check if local player is ready
            bool isLocalReady = placementManager.IsLocalPlayerReady();
            
            // Check if opponent's team is ready
            bool isOpponentReady = placementManager.IsTeamReady(opponentTeam);
            
            // Determine status text
            if (isLocalReady && isOpponentReady)
            {
                readyStatusText.text = "All players ready! Starting battle...";
            }
            else if (isLocalReady)
            {
                readyStatusText.text = "You are ready. Waiting for opponent...";
            }
            else if (isOpponentReady)
            {
                readyStatusText.text = "Opponent is ready. Waiting for you...";
            }
            else
            {
                readyStatusText.text = "Waiting for players to ready up...";
            }
            
            Debug.Log($"Ready status set to: {readyStatusText.text}");
        }
    }
}