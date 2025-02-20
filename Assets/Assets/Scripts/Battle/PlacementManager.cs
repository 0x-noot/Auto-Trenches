using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Photon.Pun;

public class PlacementManager : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class UnitPrefab
    {
        public string name;  // This should match the exact prefab filename
        public UnitType type;
    }

    [Header("Unit Settings")]
    [SerializeField] private List<UnitPrefab> unitPrefabs;
    [SerializeField] private int maxUnitsPerTeam = 11;
    [SerializeField] private Transform playerAUnitsParent;
    [SerializeField] private Transform playerBUnitsParent;

    [Header("Current Selection")]
    [SerializeField] private UnitType selectedUnitType = UnitType.Fighter;

    private List<BaseUnit> placedUnits = new List<BaseUnit>();
    private GameManager gameManager;
    private ValidPlacementSystem validPlacement;
    private HashSet<string> readyTeams = new HashSet<string>();
    
    // Set team based on network role
    private string currentTeam;
    
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

        // Set initial team based on player's actor number
        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        Debug.Log($"PlacementManager initialized for {currentTeam}");

        // Subscribe to game state changes
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameStateChanged;
        }
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
        Debug.Log($"PlacementManager: Handling state change to {newState}");
        // No need to change teams since they're determined by network role
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
        Debug.Log($"PlaceUnit called. IsMasterClient: {PhotonNetwork.IsMasterClient}, CurrentTeam: {currentTeam}");
    
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

        // Add network instantiation ownership check
        if (!PhotonNetwork.IsMasterClient && currentTeam == "TeamA" ||
            PhotonNetwork.IsMasterClient && currentTeam == "TeamB")
        {
            Debug.LogWarning($"Cannot place units for the other team! IsMasterClient: {PhotonNetwork.IsMasterClient}, CurrentTeam: {currentTeam}");
            return;
        }

        Transform parentTransform = currentTeam == "TeamA" ? playerAUnitsParent : playerBUnitsParent;
        
        // Instantiate using PhotonNetwork with path relative to Resources folder
        Debug.Log($"Attempting to instantiate: UnitPrefabs/{selectedUnitType}");
        GameObject unitObject = PhotonNetwork.Instantiate($"UnitPrefabs/{selectedUnitType}", position, Quaternion.identity);

        BaseUnit unit = unitObject.GetComponent<BaseUnit>();
        if (unit == null)
        {
            Debug.LogError($"Prefab {selectedUnitType} does not have a BaseUnit component!");
            PhotonNetwork.Destroy(unitObject);
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

        // Notify all clients about the placement
        photonView.RPC("RPCUnitPlaced", RpcTarget.All, unit.photonView.ViewID);
    }

    [PunRPC]
    private void RPCUnitPlaced(int unitViewID)
    {
        PhotonView unitView = PhotonView.Find(unitViewID);
        if (unitView == null) return;

        BaseUnit unit = unitView.GetComponent<BaseUnit>();
        if (unit == null) return;

        // Add to placed units list
        if (!placedUnits.Contains(unit))
        {
            placedUnits.Add(unit);
            
            if (unit.GetTeamId() == "TeamA")
            {
                gameManager?.RegisterPlayerUnit(unit);
            }
            else
            {
                gameManager?.RegisterEnemyUnit(unit);
            }
        }

        OnUnitsChanged?.Invoke();

        var teamUnits = GetTeamUnits(unit.GetTeamId());
        Debug.Log($"After placement - {unit.GetTeamId()} units: {teamUnits.Count}/{maxUnitsPerTeam}");

        // Check if team has placed all units
        if (teamUnits.Count >= maxUnitsPerTeam)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("RPCTeamReadyForBattle", RpcTarget.All, unit.GetTeamId());
            }
        }
    }

    private GameObject GetPrefabForType(UnitType type)
    {
        UnitPrefab unitPrefab = unitPrefabs.Find(u => u.type == type);
        if (unitPrefab == null) 
        {
            Debug.LogError($"No UnitPrefab configuration found for type: {type}");
            return null;
        }
        
        GameObject prefab = Resources.Load<GameObject>($"UnitPrefabs/{type}");
        if (prefab == null)
        {
            Debug.LogError($"Could not load prefab from Resources/UnitPrefabs/{type}");
        }
        return prefab;
    }

    public void ClearUnits()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        Debug.Log("Clearing all units before next round");
        foreach (BaseUnit unit in placedUnits.ToList())
        {
            if (unit != null && unit.gameObject != null)
            {
                Debug.Log($"Destroying unit: {unit.GetUnitType()} from team {unit.GetTeamId()}");
                PhotonNetwork.Destroy(unit.gameObject);
            }
        }
        placedUnits.Clear();
        readyTeams.Clear();
        photonView.RPC("RPCUnitsCleared", RpcTarget.All);
    }

    [PunRPC]
    private void RPCUnitsCleared()
    {
        OnUnitsChanged?.Invoke();
    }

    public void ClearTeamUnits(string team)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"Clearing units for team: {team}");
        foreach (var unit in placedUnits.ToList())
        {
            if (unit != null && unit.GetTeamId() == team)
            {
                PhotonNetwork.Destroy(unit.gameObject);
            }
        }
        
        placedUnits.RemoveAll(unit => unit == null || unit.GetTeamId() == team);
        photonView.RPC("RPCTeamUnitsCleared", RpcTarget.All, team);
    }

    [PunRPC]
    private void RPCTeamUnitsCleared(string team)
    {
        readyTeams.Remove(team);
        OnUnitsChanged?.Invoke();
    }

    [PunRPC]
    private void RPCTeamReadyForBattle(string team)
    {
        readyTeams.Add(team);
        
        // If both teams are ready, the master client starts the battle
        if (readyTeams.Count == 2 && PhotonNetwork.IsMasterClient)
        {
            gameManager?.StartBattle();
        }
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