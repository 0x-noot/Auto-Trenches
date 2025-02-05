using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlacementManager placementManager;
    
    [Header("UI Elements")]
    [SerializeField] private Button[] unitButtons;
    [SerializeField] private Button startBattleButton;
    [SerializeField] private TextMeshProUGUI unitCountText;
    [SerializeField] private TextMeshProUGUI currentTurnText;
    [SerializeField] private GameObject placementPanel;
    
    void Awake()
    {
        // Subscribe to GameManager events right away
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    void Start()
    {
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
            currentTurnText.text = "Player A's Turn";
            currentTurnText.color = Color.blue;
            Debug.Log("Setting initial turn text: Player A's Turn");
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
        Debug.Log($"UnitSelectionUI: Handling state change to {newState}");
        
        switch (newState)
        {
            case GameState.PlayerAPlacement:
                placementPanel.SetActive(true);
                if (currentTurnText != null)
                {
                    currentTurnText.text = "Player A's Turn";
                    currentTurnText.color = Color.blue;
                    Debug.Log("Set turn text to: Player A's Turn");
                }
                startBattleButton.gameObject.SetActive(false);
                UpdateUnitCountText();
                break;

            case GameState.PlayerBPlacement:
                placementPanel.SetActive(true);
                if (currentTurnText != null)
                {
                    currentTurnText.text = "Player B's Turn";
                    currentTurnText.color = Color.red;
                    Debug.Log("Set turn text to: Player B's Turn");
                }
                startBattleButton.gameObject.SetActive(true);
                UpdateUnitCountText();
                break;

            case GameState.BattleStart:
            case GameState.BattleActive:
                placementPanel.SetActive(false);
                break;

            case GameState.BattleEnd:
                // Handle battle end if needed
                break;
        }
    }

    private void InitializeButtons()
    {
        if (unitButtons == null || unitButtons.Length == 0)
        {
            Debug.LogError("No buttons assigned in the inspector!");
            return;
        }

        // Set up unit type selection buttons
        for (int i = 0; i < unitButtons.Length; i++)
        {
            if (unitButtons[i] == null)
            {
                Debug.LogError($"Button at index {i} is null!");
                continue;
            }

            UnitType type = (UnitType)i;
            
            // Remove any existing listeners to prevent duplicates
            unitButtons[i].onClick.RemoveAllListeners();
            
            // Add the new click listener
            unitButtons[i].onClick.AddListener(() => 
            {
                SelectUnitType(type);
            });
            
            // Set button text
            TextMeshProUGUI buttonText = unitButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = type.ToString();
            }
            else
            {
                Debug.LogError($"No TextMeshProUGUI found on button {i}");
            }
        }

        // Set up start battle button
        if (startBattleButton != null)
        {
            startBattleButton.onClick.RemoveAllListeners();
            startBattleButton.onClick.AddListener(StartBattle);
            startBattleButton.gameObject.SetActive(false);  // Hide initially
        }
        else
        {
            Debug.LogError("Start Battle button not assigned!");
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
        for (int i = 0; i < unitButtons.Length; i++)
        {
            bool isSelected = (UnitType)i == type;
            Image buttonImage = unitButtons[i].GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isSelected ? Color.green : Color.white;
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
                if (currentTeam == "TeamB")
                {
                    // Only show and enable start button for Team B when they've placed enough units
                    startBattleButton.gameObject.SetActive(true);
                    startBattleButton.interactable = currentCount > 0 && currentCount <= maxUnits;
                }
                else
                {
                    startBattleButton.gameObject.SetActive(false);
                }
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

    private void StartBattle()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartBattle();
        }
        else
        {
            Debug.LogError("GameManager.Instance is null!");
        }
    }

    // Helper method to validate required components
    private void OnValidate()
    {
        Debug.Log("Validating UnitSelectionUI components...");
        if (currentTurnText == null)
            Debug.LogError("CurrentTurnText is not assigned in UnitSelectionUI!");
        if (placementPanel == null)
            Debug.LogError("PlacementPanel is not assigned in UnitSelectionUI!");
    }
}