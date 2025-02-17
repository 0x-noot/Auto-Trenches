using UnityEngine;
using Photon.Pun;

public class ClickPlacementSystem : MonoBehaviourPunCallbacks
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
        // Only process clicks during placement phase and if it's the local player's turn
        if (GameManager.Instance.GetCurrentState() != GameState.PlayerAPlacement && 
            GameManager.Instance.GetCurrentState() != GameState.PlayerBPlacement)
            return;

        // If we're master client (Player A) during Player B's turn, or
        // if we're not master client (Player B) during Player A's turn, don't process clicks
        if ((PhotonNetwork.IsMasterClient && GameManager.Instance.GetCurrentState() == GameState.PlayerBPlacement) ||
            (!PhotonNetwork.IsMasterClient && GameManager.Instance.GetCurrentState() == GameState.PlayerAPlacement))
            return;

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