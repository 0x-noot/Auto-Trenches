using UnityEngine;

public class ClickPlacementSystem : MonoBehaviour
{
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private ValidPlacementSystem validPlacement;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private UpgradeUI upgradeUI; // Add this reference

    private void Update()
    {
        // Check if upgrade panel is visible before allowing placement
        if (upgradeUI != null && upgradeUI.IsPanelVisible())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = -mainCamera.transform.position.z;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

            if (validPlacement.IsValidPosition(worldPos))
            {
                Vector3 validPos = validPlacement.GetNearestValidPosition(worldPos);
                placementManager.PlaceUnit(validPos);
            }
        }
    }
}