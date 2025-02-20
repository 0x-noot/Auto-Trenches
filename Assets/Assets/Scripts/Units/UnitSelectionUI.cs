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
    }

    [Header("UI References")]
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private List<UnitButton> unitButtons;
    [SerializeField] private Button startBattleButton;
    [SerializeField] private TextMeshProUGUI unitCountText;
    [SerializeField] private TextMeshProUGUI currentTurnText;
    [SerializeField] private GameObject placementPanel;

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

        // Subscribe to events
        placementManager.OnUnitsChanged += UpdateUnitCountText;

        // Initialize buttons
        InitializeButtons();
        UpdateUnitCountText();

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
            placementManager.OnUnitsChanged -= UpdateUnitCountText;
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
                UpdateAllUI();
                break;
                
            case GameState.BattleStart:
            case GameState.BattleActive:
                placementPanel.SetActive(false);
                break;
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
            }
            else
            {
                Debug.LogError($"Button for unit type {unitButton.type} is null!");
            }
        }
    }

    public void SelectUnitType(UnitType type)
    {
        if (placementManager == null)
        {
            Debug.LogError("PlacementManager is null!");
            return;
        }

        placementManager.SelectUnitType(type);
        
        // Update button visuals
        foreach (var unitButton in unitButtons)
        {
            bool isSelected = unitButton.type == type;
            Image buttonImage = unitButton.button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isSelected ? Color.green : Color.white;
            }
        }
    }

    private void UpdateAllUI()
    {
        UpdateUnitCountText();
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool canPlace = placementManager.CanPlaceUnit();
        foreach (var unitButton in unitButtons)
        {
            if (unitButton.button != null)
            {
                unitButton.button.interactable = canPlace;
            }
        }
    }

    public void UpdateUnitCountText()
    {
        Debug.Log("UpdateUnitCountText called");
        if (unitCountText != null && placementManager != null)
        {
            string currentTeam = placementManager.GetCurrentTeam();
            int currentCount = placementManager.GetTeamUnits(currentTeam).Count;
            int maxUnits = placementManager.GetMaxUnits();
            Debug.Log($"Current count: {currentCount}, Max units: {maxUnits}");
            unitCountText.text = $"Units: {currentCount}/{maxUnits}";

            if (startBattleButton != null)
            {
                // Both players should see the button, but only enable it when units are placed
                startBattleButton.gameObject.SetActive(true);
                startBattleButton.interactable = currentCount > 0 && currentCount <= maxUnits;
                Debug.Log($"Start button interactable set to: {startBattleButton.interactable}");
            }
            else
            {
                Debug.LogError("Start battle button is null!");
            }
        }
        else
        {
            Debug.LogError($"Unit count text is null: {unitCountText == null}, PlacementManager is null: {placementManager == null}");
        }
    }
}