using UnityEngine;

public class ClickPlacementSystem : MonoBehaviour
{
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private ValidPlacementSystem validPlacement;
    [SerializeField] private Camera mainCamera;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (placementManager == null)
            placementManager = FindFirstObjectByType<PlacementManager>();

        if (validPlacement == null)
            validPlacement = FindFirstObjectByType<ValidPlacementSystem>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            // Convert mouse position to world position
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = -mainCamera.transform.position.z;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

            // Check if it's a valid position
            if (validPlacement.IsValidPosition(worldPos))
            {
                // Get the snapped position
                Vector3 validPos = validPlacement.GetNearestValidPosition(worldPos);
                
                // Place the unit
                placementManager.PlaceUnit(validPos);
            }
        }
    }
}