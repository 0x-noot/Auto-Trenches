using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;

public class ValidPlacementSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Placement Tilemaps")]
    [SerializeField] private Tilemap playerAPlacementTilemap;
    [SerializeField] private Tilemap playerBPlacementTilemap;
    [SerializeField] private Color highlightColor = new Color(0, 1, 0, 0.5f);
    
    // Add feedback for invalid placement
    [Header("Placement Feedback")]
    [SerializeField] private GameObject invalidPlacementIndicatorPrefab;
    [SerializeField] private float invalidPlacementIndicatorDuration = 0.5f;
    
    private List<Vector3Int> playerAValidPositions = new List<Vector3Int>();
    private List<Vector3Int> playerBValidPositions = new List<Vector3Int>();
    private Camera mainCamera;
    
    // Add dictionaries to track occupied positions for each team
    private Dictionary<Vector3Int, BaseUnit> playerAOccupiedPositions = new Dictionary<Vector3Int, BaseUnit>();
    private Dictionary<Vector3Int, BaseUnit> playerBOccupiedPositions = new Dictionary<Vector3Int, BaseUnit>();
    
    private string currentTeam = "TeamA";

    void Start()
    {
        mainCamera = Camera.main;
        StoreValidPositions();

        // Set initial team based on player's actor number
        SetInitialTeam();

        Debug.Log($"ValidPlacementSystem: Initialized with {playerAValidPositions.Count} positions for TeamA and {playerBValidPositions.Count} positions for TeamB");
        
        // Subscribe to game state changes to reset occupied positions between rounds
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }
    
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }
    
    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.PlayerAPlacement || newState == GameState.PlayerBPlacement)
        {
            // Clear occupied positions when entering placement phase
            ClearOccupiedPositions();
        }
    }
    
    private void ClearOccupiedPositions()
    {
        playerAOccupiedPositions.Clear();
        playerBOccupiedPositions.Clear();
        Debug.Log("ValidPlacementSystem: Cleared occupied positions for new round");
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
        Vector3Int cellPosition = new Vector3Int(0, 0, 0); // Initialize with default value
        bool isValidTile = false;
        bool isOccupied = false;

        if (currentTeam == "TeamA")
        {
            cellPosition = playerAPlacementTilemap.WorldToCell(worldPosition);
            isValidTile = playerAValidPositions.Contains(cellPosition);
            isOccupied = playerAOccupiedPositions.ContainsKey(cellPosition);
        }
        else if (currentTeam == "TeamB")
        {
            cellPosition = playerBPlacementTilemap.WorldToCell(worldPosition);
            isValidTile = playerBValidPositions.Contains(cellPosition);
            isOccupied = playerBOccupiedPositions.ContainsKey(cellPosition);
        }

        // If the tile is valid but occupied, show the indicator
        if (isValidTile && isOccupied)
        {
            Tilemap currentTilemap = currentTeam == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
            ShowInvalidPlacementIndicator(currentTilemap.GetCellCenterWorld(cellPosition));
            Debug.Log($"ValidPlacementSystem: Position {worldPosition} is occupied, showing indicator");
        }

        bool isValid = isValidTile && !isOccupied;
        Debug.Log($"ValidPlacementSystem: Checking position {worldPosition} for {currentTeam} - Valid: {isValid}");
        return isValid;
    }

    public Vector3 GetNearestValidPosition(Vector3 worldPosition)
    {
        Tilemap currentTilemap = currentTeam == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        Vector3Int cellPosition = currentTilemap.WorldToCell(worldPosition);
        
        Dictionary<Vector3Int, BaseUnit> occupiedPositions = 
            currentTeam == "TeamA" ? playerAOccupiedPositions : playerBOccupiedPositions;
            
        // Check if the position is valid and not occupied
        if ((currentTeam == "TeamA" && playerAValidPositions.Contains(cellPosition) && !occupiedPositions.ContainsKey(cellPosition)) ||
            (currentTeam == "TeamB" && playerBValidPositions.Contains(cellPosition) && !occupiedPositions.ContainsKey(cellPosition)))
        {
            Vector3 validPos = currentTilemap.GetCellCenterWorld(cellPosition);
            Debug.Log($"ValidPlacementSystem: Found valid position {validPos} for {currentTeam}");
            return validPos;
        }
        
        // Show visual feedback for invalid placement
        if (occupiedPositions.ContainsKey(cellPosition))
        {
            ShowInvalidPlacementIndicator(currentTilemap.GetCellCenterWorld(cellPosition));
            Debug.LogWarning($"ValidPlacementSystem: Position {cellPosition} is already occupied");
        }

        Debug.LogWarning($"ValidPlacementSystem: No valid position found for {worldPosition}, returning Vector3.zero");
        return Vector3.zero;
    }
    
    private void ShowInvalidPlacementIndicator(Vector3 position)
    {
        Debug.Log($"ShowInvalidPlacementIndicator called at position {position}");
        
        // Create a simple indicator
        GameObject indicator = new GameObject("InvalidPlacementIndicator");
        indicator.transform.position = position;
        
        // Add a sprite renderer with a red circle and X
        SpriteRenderer renderer = indicator.AddComponent<SpriteRenderer>();
        
        // Create a 128x128 texture (larger resolution for better quality)
        int resolution = 128;
        Texture2D texture = new Texture2D(resolution, resolution);
        Color red = new Color(1f, 0.3f, 0.3f, 0.7f);
        Color clear = new Color(0, 0, 0, 0);
        
        // Fill texture with clear color initially
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                texture.SetPixel(x, y, clear);
            }
        }
        
        // Draw circle
        int center = resolution / 2;
        int outerRadius = resolution / 2 - 4;
        int innerRadius = outerRadius - 8;  // Make circle outline thicker
        
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (distance <= outerRadius && distance >= innerRadius)
                {
                    texture.SetPixel(x, y, red);
                }
            }
        }
        
        // Draw X
        int lineThickness = 8;
        
        // First diagonal (top-left to bottom-right)
        for (int i = 0; i < resolution; i++)
        {
            int j = i;
            for (int t = -lineThickness/2; t <= lineThickness/2; t++)
            {
                int x = Mathf.Clamp(i + t, 0, resolution - 1);
                int y = Mathf.Clamp(j + t, 0, resolution - 1);
                
                // Only draw if inside the outer radius
                float distanceFromCenter = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (distanceFromCenter <= outerRadius)
                {
                    texture.SetPixel(x, y, red);
                }
            }
        }
        
        // Second diagonal (top-right to bottom-left)
        for (int i = 0; i < resolution; i++)
        {
            int j = resolution - 1 - i;
            for (int t = -lineThickness/2; t <= lineThickness/2; t++)
            {
                int x = Mathf.Clamp(i + t, 0, resolution - 1);
                int y = Mathf.Clamp(j + t, 0, resolution - 1);
                
                // Only draw if inside the outer radius
                float distanceFromCenter = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (distanceFromCenter <= outerRadius)
                {
                    texture.SetPixel(x, y, red);
                }
            }
        }
        
        texture.Apply();
        
        // Create sprite from texture
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        renderer.sprite = sprite;
        
        // Set to high sorting order to ensure visibility
        renderer.sortingOrder = 100;
        
        // Make it 1.5x larger
        indicator.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        
        // Destroy after a delay
        Destroy(indicator, invalidPlacementIndicatorDuration);
        
        Debug.Log("Created enhanced indicator with X and larger size");
        
        // Add a simple animation
        StartCoroutine(AnimateIndicator(indicator));
    }

    private IEnumerator AnimateIndicator(GameObject indicator)
    {
        if (indicator == null) yield break;
        
        // Scale up
        float duration = invalidPlacementIndicatorDuration * 0.3f;
        float startTime = Time.time;
        Vector3 startScale = indicator.transform.localScale * 0.5f;
        Vector3 targetScale = indicator.transform.localScale;
        
        while (Time.time < startTime + duration)
        {
            if (indicator == null) yield break;
            
            float t = (Time.time - startTime) / duration;
            indicator.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        
        // Hold
        yield return new WaitForSeconds(invalidPlacementIndicatorDuration * 0.4f);
        
        // Fade out
        startTime = Time.time;
        duration = invalidPlacementIndicatorDuration * 0.3f;
        SpriteRenderer renderer = indicator.GetComponent<SpriteRenderer>();
        
        if (renderer != null)
        {
            Color startColor = renderer.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0);
            
            while (Time.time < startTime + duration)
            {
                if (indicator == null || renderer == null) yield break;
                
                float t = (Time.time - startTime) / duration;
                renderer.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }
        }
    }

    public List<Vector3> GetAllValidWorldPositions(string team)
    {
        List<Vector3> worldPositions = new List<Vector3>();
        Tilemap tilemap = team == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        List<Vector3Int> validPositions = team == "TeamA" ? playerAValidPositions : playerBValidPositions;
        Dictionary<Vector3Int, BaseUnit> occupiedPositions = team == "TeamA" ? playerAOccupiedPositions : playerBOccupiedPositions;

        foreach (Vector3Int cellPos in validPositions)
        {
            if (!occupiedPositions.ContainsKey(cellPos))
            {
                worldPositions.Add(tilemap.GetCellCenterWorld(cellPos));
            }
        }
        return worldPositions;
    }
    
    // Add a method to register a unit at a position
    public void RegisterUnitAtPosition(Vector3 worldPosition, BaseUnit unit)
    {
        Tilemap currentTilemap = unit.GetTeamId() == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        Vector3Int cellPosition = currentTilemap.WorldToCell(worldPosition);
        
        Dictionary<Vector3Int, BaseUnit> occupiedPositions = 
            unit.GetTeamId() == "TeamA" ? playerAOccupiedPositions : playerBOccupiedPositions;
        
        // Register the unit at this position
        if (!occupiedPositions.ContainsKey(cellPosition))
        {
            occupiedPositions[cellPosition] = unit;
            Debug.Log($"ValidPlacementSystem: Registered unit {unit.GetUnitType()} at position {cellPosition} for team {unit.GetTeamId()}");
        }
        else
        {
            Debug.LogWarning($"ValidPlacementSystem: Position {cellPosition} is already occupied by {occupiedPositions[cellPosition].GetUnitType()}");
        }
    }
    
    // Add a method to unregister a unit when it's removed
    public void UnregisterUnitAtPosition(Vector3 worldPosition, string teamId)
    {
        Tilemap tilemap = teamId == "TeamA" ? playerAPlacementTilemap : playerBPlacementTilemap;
        Vector3Int cellPosition = tilemap.WorldToCell(worldPosition);
        
        Dictionary<Vector3Int, BaseUnit> occupiedPositions = 
            teamId == "TeamA" ? playerAOccupiedPositions : playerBOccupiedPositions;
        
        if (occupiedPositions.ContainsKey(cellPosition))
        {
            occupiedPositions.Remove(cellPosition);
            Debug.Log($"ValidPlacementSystem: Unregistered unit at position {cellPosition} for team {teamId}");
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // We don't need to sync occupied positions since each client will track their own units
        // If this changes in the future, we can add synchronization here
    }
}