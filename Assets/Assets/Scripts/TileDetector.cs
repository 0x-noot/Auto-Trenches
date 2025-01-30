using UnityEngine;
using UnityEngine.Tilemaps;

public class TileDetector : MonoBehaviour
{
    [SerializeField] private Tilemap placementTilemap;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    public bool IsValidPlacementPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        return placementTilemap.HasTile(cellPosition);
    }

    public Vector3 GetCellCenterWorld(Vector3 worldPosition)
    {
        Vector3Int cellPosition = placementTilemap.WorldToCell(worldPosition);
        return placementTilemap.GetCellCenterWorld(cellPosition);
    }
}