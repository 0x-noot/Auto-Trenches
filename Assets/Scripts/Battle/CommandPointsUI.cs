using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;

public class CommandPointsUI : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI commandPointsText;
    [SerializeField] private Slider commandPointsSlider;
    [SerializeField] private Image fillImage;

    [Header("Visual Settings")]
    [SerializeField] private Color fullColor = Color.green;
    [SerializeField] private Color lowColor = Color.red;

    private string currentTeam;
    private PlacementManager placementManager;

    private void Start()
    {
        // Determine team based on network role
        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        
        // Get reference to PlacementManager
        placementManager = FindFirstObjectByType<PlacementManager>();
        if (placementManager == null)
        {
            Debug.LogError("CommandPointsUI: PlacementManager not found!");
            return;
        }
        
        // Subscribe to events
        placementManager.OnCommandPointsChanged += UpdateCommandPointsUI;
        
        // Initial update
        UpdateCommandPointsDisplay(
            placementManager.GetCommandPoints(currentTeam),
            placementManager.GetMaxCommandPoints(currentTeam)
        );
    }

    private void OnDestroy()
    {
        if (placementManager != null)
        {
            placementManager.OnCommandPointsChanged -= UpdateCommandPointsUI;
        }
    }

    private void UpdateCommandPointsUI(string team, int currentPoints, int maxPoints)
    {
        // Only update if this is our team
        if (team == currentTeam)
        {
            UpdateCommandPointsDisplay(currentPoints, maxPoints);
        }
    }

    private void UpdateCommandPointsDisplay(int currentPoints, int maxPoints)
    {
        // Update text
        if (commandPointsText != null)
        {
            commandPointsText.text = $"Command Points: {currentPoints}/{maxPoints}";
        }
        
        // Update slider
        if (commandPointsSlider != null)
        {
            commandPointsSlider.maxValue = maxPoints;
            commandPointsSlider.value = currentPoints;
            
            // Update color based on available points
            if (fillImage != null)
            {
                float ratio = (float)currentPoints / maxPoints;
                fillImage.color = Color.Lerp(lowColor, fullColor, ratio);
            }
        }
    }
}