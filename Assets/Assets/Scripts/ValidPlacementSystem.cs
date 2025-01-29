using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ValidPlacementSystem : MonoBehaviour
{
    [SerializeField] private Tilemap placementTilemap; // Drag your tilemap with valid positions here
    [SerializeField] private Color highlightColor = new Color(0, 1, 0, 0.5f); // Green highlight
    
    private List<Vector3Int> validPlacementPositions = new List<Vector3Int>();
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        StoreValidPositions();
    }

    // Store all valid positions when game starts
    private void StoreValidPositions()
    {
        // Get the bounds of the tilemap
        BoundsInt bounds = placementTilemap.cellBounds;

        // Loop through all positions in the tilemap
        for (int x = bounds.min.x; x < bounds.max.x; x++)
        {
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                Vector3Int tilePosition = new Vector3Int(x, y, 0);
                
                // If there's a tile here, it's a valid position
                if (placementTilemap.HasTile(tilePosition))
                {
                    validPlacementPositions.Add(tilePosition);
                }
            }
        }
    }

    // Check if a world position is valid for placement
    public bool IsValidPosition(Vector3 worldPosition)
    {
        // Convert world position to cell position
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        return validPlacementPositions.Contains(cellPosition);
    }

    // Get the center position of the nearest valid tile
    public Vector3 GetNearestValidPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        if (validPlacementPositions.Contains(cellPosition))
        {
            return placementTilemap.GetCellCenterWorld(cellPosition);
        }
        return Vector3.zero; // Return zero if no valid position found
    }

    // Get all valid positions in world coordinates
    public List<Vector3> GetAllValidWorldPositions()
    {
        List<Vector3> worldPositions = new List<Vector3>();
        foreach (Vector3Int cellPos in validPlacementPositions)
        {
            worldPositions.Add(placementTilemap.GetCellCenterWorld(cellPos));
        }
        return worldPositions;
    }
}