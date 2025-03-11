using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;

public class OrderSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    public static OrderSystem Instance { get; private set; }
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Track units by order type
    private Dictionary<OrderType, List<BaseUnit>> orderUnits = new Dictionary<OrderType, List<BaseUnit>>();
    
    // Track order counts by team
    private Dictionary<string, Dictionary<OrderType, int>> teamOrderCounts = new Dictionary<string, Dictionary<OrderType, int>>();
    
    // Events
    public event Action<string, OrderType, int> OnOrderCountChanged;
    public event Action<string, OrderType, int, bool> OnSynergyActivated; // team, order, count, isActivated

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeCollections();
            LogDebug("OrderSystem initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Listen for game state changes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            GameManager.Instance.OnUnitDied += HandleUnitDied;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnUnitDied -= HandleUnitDied;
        }
    }

    private void InitializeCollections()
    {
        // Initialize collections for each order type
        foreach (OrderType order in Enum.GetValues(typeof(OrderType)))
        {
            if (order != OrderType.None) // Skip None type
            {
                orderUnits[order] = new List<BaseUnit>();
            }
        }
        
        // Initialize team dictionaries
        teamOrderCounts["TeamA"] = new Dictionary<OrderType, int>();
        teamOrderCounts["TeamB"] = new Dictionary<OrderType, int>();
        
        // Initialize order counts for each team
        foreach (string team in teamOrderCounts.Keys)
        {
            foreach (OrderType order in Enum.GetValues(typeof(OrderType)))
            {
                if (order != OrderType.None) // Skip None type
                {
                    teamOrderCounts[team][order] = 0;
                }
            }
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.BattleStart)
        {
            // Apply synergies at battle start
            foreach (string team in teamOrderCounts.Keys)
            {
                foreach (OrderType order in teamOrderCounts[team].Keys)
                {
                    if (teamOrderCounts[team][order] > 0)
                    {
                        ApplySynergyEffects(team, order);
                    }
                }
            }
        }
        else if (newState == GameState.PlayerAPlacement || newState == GameState.PlayerBPlacement)
        {
            // Reset unit collections during placement phase
            if (PhotonNetwork.IsMasterClient)
            {
                ClearUnitCollections();
            }
        }
    }

    private void HandleUnitDied(BaseUnit unit)
    {
        if (unit == null) return;
        
        // Unregister unit on death
        UnregisterUnit(unit);
    }

    public void RegisterUnit(BaseUnit unit)
    {
        if (unit == null) return;
        
        OrderType orderType = unit.GetOrderType();
        string teamId = unit.GetTeamId();
        
        // Skip units with no order
        if (orderType == OrderType.None)
            return;
        
        LogDebug($"Registering {unit.GetUnitType()} to {orderType} order for team {teamId}");
        
        // Add to tracking collections
        if (!orderUnits.ContainsKey(orderType))
            orderUnits[orderType] = new List<BaseUnit>();
        
        // CRITICAL: Skip if unit is already registered
        if (orderUnits[orderType].Contains(unit)) {
            LogDebug($"Unit {unit.GetUnitType()} already registered, skipping");
            return;
        }
        
        // Add the unit
        orderUnits[orderType].Add(unit);
        
        // Update team counts
        if (!teamOrderCounts.ContainsKey(teamId))
            teamOrderCounts[teamId] = new Dictionary<OrderType, int>();
        
        if (!teamOrderCounts[teamId].ContainsKey(orderType))
            teamOrderCounts[teamId][orderType] = 0;
        
        // Get current count before incrementing
        int previousCount = teamOrderCounts[teamId][orderType];
        teamOrderCounts[teamId][orderType]++;
        int newCount = teamOrderCounts[teamId][orderType];
        
        LogDebug($"Team {teamId} now has {newCount} units of order {orderType}");
        
        // Apply synergy effects if count thresholds are crossed
        bool synergyActivated = IsSynergyThresholdCrossed(previousCount, newCount);
        if (synergyActivated)
        {
            LogDebug($"Order {orderType} synergy activated for team {teamId} with {newCount} units");
            ApplySynergyEffects(teamId, orderType);
            
            // Fire synergy activated event
            OnSynergyActivated?.Invoke(teamId, orderType, newCount, true);
        }
        
        // Fire count changed event
        OnOrderCountChanged?.Invoke(teamId, orderType, newCount);
    }

    public void UnregisterUnit(BaseUnit unit)
    {
        if (unit == null) return;
        
        OrderType orderType = unit.GetOrderType();
        string teamId = unit.GetTeamId();
        
        // Skip units with no order
        if (orderType == OrderType.None)
            return;
            
        LogDebug($"Unregistering {unit.GetUnitType()} from {orderType} order for team {teamId}");
        
        // Remove from tracking collections
        if (orderUnits.ContainsKey(orderType))
        {
            orderUnits[orderType].Remove(unit);
        }
        
        // Update team counts
        if (teamOrderCounts.ContainsKey(teamId) && teamOrderCounts[teamId].ContainsKey(orderType))
        {
            int previousCount = teamOrderCounts[teamId][orderType];
            teamOrderCounts[teamId][orderType] = Math.Max(0, teamOrderCounts[teamId][orderType] - 1);
            int newCount = teamOrderCounts[teamId][orderType];
            
            LogDebug($"Team {teamId} now has {newCount} units of order {orderType}");
            
            // Check if synergy should be deactivated
            bool synergyDeactivated = IsSynergyThresholdCrossed(newCount + 1, newCount);
            if (synergyDeactivated)
            {
                LogDebug($"Order {orderType} synergy deactivated for team {teamId} with {newCount} units");
                // Should we remove effects? Let's keep them for now
                
                // Fire synergy deactivated event
                OnSynergyActivated?.Invoke(teamId, orderType, newCount, false);
            }
            
            // Fire count changed event
            OnOrderCountChanged?.Invoke(teamId, orderType, newCount);
        }
    }

    private bool IsSynergyThresholdCrossed(int oldCount, int newCount)
    {
        // For most orders, synergy activates at 2 units
        bool crossedThreshold = (oldCount < 2 && newCount >= 2) || (oldCount >= 2 && newCount < 2);
        return crossedThreshold;
    }

    private void ApplySynergyEffects(string teamId, OrderType orderType)
    {
        if (!teamOrderCounts.ContainsKey(teamId) || !teamOrderCounts[teamId].ContainsKey(orderType))
            return;
            
        int count = teamOrderCounts[teamId][orderType];
        
        // Only apply if we have enough units for synergy
        if (count < 2) return;
        
        List<BaseUnit> units = GetOrderUnits(teamId, orderType);
        
        if (units.Count == 0)
        {
            LogDebug($"No units found for order {orderType} on team {teamId}");
            return;
        }
        
        LogDebug($"Applying {orderType} synergy to {units.Count} units for team {teamId}");
        
        // Each order type has different synergy effects
        switch (orderType)
        {
            case OrderType.Shield:
                ApplyShieldSynergy(units, count);
                break;
                
            case OrderType.Wild:
                ApplyWildSynergy(units, count);
                break;
                
            case OrderType.Arcane:
                ApplyArcaneSynergy(units, count);
                break;
                
            case OrderType.Realm:
                ApplyRealmSynergy(units, count);
                break;
        }
    }

    private void ApplyShieldSynergy(List<BaseUnit> units, int count)
    {
        // Shield Order: +15% health, +15% ability trigger chance (was 10%)
        foreach (BaseUnit unit in units)
        {
            if (unit == null || !unit.photonView.IsMine) continue;
            
            // Apply health boost
            unit.ApplySynergyBonus("Shield", "health", 0.15f);
            
            // Apply ability chance boost
            unit.ApplySynergyBonus("Shield", "abilityChance", 0.15f); // Was 0.10f
            
            LogDebug($"Applied Shield synergy to {unit.GetUnitType()} - boosted health by 15% and ability chance by 15%");
        }
    }

    private void ApplyWildSynergy(List<BaseUnit> units, int count)
    {
        // Wild Order: +0.2 attack speed, +15% damage when below 50% HP
        foreach (BaseUnit unit in units)
        {
            if (unit == null || !unit.photonView.IsMine) continue;
            
            // Apply attack speed boost
            unit.ApplySynergyBonus("Wild", "attackSpeed", 0.2f);
            
            // Apply conditional damage boost logic
            unit.ApplySynergyBonus("Wild", "lowHealthDamage", 0.15f);
            
            LogDebug($"Applied Wild synergy to {unit.GetUnitType()} - boosted attack speed by 0.2 and added 15% damage when below 50% HP");
        }
    }

    private void ApplyArcaneSynergy(List<BaseUnit> units, int count)
    {
        // Arcane Order: +15% damage to targets affected by abilities, abilities leave lingering effects
        foreach (BaseUnit unit in units)
        {
            if (unit == null || !unit.photonView.IsMine) continue;
            
            // Apply damage boost logic for affected targets
            unit.ApplySynergyBonus("Arcane", "affectedTargetDamage", 0.15f);
            
            // Apply lingering effect flag
            unit.ApplySynergyBonus("Arcane", "lingeringEffects", 1.0f);
            
            LogDebug($"Applied Arcane synergy to {unit.GetUnitType()} - added 15% damage to affected targets and lingering effects");
        }
    }

    private void ApplyRealmSynergy(List<BaseUnit> units, int count)
    {
        // Realm Order: Each additional Militia grants +15% health and damage (30% cap)
        foreach (BaseUnit unit in units)
        {
            if (unit == null || !unit.photonView.IsMine) continue;
            
            // Calculate bonus based on number of units, capped at 30%
            float bonusMultiplier = Mathf.Min((count - 1) * 0.10f, 0.30f); // Was (count - 1) * 0.15f
            
            // Apply health and damage boosts
            unit.ApplySynergyBonus("Realm", "health", bonusMultiplier);
            unit.ApplySynergyBonus("Realm", "damage", bonusMultiplier);
            
            LogDebug($"Applied Realm synergy to {unit.GetUnitType()} - boosted health and damage by {bonusMultiplier * 100}%");
        }
    }

    private List<BaseUnit> GetOrderUnits(string teamId, OrderType orderType)
    {
        if (!orderUnits.ContainsKey(orderType))
            return new List<BaseUnit>();
            
        return orderUnits[orderType]
            .Where(u => u != null && u.GetTeamId() == teamId && u.GetCurrentState() != UnitState.Dead)
            .ToList();
    }

    public int GetOrderCount(string teamId, OrderType orderType)
    {
        if (!teamOrderCounts.ContainsKey(teamId) || !teamOrderCounts[teamId].ContainsKey(orderType))
            return 0;
            
        return teamOrderCounts[teamId][orderType];
    }

    private void ClearUnitCollections()
    {
        // Create a copy of keys to avoid modification during enumeration
        foreach (OrderType order in orderUnits.Keys.ToList())
        {
            orderUnits[order].Clear();
        }
        
        foreach (string team in teamOrderCounts.Keys.ToList())
        {
            foreach (OrderType order in teamOrderCounts[team].Keys.ToList())
            {
                teamOrderCounts[team][order] = 0;
            }
        }
        
        LogDebug("Unit collections cleared");
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[OrderSystem] {message}");
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send team order counts
            foreach (string team in new[] { "TeamA", "TeamB" })
            {
                foreach (OrderType order in Enum.GetValues(typeof(OrderType)))
                {
                    if (order != OrderType.None)
                    {
                        int count = GetOrderCount(team, order);
                        stream.SendNext(count);
                    }
                }
            }
        }
        else
        {
            // Receive team order counts
            foreach (string team in new[] { "TeamA", "TeamB" })
            {
                foreach (OrderType order in Enum.GetValues(typeof(OrderType)))
                {
                    if (order != OrderType.None)
                    {
                        int count = (int)stream.ReceiveNext();
                        if (teamOrderCounts.ContainsKey(team) && teamOrderCounts[team].ContainsKey(order))
                        {
                            teamOrderCounts[team][order] = count;
                        }
                    }
                }
            }
        }
    }
}