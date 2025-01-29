// Create TileDetector.cs
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileDetector : MonoBehaviour
{
    [SerializeField] private Tilemap placementTilemap; // Reference your placement tiles tilemap
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    public bool IsValidPlacementPosition(Vector3 worldPosition)
    {
        // Convert world position to cell position
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        
        // Check if there's a tile at this position
        return placementTilemap.HasTile(cellPosition);
    }

    public Vector3 GetCellCenterWorld(Vector3 worldPosition)
    {
        // Convert to cell position and back to get center
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        return placementTilemap.GetCellCenterWorld(cellPosition);
    }
}