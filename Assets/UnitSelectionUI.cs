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
    
    private void Start()
    {
        Debug.Log("UnitSelectionUI: Start method called");

        // Find PlacementManager if not assigned
        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
            Debug.Log($"UnitSelectionUI: Found PlacementManager? {placementManager != null}");
            if (placementManager == null)
            {
                Debug.LogError("No PlacementManager found in scene!");
                return;
            }
        }

        // Check if we have the correct number of buttons
        Debug.Log($"UnitSelectionUI: Number of unit buttons: {unitButtons.Length}");

        // Subscribe to the OnUnitsChanged event
        placementManager.OnUnitsChanged += UpdateUnitCountText;

        // Initialize buttons
        InitializeButtons();
        UpdateUnitCountText();
        
        Debug.Log("UnitSelectionUI: Initialization complete");
    }

    private void OnDestroy()
    {
        if (placementManager != null)
        {
            placementManager.OnUnitsChanged -= UpdateUnitCountText;
        }
    }

    private void InitializeButtons()
    {
        Debug.Log("UnitSelectionUI: Initializing buttons");

        if (unitButtons == null || unitButtons.Length == 0)
        {
            Debug.LogError("UnitSelectionUI: No buttons assigned in the inspector!");
            return;
        }

        // Set up unit type selection buttons
        for (int i = 0; i < unitButtons.Length; i++)
        {
            if (unitButtons[i] == null)
            {
                Debug.LogError($"UnitSelectionUI: Button at index {i} is null!");
                continue;
            }

            int index = i; // Need to capture the index for the lambda
            UnitType type = (UnitType)i;
            
            // Remove any existing listeners to prevent duplicates
            unitButtons[i].onClick.RemoveAllListeners();
            
            // Add the new click listener
            unitButtons[i].onClick.AddListener(() => 
            {
                Debug.Log($"UnitSelectionUI: Button clicked for type {type}");
                SelectUnitType(type);
            });
            
            // Set button text
            TextMeshProUGUI buttonText = unitButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = type.ToString();
                Debug.Log($"UnitSelectionUI: Set button {i} text to {type}");
            }
            else
            {
                Debug.LogError($"UnitSelectionUI: No TextMeshProUGUI found on button {i}");
            }
        }

        // Set up start battle button
        if (startBattleButton != null)
        {
            startBattleButton.onClick.RemoveAllListeners();
            startBattleButton.onClick.AddListener(() => 
            {
                Debug.Log("UnitSelectionUI: Start Battle button clicked");
                StartBattle();
            });
            startBattleButton.interactable = false;
        }
        else
        {
            Debug.LogError("UnitSelectionUI: Start Battle button not assigned!");
        }
    }

    public void SelectUnitType(UnitType type)
    {
        Debug.Log($"UnitSelectionUI: Selecting unit type: {type}");
        
        if (placementManager == null)
        {
            Debug.LogError("UnitSelectionUI: PlacementManager is null in SelectUnitType!");
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
        if (unitCountText != null && placementManager != null)
        {
            int currentCount = placementManager.GetPlacedUnitsCount();
            int maxUnits = placementManager.GetMaxUnits();
            unitCountText.text = $"Units: {currentCount}/{maxUnits}";
            Debug.Log($"UnitSelectionUI: Updated unit count to {currentCount}/{maxUnits}");

            if (startBattleButton != null)
            {
                startBattleButton.interactable = currentCount == maxUnits;
            }
        }
    }

    private void StartBattle()
    {
        if (GameManager.Instance != null)
        {
            Debug.Log("UnitSelectionUI: Starting battle");
            GameManager.Instance.StartBattle();
        }
        else
        {
            Debug.LogError("UnitSelectionUI: GameManager.Instance is null!");
        }
    }
}