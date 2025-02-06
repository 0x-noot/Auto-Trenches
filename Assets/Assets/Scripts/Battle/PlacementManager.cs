using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlacementManager : MonoBehaviour
{
    [System.Serializable]
    public class UnitPrefab
    {
        public string name;
        public GameObject prefab;
        public UnitType type;
    }

    [Header("Unit Settings")]
    [SerializeField] private List<UnitPrefab> unitPrefabs;
    [SerializeField] private int maxUnitsPerTeam = 11;
    [SerializeField] private Transform playerAUnitsParent;
    [SerializeField] private Transform playerBUnitsParent;

    [Header("Current Selection")]
    [SerializeField] private UnitType selectedUnitType = UnitType.Fighter;
    
    [Header("Team Settings")]
    [SerializeField] private string currentTeam = "TeamA";

    private List<BaseUnit> placedUnits = new List<BaseUnit>();
    private GameManager gameManager;
    private ValidPlacementSystem validPlacement;
    
    public event Action OnUnitsChanged;

    private void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found in scene!");
        }

        validPlacement = FindFirstObjectByType<ValidPlacementSystem>();
        if (validPlacement == null)
        {
            Debug.LogError("ValidPlacementSystem not found in scene!");
        }

        // If parent transforms aren't assigned, use this transform as default
        if (playerAUnitsParent == null) playerAUnitsParent = transform;
        if (playerBUnitsParent == null) playerBUnitsParent = transform;

        // Subscribe to game state changes
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        // Verify required layers exist
        VerifyRequiredLayers();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.PlayerAPlacement:
                SetCurrentTeam("TeamA");
                break;
            case GameState.PlayerBPlacement:
                SetCurrentTeam("TeamB");
                break;
        }
    }

    private void VerifyRequiredLayers()
    {
        Debug.Log("Verifying required layers...");
        string[] allLayers = GetAllLayerNames();
        Debug.Log($"Available layers: {string.Join(", ", allLayers)}");
        
        int teamALayer = LayerMask.NameToLayer("TeamA");
        int teamBLayer = LayerMask.NameToLayer("TeamB");
        
        Debug.Log($"TeamA layer index: {teamALayer}");
        Debug.Log($"TeamB layer index: {teamBLayer}");
        
        if (teamALayer == -1)
            Debug.LogError("TeamA layer is missing! Please add it in Project Settings -> Tags and Layers");
        if (teamBLayer == -1)
            Debug.LogError("TeamB layer is missing! Please add it in Project Settings -> Tags and Layers");
    }

    private string[] GetAllLayerNames()
    {
        List<string> layers = new List<string>();
        for(int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if(!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
            }
        }
        return layers.ToArray();
    }

    public void SetCurrentTeam(string team)
    {
        currentTeam = team;
        Debug.Log($"PlacementManager: Current team set to {team}");
        validPlacement?.SetCurrentTeam(team);
    }

    public bool CanPlaceUnit()
    {
        int teamUnitCount = placedUnits.Count(u => u.GetTeamId() == currentTeam);
        return teamUnitCount < maxUnitsPerTeam;
    }

    public void SelectUnitType(UnitType type)
    {
        selectedUnitType = type;
        Debug.Log($"Selected unit type: {type}");
    }

    public void PlaceUnit(Vector3 position)
    {
        Debug.Log($"PlaceUnit called. Current team: {currentTeam}");
        
        if (!CanPlaceUnit())
        {
            Debug.Log("Maximum number of units reached!");
            return;
        }

        GameObject prefab = GetPrefabForType(selectedUnitType);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for unit type: {selectedUnitType}");
            return;
        }

        Transform parentTransform = currentTeam == "TeamA" ? playerAUnitsParent : playerBUnitsParent;
        Debug.Log($"Using parent transform: {parentTransform?.name ?? "null"}");

        GameObject unitObject = Instantiate(prefab, position, Quaternion.identity, parentTransform);
        BaseUnit unit = unitObject.GetComponent<BaseUnit>();
        
        if (unit == null)
        {
            Debug.LogError($"Prefab {prefab.name} does not have a BaseUnit component!");
            Destroy(unitObject);
            return;
        }

        unit.SetTeam(currentTeam);
        
        // Log upgrade levels when placing unit
        if (EconomyManager.Instance != null)
        {
            Debug.Log($"[{currentTeam}] Current upgrade levels when placing {selectedUnitType}:");
            foreach (UpgradeType upgrade in System.Enum.GetValues(typeof(UpgradeType)))
            {
                float multiplier = EconomyManager.Instance.GetUpgradeMultiplier(currentTeam, upgrade);
                Debug.Log($"  {upgrade}: multiplier {multiplier}");
            }
        }

        placedUnits.Add(unit);
        
        if (currentTeam == "TeamA")
        {
            Debug.Log("Registering as player unit");
            gameManager?.RegisterPlayerUnit(unit);
        }
        else
        {
            Debug.Log("Registering as enemy unit");
            gameManager?.RegisterEnemyUnit(unit);
        }

        OnUnitsChanged?.Invoke();

        var teamUnits = GetTeamUnits(currentTeam);
        Debug.Log($"After placement - {currentTeam} units: {teamUnits.Count}/{maxUnitsPerTeam}");

        // Check if current team has placed all units
        if (teamUnits.Count >= maxUnitsPerTeam)
        {
            if (currentTeam == "TeamA")
            {
                gameManager?.UpdateGameState(GameState.PlayerBPlacement);
            }
            else if (currentTeam == "TeamB")
            {
                gameManager?.StartBattle();
            }
        }
    }

    private GameObject GetPrefabForType(UnitType type)
    {
        UnitPrefab unitPrefab = unitPrefabs.Find(u => u.type == type);
        return unitPrefab?.prefab;
    }

    public void ClearUnits()
    {
        Debug.Log("Clearing all units before next round");
        foreach (BaseUnit unit in placedUnits)
        {
            if (unit != null && unit.gameObject != null)
            {
                Debug.Log($"Destroying unit: {unit.GetUnitType()} from team {unit.GetTeamId()}");
                Destroy(unit.gameObject);
            }
        }
        placedUnits.Clear();
        OnUnitsChanged?.Invoke();
    }

    public void ClearTeamUnits(string team)
    {
        Debug.Log($"Clearing units for team: {team}");
        placedUnits.RemoveAll(unit => {
            if (unit != null && unit.GetTeamId() == team)
            {
                Destroy(unit.gameObject);
                return true;
            }
            return false;
        });
        OnUnitsChanged?.Invoke();
    }

    public int GetPlacedUnitsCount()
    {
        return placedUnits.Count(u => u.GetTeamId() == currentTeam);
    }

    public int GetMaxUnits()
    {
        return maxUnitsPerTeam;
    }

    public List<BaseUnit> GetPlacedUnits()
    {
        return placedUnits;
    }

    public List<BaseUnit> GetTeamUnits(string team)
    {
        return placedUnits.Where(u => u.GetTeamId() == team).ToList();
    }

    public bool IsPositionOccupied(Vector3 position, float threshold = 0.5f)
    {
        foreach (BaseUnit unit in placedUnits)
        {
            if (unit != null && Vector3.Distance(unit.transform.position, position) < threshold)
            {
                return true;
            }
        }
        return false;
    }

    public string GetCurrentTeam()
    {
        return currentTeam;
    }
}