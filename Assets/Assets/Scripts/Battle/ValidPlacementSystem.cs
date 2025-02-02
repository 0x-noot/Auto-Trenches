using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ValidPlacementSystem : MonoBehaviour
{
    [Header("Placement Tilemaps")]
    [SerializeField] private Tilemap playerAPlacementTilemap;
    [SerializeField] private Tilemap playerBPlacementTilemap;
    [SerializeField] private Color highlightColor = new Color(0, 1, 0, 0.5f);
    
    private List<Vector3Int> playerAValidPositions = new List<Vector3Int>();
    private List<Vector3Int> playerBValidPositions = new List<Vector3Int>();
    private Camera mainCamera;
    
    private string currentTeam = "PlayerA"; // Default to PlayerA

    void Start()
    {
        mainCamera = Camera.main;
        StoreValidPositions();
    }

    private void StoreValidPositions()
    {
        // Store PlayerA positions
        if (playerAPlacementTilemap != null)
        {
            BoundsInt boundsA = playerAPlacementTilemap.cellBounds;
            for (int x = boundsA.min.x; x < boundsA.max.x; x++)
            {
                for (int y = boundsA.min.y; y < boundsA.max.y; y++)
                {
                    Vector3Int tilePosition = new Vector3Int(x, y, 0);
                    if (playerAPlacementTilemap.HasTile(tilePosition))
                    {
                        playerAValidPositions.Add(tilePosition);
                    }
                }
            }
        }

        // Store PlayerB positions
        if (playerBPlacementTilemap != null)
        {
            BoundsInt boundsB = playerBPlacementTilemap.cellBounds;
            for (int x = boundsB.min.x; x < boundsB.max.x; x++)
            {
                for (int y = boundsB.min.y; y < boundsB.max.y; y++)
                {
                    Vector3Int tilePosition = new Vector3Int(x, y, 0);
                    if (playerBPlacementTilemap.HasTile(tilePosition))
                    {
                        playerBValidPositions.Add(tilePosition);
                    }
                }
            }
        }
    }

    public void SetCurrentTeam(string team)
    {
        currentTeam = team;
    }

    public bool IsValidPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition;
        if (currentTeam == "PlayerA")
        {
            cellPosition = playerAPlacementTilemap.WorldToCell(worldPosition);
            return playerAValidPositions.Contains(cellPosition);
        }
        else
        {
            cellPosition = playerBPlacementTilemap.WorldToCell(worldPosition);
            return playerBValidPositions.Contains(cellPosition);
        }
    }

    public Vector3 GetNearestValidPosition(Vector3 worldPosition)
    {
        Tilemap currentTilemap = currentTeam == "PlayerA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        Vector3Int cellPosition = currentTilemap.WorldToCell(worldPosition);
        
        if ((currentTeam == "PlayerA" && playerAValidPositions.Contains(cellPosition)) ||
            (currentTeam == "PlayerB" && playerBValidPositions.Contains(cellPosition)))
        {
            return currentTilemap.GetCellCenterWorld(cellPosition);
        }
        return Vector3.zero;
    }

    public List<Vector3> GetAllValidWorldPositions(string team)
    {
        List<Vector3> worldPositions = new List<Vector3>();
        Tilemap tilemap = team == "PlayerA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        List<Vector3Int> validPositions = team == "PlayerA" ? playerAValidPositions : playerBValidPositions;

        foreach (Vector3Int cellPos in validPositions)
        {
            worldPositions.Add(tilemap.GetCellCenterWorld(cellPos));
        }
        return worldPositions;
    }
}