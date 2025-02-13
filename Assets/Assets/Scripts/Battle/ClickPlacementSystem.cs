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