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

        // Subscribe to the OnUnitsChanged event
        placementManager.OnUnitsChanged += UpdateUnitCountText;

        // Initialize buttons
        InitializeButtons();
        UpdateUnitCountText();
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
            startBattleButton.interactable = false;
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
            // Update to use TeamA consistently
            int currentCount = placementManager.GetTeamUnits("TeamA").Count;
            int maxUnits = placementManager.GetMaxUnits();
            Debug.Log($"Current count: {currentCount}, Max units: {maxUnits}");
            unitCountText.text = $"Units: {currentCount}/{maxUnits}";

            if (startBattleButton != null)
            {
                // Enable start button when we have at least one unit but not more than max
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
}