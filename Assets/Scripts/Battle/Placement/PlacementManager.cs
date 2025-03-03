using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Photon.Pun;
using System.Collections;

public class PlacementManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [System.Serializable]
    public class UnitPrefab
    {
        public string name;
        public UnitType type;
    }

    [Header("Unit Settings")]
    [SerializeField] private List<UnitPrefab> unitPrefabs;
    [SerializeField] private Transform playerAUnitsParent;
    [SerializeField] private Transform playerBUnitsParent;

    [Header("Command Points Settings")]
    [SerializeField] private int startingCommandPoints = 20;
    [SerializeField] private int maxCommandPoints = 30;
    [SerializeField] private int pointsPerRound = 2;

    [Header("Unit Costs")]
    [SerializeField] private int knightCost = 5;     // Was tankCost
    [SerializeField] private int sorcererCost = 4;   // Was mageCost
    [SerializeField] private int archerCost = 4;     // Was rangeCost
    [SerializeField] private int berserkerCost = 3;  // Was fighterCost
    // New unit costs
    [SerializeField] private int clericCost = 4;
    [SerializeField] private int barbarianCost = 4;
    [SerializeField] private int peasantMilitiaCost = 2;
    [SerializeField] private int blacksmithCost = 5;

    [Header("Current Selection")]
    [SerializeField] private UnitType selectedUnitType = UnitType.Berserker;  // Was UnitType.Fighter

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    private List<BaseUnit> placedUnits = new List<BaseUnit>();
    private GameManager gameManager;
    private ValidPlacementSystem validPlacement;
    private HashSet<string> readyTeams = new HashSet<string>();
    private string currentTeam;
    private bool isLocalPlayerReady = false;
    private bool isProcessingPlacement = false;
    private HashSet<long> pendingPlacementTimestamps = new HashSet<long>();
    
    // Command points dictionaries
    private Dictionary<string, int> teamCommandPoints = new Dictionary<string, int>();
    private Dictionary<string, int> teamMaxCommandPoints = new Dictionary<string, int>();
    private Dictionary<UnitType, int> unitCosts = new Dictionary<UnitType, int>();

    public event Action OnUnitsChanged;
    public event Action<string, int, int> OnCommandPointsChanged; // team, current, max

    private void Start()
    {
        gameManager = GameManager.Instance;
        validPlacement = FindFirstObjectByType<ValidPlacementSystem>();

        if (playerAUnitsParent == null) playerAUnitsParent = transform;
        if (playerBUnitsParent == null) playerBUnitsParent = transform;

        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        LogDebug($"PlacementManager initialized for {currentTeam}");

        // Initialize unit costs
        InitializeUnitCosts();
        
        // Initialize command points
        InitializeCommandPoints();

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameStateChanged;
        }
        
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
        
        if (BattleRoundManager.Instance != null)
        {
            BattleRoundManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }
    
    private void InitializeUnitCosts()
    {
        unitCosts[UnitType.Knight] = knightCost;        // Was Tank
        unitCosts[UnitType.Sorcerer] = sorcererCost;    // Was Mage
        unitCosts[UnitType.Archer] = archerCost;        // Was Range
        unitCosts[UnitType.Berserker] = berserkerCost;  // Was Fighter
        unitCosts[UnitType.Cleric] = clericCost;
        unitCosts[UnitType.Barbarian] = barbarianCost;
        unitCosts[UnitType.PeasantMilitia] = peasantMilitiaCost;
        unitCosts[UnitType.Blacksmith] = blacksmithCost;
    }
    
    private void InitializeCommandPoints()
    {
        // Initialize command points for both teams
        teamCommandPoints["TeamA"] = startingCommandPoints;
        teamCommandPoints["TeamB"] = startingCommandPoints;
        
        teamMaxCommandPoints["TeamA"] = startingCommandPoints;
        teamMaxCommandPoints["TeamB"] = startingCommandPoints;

        // Notify any listeners
        OnCommandPointsChanged?.Invoke("TeamA", startingCommandPoints, startingCommandPoints);
        OnCommandPointsChanged?.Invoke("TeamB", startingCommandPoints, startingCommandPoints);

        LogDebug($"Initialized command points. A: {startingCommandPoints}, B: {startingCommandPoints}");
    }
    
    private void HandleRoundStart(int round)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Increase command points at the start of each round
        photonView.RPC("RPCIncreaseCommandPoints", RpcTarget.All);
    }
    
    [PunRPC]
    private void RPCIncreaseCommandPoints()
    {
        foreach (string team in new[] { "TeamA", "TeamB" })
        {
            int newMax = Mathf.Min(teamMaxCommandPoints[team] + pointsPerRound, maxCommandPoints);
            teamMaxCommandPoints[team] = newMax;
            teamCommandPoints[team] = newMax; // Reset current points to new max

            // Notify listeners
            OnCommandPointsChanged?.Invoke(team, teamCommandPoints[team], teamMaxCommandPoints[team]);
        }

        LogDebug($"Increased command points. A: {teamCommandPoints["TeamA"]}/{teamMaxCommandPoints["TeamA"]}, B: {teamCommandPoints["TeamB"]}/{teamMaxCommandPoints["TeamB"]}");
    }

    private void HandleGameStateChanged(GameState newState)
    {
        LogDebug($"Handling state change to {newState}");
    }

    public bool CanPlaceUnit()
    {
        return CanPlaceUnit(currentTeam, selectedUnitType);
    }
    
    public bool CanPlaceUnit(string team, UnitType unitType)
    {
        if (!teamCommandPoints.ContainsKey(team) || !unitCosts.ContainsKey(unitType))
            return false;

        return teamCommandPoints[team] >= unitCosts[unitType];
    }

    public void SelectUnitType(UnitType type)
    {
        selectedUnitType = type;
        LogDebug($"Selected unit type: {type}, Cost: {GetUnitCost(type)}");
    }

    public void PlaceUnit(Vector3 position)
    {
        // Generate a unique timestamp for this placement request
        long requestTimestamp = DateTime.Now.Ticks;
        
        // Check if we're already processing a placement
        if (isProcessingPlacement)
        {
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Placement already in progress, please wait. isProcessingPlacement={isProcessingPlacement}, pendingTimestamps={pendingPlacementTimestamps.Count}");
            return;
        }
        
        LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Starting placement. Points: {GetCommandPoints(currentTeam)}, Cost: {GetUnitCost(selectedUnitType)}");
        
        // Double-check command points before proceeding
        if (!CanPlaceUnit())
        {
            LogDebug($"Cannot place unit. Insufficient command points. Required: {GetUnitCost(selectedUnitType)}, Available: {GetCommandPoints(currentTeam)}");
            return;
        }

        bool canPlace = (PhotonNetwork.IsMasterClient && currentTeam == "TeamA") ||
                    (!PhotonNetwork.IsMasterClient && currentTeam == "TeamB");

        if (!canPlace)
        {
            Debug.LogWarning($"Cannot place units for other team. Local: {currentTeam}");
            return;
        }

        // Lock placement process and track this request
        isProcessingPlacement = true;
        pendingPlacementTimestamps.Add(requestTimestamp);
        
        LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Placement locked. Timestamp: {requestTimestamp}");
        
        // Strictly enforce command point limit - fail if not enough
        int currentPoints = teamCommandPoints[currentTeam];
        int cost = unitCosts[selectedUnitType];
        
        if (currentPoints < cost)
        {
            Debug.LogError($"Command point check failed at placement execution. Available: {currentPoints}, Required: {cost}");
            isProcessingPlacement = false;
            pendingPlacementTimestamps.Remove(requestTimestamp);
            return;
        }

        // CRITICAL: Locally deduct points immediately to prevent overplacement during latency
        teamCommandPoints[currentTeam] = Mathf.Max(0, currentPoints - cost);
        
        // Notify UI immediately for responsive feedback
        OnCommandPointsChanged?.Invoke(currentTeam, teamCommandPoints[currentTeam], teamMaxCommandPoints[currentTeam]);

        // Deduct command points using RPC that targets ALL clients
        photonView.RPC("RPCSpendCommandPoints", RpcTarget.AllBuffered, currentTeam, (int)selectedUnitType, requestTimestamp);

        try
        {
            // Convert enum names to proper resource paths
            string unitTypeName = selectedUnitType.ToString();
            string prefabPath = $"UnitPrefabs/{unitTypeName}";
            
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Instantiating unit: {prefabPath}");
            GameObject unitObject = PhotonNetwork.Instantiate(prefabPath, position, Quaternion.identity);
            
            if (unitObject == null)
            {
                Debug.LogError($"Failed to instantiate unit prefab: {prefabPath}");
                // Refund points on failure
                teamCommandPoints[currentTeam] = Mathf.Min(teamMaxCommandPoints[currentTeam], currentPoints);
                OnCommandPointsChanged?.Invoke(currentTeam, teamCommandPoints[currentTeam], teamMaxCommandPoints[currentTeam]);
                isProcessingPlacement = false;
                pendingPlacementTimestamps.Remove(requestTimestamp);
                return;
            }

            BaseUnit unit = unitObject.GetComponent<BaseUnit>();
            if (unit == null)
            {
                Debug.LogError($"Prefab {selectedUnitType} does not have a BaseUnit component!");
                PhotonNetwork.Destroy(unitObject);
                // Refund points on failure
                teamCommandPoints[currentTeam] = Mathf.Min(teamMaxCommandPoints[currentTeam], currentPoints);
                OnCommandPointsChanged?.Invoke(currentTeam, teamCommandPoints[currentTeam], teamMaxCommandPoints[currentTeam]);
                isProcessingPlacement = false;
                pendingPlacementTimestamps.Remove(requestTimestamp);
                return;
            }

            unit.SetTeam(currentTeam);
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Unit placed successfully, sending RPC");
            
            photonView.RPC("RPCUnitPlaced", RpcTarget.All, unit.photonView.ViewID, requestTimestamp);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during unit placement: {ex.Message}");
            // Refund points on exception
            teamCommandPoints[currentTeam] = Mathf.Min(teamMaxCommandPoints[currentTeam], currentPoints);
            OnCommandPointsChanged?.Invoke(currentTeam, teamCommandPoints[currentTeam], teamMaxCommandPoints[currentTeam]);
            isProcessingPlacement = false;
            pendingPlacementTimestamps.Remove(requestTimestamp);
        }
        
        // Add a delay before allowing next placement to prevent spam clicking
        StartCoroutine(UnlockPlacementAfterDelay(0.2f, requestTimestamp));
    }

    private IEnumerator UnlockPlacementAfterDelay(float delay, long requestTimestamp)
    {
        yield return new WaitForSeconds(delay);
        
        // Automatically unlock after a timeout to prevent permanent locks
        if (pendingPlacementTimestamps.Contains(requestTimestamp))
        {
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Removing timestamp {requestTimestamp} and unlocking placement");
            pendingPlacementTimestamps.Remove(requestTimestamp);
        }
        
        // If there are no more pending placements, unlock
        if (pendingPlacementTimestamps.Count == 0)
        {
            isProcessingPlacement = false;
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Placement unlocked. No more pending timestamps.");
        }
        else
        {
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Keeping placement locked. {pendingPlacementTimestamps.Count} timestamps still pending.");
        }
    }

    [PunRPC]
    private void RPCSpendCommandPoints(string team, int unitTypeInt, long requestTimestamp)
    {
        UnitType unitType = (UnitType)unitTypeInt;
        
        if (teamCommandPoints.ContainsKey(team) && unitCosts.ContainsKey(unitType))
        {
            int previousPoints = teamCommandPoints[team];
            int cost = unitCosts[unitType];
            
            // For the host, don't deduct points again - they were already deducted locally
            // For the client, apply the deduction normally
            if (PhotonNetwork.IsMasterClient && team == "TeamA" || 
                !PhotonNetwork.IsMasterClient && team == "TeamB")
            {
                // This is our own team, and we've already deducted points locally
                // So just ensure the value is correct, but don't deduct again
                teamCommandPoints[team] = Mathf.Max(0, previousPoints);
            }
            else
            {
                // This is the other team, or we're receiving an RPC from the other player
                // Apply the deduction normally
                teamCommandPoints[team] = Mathf.Max(0, previousPoints - cost);
            }
            
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] RPCSpendCommandPoints: {team} spent {cost} points for {unitType}. Points: {previousPoints} -> {teamCommandPoints[team]}");
            
            // Notify listeners
            OnCommandPointsChanged?.Invoke(team, teamCommandPoints[team], teamMaxCommandPoints[team]);
        }
    }

    private void RefundCommandPoints(string team, UnitType unitType)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC("RPCRefundCommandPoints", RpcTarget.AllBuffered, team, (int)unitType);
    }

    [PunRPC]
    private void RPCRefundCommandPoints(string team, int unitTypeInt)
    {
        UnitType unitType = (UnitType)unitTypeInt;
        
        if (teamCommandPoints.ContainsKey(team) && unitCosts.ContainsKey(unitType))
        {
            teamCommandPoints[team] += unitCosts[unitType];
            
            // Make sure we don't exceed max points
            teamCommandPoints[team] = Mathf.Min(teamCommandPoints[team], teamMaxCommandPoints[team]);
            
            // Notify listeners
            OnCommandPointsChanged?.Invoke(team, teamCommandPoints[team], teamMaxCommandPoints[team]);
            
            LogDebug($"{team} refunded {unitCosts[unitType]} points for {unitType}. Remaining points: {teamCommandPoints[team]}/{teamMaxCommandPoints[team]}");
        }
    }

    [PunRPC]
    private void RPCUnitPlaced(int unitViewID, long requestTimestamp)
    {
        LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] RPCUnitPlaced received for ViewID {unitViewID}, timestamp {requestTimestamp}");
        // Process normal unit placement logic
        PhotonView unitView = PhotonView.Find(unitViewID);
        if (unitView == null)
        {
            Debug.LogError($"Cannot find unit with ViewID {unitViewID}");
            return;
        }

        BaseUnit unit = unitView.GetComponent<BaseUnit>();
        if (unit == null)
        {
            Debug.LogError($"Unit with ViewID {unitViewID} has no BaseUnit component");
            return;
        }

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
            
            // Register with OrderSystem if applicable
            OrderType orderType = unit.GetOrderType();
            if (orderType != OrderType.None && OrderSystem.Instance != null)
            {
                OrderSystem.Instance.RegisterUnit(unit);
                LogDebug($"Registered unit {unit.GetUnitType()} with order {orderType}");
            }
        }

        OnUnitsChanged?.Invoke();

        var teamUnits = GetTeamUnits(unit.GetTeamId());
        LogDebug($"After placement - {unit.GetTeamId()} units: {teamUnits.Count}");
        
        // This is critical - make sure we unlock placement for both the originator and other clients
        if (photonView.IsMine && pendingPlacementTimestamps.Contains(requestTimestamp))
        {
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Removing own timestamp {requestTimestamp}");
            pendingPlacementTimestamps.Remove(requestTimestamp);
        }
        
        // IMPORTANT: Force unlock placement after a short delay to prevent locking up
        if (!photonView.IsMine)
        {
            LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Received RPC from other player, scheduling unlock");
            // For non-originator clients, unlock after processing the RPC
            StartCoroutine(ForceUnlockAfterDelay(0.1f));
        }
    }
    
    public void SetTeamReady(string team, bool isReady)
    {
        if (!PhotonNetwork.IsConnected) return;
        
        LogDebug($"SetTeamReady called: team={team}, isReady={isReady}, currentState={isLocalPlayerReady}");
        
        // Only send RPC if we're changing state
        if (isReady != isLocalPlayerReady)
        {
            if (isReady)
            {
                LogDebug($"Setting {team} to ready");
                photonView.RPC("RPCSetTeamReady", RpcTarget.AllBuffered, team);
                isLocalPlayerReady = true;
            }
            else
            {
                LogDebug($"Setting {team} to not ready");
                photonView.RPC("RPCSetTeamNotReady", RpcTarget.AllBuffered, team);
                isLocalPlayerReady = false;
            }
        }
    }

    [PunRPC]
    private void RPCSetTeamReady(string team)
    {
        LogDebug($"RPCSetTeamReady: Adding {team} to readyTeams");
        readyTeams.Add(team);
        
        // If this is our team, update local ready state
        if (team == (PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB"))
        {
            isLocalPlayerReady = true;
            LogDebug("Updated local ready state to true");
        }
        
        LogDebug($"Team {team} is ready. Ready teams: {readyTeams.Count}/2");
        
        // Notify UI that readiness state changed
        OnUnitsChanged?.Invoke(); // Reuse this event to update UI
        
        // Check if all teams are ready
        if (readyTeams.Count == 2 && PhotonNetwork.IsMasterClient)
        {
            gameManager?.StartBattle();
        }
    }

    [PunRPC]
    private void RPCSetTeamNotReady(string team)
    {
        LogDebug($"RPCSetTeamNotReady: Removing {team} from readyTeams");
        readyTeams.Remove(team);
        
        // If this is our team, update local ready state
        if (team == (PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB"))
        {
            isLocalPlayerReady = false;
            LogDebug("Updated local ready state to false");
        }
        
        LogDebug($"Team {team} is not ready. Ready teams: {readyTeams.Count}/2");
        
        // Notify UI that readiness state changed
        OnUnitsChanged?.Invoke();
    }

    private IEnumerator ForceUnlockAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        pendingPlacementTimestamps.Clear();
        isProcessingPlacement = false;
        LogDebug($"[{(PhotonNetwork.IsMasterClient ? "HOST" : "CLIENT")}] Forced placement unlock");
    }

    public bool IsLocalPlayerReady()
    {
        return isLocalPlayerReady;
    }
    
    public bool IsTeamReady(string team)
    {
        return readyTeams.Contains(team);
    }

    public int GetReadyTeamsCount()
    {
        return readyTeams.Count;
    }

    private void ResetReadyState()
    {
        readyTeams.Clear();
        isLocalPlayerReady = false;
    }

    public void ClearUnits()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        LogDebug("Clearing all units before next round");
        foreach (BaseUnit unit in placedUnits.ToList())
        {
            if (unit != null && unit.gameObject != null)
            {
                LogDebug($"Destroying unit: {unit.GetUnitType()} from team {unit.GetTeamId()}");
                PhotonNetwork.Destroy(unit.gameObject);
            }
        }
        placedUnits.Clear();
        readyTeams.Clear();
        isLocalPlayerReady = false;
        
        // Reset command points
        ResetCommandPoints();
        
        photonView.RPC("RPCUnitsCleared", RpcTarget.All);
    }

    private void ResetCommandPoints()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC("RPCResetCommandPoints", RpcTarget.All);
    }

    [PunRPC]
    private void RPCResetCommandPoints()
    {
        foreach (string team in new[] { "TeamA", "TeamB" })
        {
            teamCommandPoints[team] = teamMaxCommandPoints[team];
            
            // Notify listeners
            OnCommandPointsChanged?.Invoke(team, teamCommandPoints[team], teamMaxCommandPoints[team]);
        }
        
        LogDebug($"Reset command points. A: {teamCommandPoints["TeamA"]}/{teamMaxCommandPoints["TeamA"]}, B: {teamCommandPoints["TeamB"]}/{teamMaxCommandPoints["TeamB"]}");
    }

    [PunRPC]
    private void RPCUnitsCleared()
    {
        OnUnitsChanged?.Invoke();
    }

    public void ClearTeamUnits(string team)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        LogDebug($"Clearing units for team: {team}");
        
        // Keep track of units to refund
        Dictionary<UnitType, int> unitsToRefund = new Dictionary<UnitType, int>();
        
        foreach (var unit in placedUnits.ToList())
        {
            if (unit != null && unit.GetTeamId() == team)
            {
                // Count unit types for refund
                UnitType unitType = unit.GetUnitType();
                if (!unitsToRefund.ContainsKey(unitType))
                {
                    unitsToRefund[unitType] = 0;
                }
                unitsToRefund[unitType]++;
                
                PhotonNetwork.Destroy(unit.gameObject);
            }
        }
        
        placedUnits.RemoveAll(unit => unit == null || unit.GetTeamId() == team);
        
        // Reset ready state for the team
        readyTeams.Remove(team);
        if (team == currentTeam)
        {
            isLocalPlayerReady = false;
        }
        
        photonView.RPC("RPCTeamUnitsCleared", RpcTarget.All, team);
        
        // Refund command points
        foreach (var kvp in unitsToRefund)
        {
            for (int i = 0; i < kvp.Value; i++)
            {
                RefundCommandPoints(team, kvp.Key);
            }
        }
    }

    [PunRPC]
    private void RPCTeamUnitsCleared(string team)
    {
        readyTeams.Remove(team);
        if (team == currentTeam)
        {
            isLocalPlayerReady = false;
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

    // Getters
    public int GetCommandPoints(string team)
    {
        return teamCommandPoints.ContainsKey(team) ? teamCommandPoints[team] : 0;
    }

    public int GetMaxCommandPoints(string team)
    {
        return teamMaxCommandPoints.ContainsKey(team) ? teamMaxCommandPoints[team] : 0;
    }

    public int GetUnitCost(UnitType unitType)
    {
        return unitCosts.ContainsKey(unitType) ? unitCosts[unitType] : 0;
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
    
    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlacementManager] {message}");
        }
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Write TeamA command points data
            stream.SendNext(teamCommandPoints["TeamA"]);
            stream.SendNext(teamMaxCommandPoints["TeamA"]);

            // Write TeamB command points data
            stream.SendNext(teamCommandPoints["TeamB"]);
            stream.SendNext(teamMaxCommandPoints["TeamB"]);
            
            // Send ready state information
            stream.SendNext(readyTeams.Contains("TeamA"));
            stream.SendNext(readyTeams.Contains("TeamB"));
        }
        else
        {
            // Read TeamA command points data
            teamCommandPoints["TeamA"] = (int)stream.ReceiveNext();
            teamMaxCommandPoints["TeamA"] = (int)stream.ReceiveNext();

            // Read TeamB command points data
            teamCommandPoints["TeamB"] = (int)stream.ReceiveNext();
            teamMaxCommandPoints["TeamB"] = (int)stream.ReceiveNext();
            
            // Read ready state information
            bool teamAReady = (bool)stream.ReceiveNext();
            bool teamBReady = (bool)stream.ReceiveNext();
            
            // Update ready teams set
            if (teamAReady) readyTeams.Add("TeamA");
            else readyTeams.Remove("TeamA");
            
            if (teamBReady) readyTeams.Add("TeamB");
            else readyTeams.Remove("TeamB");
            
            // Update local player ready state
            string localTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
            isLocalPlayerReady = readyTeams.Contains(localTeam);
            
            // Notify UI of changes
            OnCommandPointsChanged?.Invoke("TeamA", teamCommandPoints["TeamA"], teamMaxCommandPoints["TeamA"]);
            OnCommandPointsChanged?.Invoke("TeamB", teamCommandPoints["TeamB"], teamMaxCommandPoints["TeamB"]);
            OnUnitsChanged?.Invoke();
        }
    }
}