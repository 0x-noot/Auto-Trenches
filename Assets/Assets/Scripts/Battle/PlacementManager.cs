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
        public string name;
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

        // Set initial team based on player's PhotonView
        SetInitialTeam();

        // Subscribe to game state changes
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        // Verify required layers exist
        VerifyRequiredLayers();
    }

    private void SetInitialTeam()
    {
        // Get the local player's actor number (1 for master client, 2 for second player)
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        currentTeam = actorNumber == 1 ? "TeamA" : "TeamB";
        Debug.Log($"Setting initial team to {currentTeam} for actor {actorNumber}");
        validPlacement?.SetCurrentTeam(currentTeam);
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
        // In networked mode, we don't need to switch teams based on state
        // Each player maintains their own team throughout placement
    }

    private void VerifyRequiredLayers()
    {
        string[] allLayers = GetAllLayerNames();
        
        int teamALayer = LayerMask.NameToLayer("TeamA");
        int teamBLayer = LayerMask.NameToLayer("TeamB");
        
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
        // In networked mode, team is determined by player's actor number
        // This method might still be called from existing code, so we'll log a warning
        Debug.LogWarning("SetCurrentTeam called in networked mode - teams are determined by player actor numbers");
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

    [PunRPC]
    private void RPCUnitPlaced()
    {
        OnUnitsChanged?.Invoke();

        var teamUnits = GetTeamUnits(currentTeam);
        Debug.Log($"After placement - {currentTeam} units: {teamUnits.Count}/{maxUnitsPerTeam}");

        // Check if current team has placed all units
        if (teamUnits.Count >= maxUnitsPerTeam)
        {
            // Both players monitor their own placement completion
            if (PhotonNetwork.IsMasterClient && currentTeam == "TeamA" ||
                !PhotonNetwork.IsMasterClient && currentTeam == "TeamB")
            {
                photonView.RPC("RPCTeamReadyForBattle", RpcTarget.All, currentTeam);
            }
        }
    }

    private HashSet<string> readyTeams = new HashSet<string>();

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

    public void PlaceUnit(Vector3 position)
    {
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
        
        // Instantiate using PhotonNetwork
        GameObject unitObject = PhotonNetwork.Instantiate(
            prefab.name, // Make sure prefab is in Resources folder
            position,
            Quaternion.identity
        );

        BaseUnit unit = unitObject.GetComponent<BaseUnit>();
        if (unit == null)
        {
            Debug.LogError($"Prefab {prefab.name} does not have a BaseUnit component!");
            PhotonNetwork.Destroy(unitObject);
            return;
        }

        // The unit's PhotonView should handle team assignment and stats
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
            gameManager?.RegisterPlayerUnit(unit);
        }
        else
        {
            gameManager?.RegisterEnemyUnit(unit);
        }

        // Notify all clients about the placement
        photonView.RPC("RPCUnitPlaced", RpcTarget.All);
    }


    private GameObject GetPrefabForType(UnitType type)
    {
        UnitPrefab unitPrefab = unitPrefabs.Find(u => u.type == type);
        if (unitPrefab == null) return null;
        
        // Load from Resources folder using the path
        return Resources.Load<GameObject>(unitPrefab.name);
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