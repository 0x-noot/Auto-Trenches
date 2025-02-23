using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Photon.Pun;

public class ValidPlacementSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Placement Tilemaps")]
    [SerializeField] private Tilemap playerAPlacementTilemap;
    [SerializeField] private Tilemap playerBPlacementTilemap;
    [SerializeField] private Color highlightColor = new Color(0, 1, 0, 0.5f);
    
    private List<Vector3Int> playerAValidPositions = new List<Vector3Int>();
    private List<Vector3Int> playerBValidPositions = new List<Vector3Int>();
    private Camera mainCamera;
    
    private string currentTeam = "TeamA";

    void Start()
    {
        mainCamera = Camera.main;
        StoreValidPositions();

        // Set initial team based on player's actor number
        SetInitialTeam();

        Debug.Log($"ValidPlacementSystem: Initialized with {playerAValidPositions.Count} positions for TeamA and {playerBValidPositions.Count} positions for TeamB");
    }

    private void SetInitialTeam()
    {
        // Get the local player's actor number (1 for master client, 2 for second player)
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        currentTeam = actorNumber == 1 ? "TeamA" : "TeamB";
        Debug.Log($"ValidPlacementSystem: Setting initial team to {currentTeam} for actor {actorNumber}");
    }

    private void StoreValidPositions()
    {
        // Store TeamA positions
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
        else
        {
            Debug.LogError("ValidPlacementSystem: TeamA placement tilemap is not assigned!");
        }

        // Store TeamB positions
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
        else
        {
            Debug.LogError("ValidPlacementSystem: TeamB placement tilemap is not assigned!");
        }
    }

    public void SetCurrentTeam(string team)
    {
        // In networked mode, team is determined by player's actor number
        // We'll log a warning if this is called unexpectedly
        if (team != currentTeam)
        {
            Debug.LogWarning($"ValidPlacementSystem: Attempted to change team to {team} but team is determined by network actor number");
        }
    }

    public bool IsValidPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition;
        bool isValid = false;

        if (currentTeam == "TeamA")
        {
            cellPosition = playerAPlacementTilemap.WorldToCell(worldPosition);
            isValid = playerAValidPositions.Contains(cellPosition);
        }
        else if (currentTeam == "TeamB")
        {
            cellPosition = playerBPlacementTilemap.WorldToCell(worldPosition);
            isValid = playerBValidPositions.Contains(cellPosition);
        }

        Debug.Log($"ValidPlacementSystem: Checking position {worldPosition} for {currentTeam} - Valid: {isValid}");
        return isValid;
    }

    public Vector3 GetNearestValidPosition(Vector3 worldPosition)
    {
        Tilemap currentTilemap = currentTeam == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        Vector3Int cellPosition = currentTilemap.WorldToCell(worldPosition);
        
        if ((currentTeam == "TeamA" && playerAValidPositions.Contains(cellPosition)) ||
            (currentTeam == "TeamB" && playerBValidPositions.Contains(cellPosition)))
        {
            Vector3 validPos = currentTilemap.GetCellCenterWorld(cellPosition);
            Debug.Log($"ValidPlacementSystem: Found valid position {validPos} for {currentTeam}");
            return validPos;
        }

        Debug.LogWarning($"ValidPlacementSystem: No valid position found for {worldPosition}, returning Vector3.zero");
        return Vector3.zero;
    }

    public List<Vector3> GetAllValidWorldPositions(string team)
    {
        List<Vector3> worldPositions = new List<Vector3>();
        Tilemap tilemap = team == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        List<Vector3Int> validPositions = team == "TeamA" ? playerAValidPositions : playerBValidPositions;

        foreach (Vector3Int cellPos in validPositions)
        {
            worldPositions.Add(tilemap.GetCellCenterWorld(cellPos));
        }
        return worldPositions;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // We don't actually need to sync any data since valid positions are determined
        // by the tilemaps and team assignment is based on actor number
        // But we'll keep the interface implementation in case we need to add synced data later
    }
}