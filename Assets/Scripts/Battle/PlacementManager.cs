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

    private List<BaseUnit> placedUnits = new List<BaseUnit>();
    private GameManager gameManager;
    private ValidPlacementSystem validPlacement;
    private HashSet<string> readyTeams = new HashSet<string>();
    private string currentTeam;
    private int currentUnitCount = 0;

    public event Action OnUnitsChanged;

    private void Start()
    {
        gameManager = GameManager.Instance;
        validPlacement = FindFirstObjectByType<ValidPlacementSystem>();

        if (playerAUnitsParent == null) playerAUnitsParent = transform;
        if (playerBUnitsParent == null) playerBUnitsParent = transform;

        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        Debug.Log($"PlacementManager initialized for {currentTeam}");

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
    }

    public bool CanPlaceUnit()
    {
        return currentUnitCount < maxUnitsPerTeam;
    }

    public void SelectUnitType(UnitType type)
    {
        selectedUnitType = type;
        Debug.Log($"Selected unit type: {type}");
    }

    public void PlaceUnit(Vector3 position)
    {
        Debug.Log($"PlaceUnit called. IsMasterClient: {PhotonNetwork.IsMasterClient}, CurrentTeam: {currentTeam}, CurrentCount: {currentUnitCount}");
    
        if (!CanPlaceUnit())
        {
            Debug.Log("Maximum number of units reached!");
            return;
        }

        bool canPlace = (PhotonNetwork.IsMasterClient && currentTeam == "TeamA") ||
                      (!PhotonNetwork.IsMasterClient && currentTeam == "TeamB");

        if (!canPlace)
        {
            Debug.LogWarning($"Cannot place units for other team. Local: {currentTeam}");
            return;
        }

        string prefabPath = $"UnitPrefabs/{selectedUnitType}";
        GameObject unitObject = PhotonNetwork.Instantiate(prefabPath, position, Quaternion.identity);
        
        if (unitObject == null)
        {
            Debug.LogError($"Failed to instantiate unit prefab: {prefabPath}");
            return;
        }

        BaseUnit unit = unitObject.GetComponent<BaseUnit>();
        if (unit == null)
        {
            Debug.LogError($"Prefab {selectedUnitType} does not have a BaseUnit component!");
            PhotonNetwork.Destroy(unitObject);
            return;
        }

        unit.SetTeam(currentTeam);
        currentUnitCount++;
        
        photonView.RPC("RPCUnitPlaced", RpcTarget.All, unit.photonView.ViewID);
    }

    [PunRPC]
    private void RPCUnitPlaced(int unitViewID)
    {
        PhotonView unitView = PhotonView.Find(unitViewID);
        if (unitView == null) return;

        BaseUnit unit = unitView.GetComponent<BaseUnit>();
        if (unit == null) return;

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

        if (teamUnits.Count >= maxUnitsPerTeam)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("RPCTeamReadyForBattle", RpcTarget.All, unit.GetTeamId());
            }
        }
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
        currentUnitCount = 0;
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
        
        // Reset unit count for the appropriate team
        if ((PhotonNetwork.IsMasterClient && team == "TeamA") ||
            (!PhotonNetwork.IsMasterClient && team == "TeamB"))
        {
            currentUnitCount = 0;
            Debug.Log($"Reset unit count for {team}");
        }

        placedUnits.RemoveAll(unit => unit == null || unit.GetTeamId() == team);
        OnUnitsChanged?.Invoke();
    }

    [PunRPC]
    private void RPCTeamReadyForBattle(string team)
    {
        readyTeams.Add(team);
        
        if (readyTeams.Count == 2 && PhotonNetwork.IsMasterClient)
        {
            gameManager?.StartBattle();
        }
    }

    public int GetPlacedUnitsCount()
    {
        return currentUnitCount;
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
        return placedUnits.Where(u => u != null && u.GetTeamId() == team).ToList();
    }

    public string GetCurrentTeam()
    {
        return currentTeam;
    }
}