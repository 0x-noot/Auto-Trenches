using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ValidPlacementSystem : MonoBehaviour
{
    [SerializeField] private Tilemap placementTilemap;
    [SerializeField] private Color highlightColor = new Color(0, 1, 0, 0.5f);
    
    private List<Vector3Int> validPlacementPositions = new List<Vector3Int>();
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        StoreValidPositions();
    }

    private void StoreValidPositions()
    {
        BoundsInt bounds = placementTilemap.cellBounds;

        for (int x = bounds.min.x; x < bounds.max.x; x++)
        {
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                Vector3Int tilePosition = new Vector3Int(x, y, 0);
                
                if (placementTilemap.HasTile(tilePosition))
                {
                    validPlacementPositions.Add(tilePosition);
                }
            }
        }
    }

    public bool IsValidPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        return validPlacementPositions.Contains(cellPosition);
    }

    public Vector3 GetNearestValidPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        if (validPlacementPositions.Contains(cellPosition))
        {
            return placementTilemap.GetCellCenterWorld(cellPosition);
        }
        return Vector3.zero;
    }

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