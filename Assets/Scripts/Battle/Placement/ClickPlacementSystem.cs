using UnityEngine;
using Photon.Pun;

public class ClickPlacementSystem : MonoBehaviourPunCallbacks
{
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private ValidPlacementSystem validPlacement;
    [SerializeField] private Camera mainCamera;

    private void Start()
    {
        Debug.Log("ClickPlacementSystem: Start initialized");
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            Debug.Log($"ClickPlacementSystem: Found main camera: {mainCamera != null}");
        }

        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
            Debug.Log($"ClickPlacementSystem: Found placement manager: {placementManager != null}");
        }

        if (validPlacement == null)
        {
            validPlacement = FindFirstObjectByType<ValidPlacementSystem>();
            Debug.Log($"ClickPlacementSystem: Found valid placement system: {validPlacement != null}");
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("ClickPlacementSystem: GameManager instance is null");
            return;
        }

        GameState currentState = GameManager.Instance.GetCurrentState();
        bool isPlacementPhase = (currentState == GameState.PlayerAPlacement || currentState == GameState.PlayerBPlacement);
        
        // Add debug for game state
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"ClickPlacementSystem: Click detected. Current state: {currentState}, Is placement phase: {isPlacementPhase}");
        }

        if (!isPlacementPhase)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandlePlacementClick();
        }
    }

    private void HandlePlacementClick()
    {
        // Convert mouse position to world position
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        Debug.Log($"ClickPlacementSystem: Attempting placement at world position: {worldPos}");

        // Check if position is valid
        bool isValidPosition = validPlacement.IsValidPosition(worldPos);
        Debug.Log($"ClickPlacementSystem: Position valid: {isValidPosition}");

        if (isValidPosition)
        {
            // Get the nearest valid position
            Vector3 validPos = validPlacement.GetNearestValidPosition(worldPos);
            Debug.Log($"ClickPlacementSystem: Using valid position: {validPos}");

            // Check if we can place a unit
            if (placementManager.CanPlaceUnit())
            {
                Debug.Log("ClickPlacementSystem: Calling PlaceUnit on PlacementManager");
                placementManager.PlaceUnit(validPos);
            }
            else
            {
                Debug.Log("ClickPlacementSystem: Cannot place more units (maximum reached or not allowed)");
            }
        }
        else
        {
            // This is the key addition - if not valid, try to get nearest valid position
            // which will run the indicator logic if the position is occupied
            Vector3 validPos = validPlacement.GetNearestValidPosition(worldPos);
            if (validPos == Vector3.zero)
            {
                Debug.Log("ClickPlacementSystem: No valid placement position found");
            }
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        Debug.Log("ClickPlacementSystem: Enabled");
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Debug.Log("ClickPlacementSystem: Disabled");
    }
}