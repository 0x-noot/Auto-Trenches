using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;

public class EconomyManager : MonoBehaviourPunCallbacks, IPunObservable
{
    private static EconomyManager instance;
    public static EconomyManager Instance => instance;

    [System.Serializable]
    public class PlayerEconomy
    {
        public int supplyPoints;
        public Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();
    }

    private Dictionary<string, PlayerEconomy> playerEconomies = new Dictionary<string, PlayerEconomy>();
    private GameManager gameManager;
    private BattleRoundManager battleRoundManager;

    public event Action<string, int> OnSupplyPointsChanged;
    public event Action<string, UpgradeType, int> OnUpgradePurchased;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("EconomyManager: Instance created");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeEconomies();
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        battleRoundManager = BattleRoundManager.Instance;

        if (battleRoundManager != null)
        {
            battleRoundManager.OnRoundEnd += HandleRoundEnd;
            Debug.Log("EconomyManager: Subscribed to round end events");
        }
    }

    private void InitializeEconomies()
    {
        playerEconomies["TeamA"] = new PlayerEconomy();
        playerEconomies["TeamB"] = new PlayerEconomy();

        foreach (string team in playerEconomies.Keys)
        {
            foreach (UpgradeType upgrade in Enum.GetValues(typeof(UpgradeType)))
            {
                playerEconomies[team].upgradeLevels[upgrade] = 0;
            }
        }

        Debug.Log("EconomyManager: Economies initialized");
    }

    private void HandleRoundEnd(string winner, int survivingUnits)
    {
        // Only the MasterClient should award points to avoid duplication
        if (!PhotonNetwork.IsMasterClient) return;

        // Determine if local player won
        bool isLocalPlayerWinner = (PhotonNetwork.IsMasterClient && winner == "player") ||
                                (!PhotonNetwork.IsMasterClient && winner == "enemy");

        string winningTeam = isLocalPlayerWinner ? 
            (PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB") : 
            (PhotonNetwork.IsMasterClient ? "TeamB" : "TeamA");

        Debug.Log($"Round ended - Winner: {winner}, Winning Team: {winningTeam}, Local Player Won: {isLocalPlayerWinner}");

        // Calculate points for winning team only
        int basePoints = survivingUnits;
        int killPoints = CalculateKillPoints(winningTeam);
        int victoryPoints = 3;
        int streakBonus = GetWinStreakBonus(winningTeam);

        int totalPoints = basePoints + killPoints + victoryPoints + streakBonus;

        Debug.Log($"Point Breakdown for {winningTeam}:" +
                $"\nBase Points (Surviving Units): {basePoints}" +
                $"\nKill Points: {killPoints}" +
                $"\nVictory Points: {victoryPoints}" +
                $"\nStreak Bonus: {streakBonus}" +
                $"\nTotal Points: {totalPoints}");

        photonView.RPC("RPCAddSupplyPoints", RpcTarget.All, winningTeam, totalPoints);
    }

    private int CalculateKillPoints(string winningTeam)
    {
        List<BaseUnit> enemyUnits = winningTeam == "TeamA" ? 
            gameManager.GetEnemyUnits() : gameManager.GetPlayerUnits();

        int deadCount = enemyUnits.Count(unit => 
            unit != null && unit.GetCurrentState() == UnitState.Dead);

        Debug.Log($"Kill points calculated for {winningTeam}: {deadCount}");
        return deadCount;
    }

    private int GetWinStreakBonus(string team)
    {
        PlayerHP playerHP = GameObject.Find($"{team}HP")?.GetComponent<PlayerHP>();
        if (playerHP == null) return 0;
        
        int streak = playerHP.winStreak;
        Debug.Log($"Win streak bonus for {team}: {streak}");
        return streak;
    }
    public void AddSupplyPoints(string team, int points)
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"EconomyManager: Adding {points} points to {team}");
        photonView.RPC("RPCAddSupplyPoints", RpcTarget.All, team, points);
    }
    public void HandlePointsAwarded(string team, int points)
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Awarding {points} points to {team}");
        photonView.RPC("RPCAddSupplyPoints", RpcTarget.All, team, points);
    }

    public bool CanPurchaseUpgrade(string team, UpgradeType upgradeType)
    {
        if (!playerEconomies.ContainsKey(team)) return false;

        PlayerEconomy economy = playerEconomies[team];
        int currentLevel = economy.upgradeLevels[upgradeType];
        int cost = GetUpgradeCost(upgradeType, currentLevel);

        return economy.supplyPoints >= cost && currentLevel < GetMaxUpgradeLevel(upgradeType);
    }

    public bool PurchaseUpgrade(string team, UpgradeType upgradeType)
    {
        string localTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        if (team != localTeam)
        {
            Debug.LogWarning($"Cannot purchase upgrades for other team. Local: {localTeam}, Requested: {team}");
            return false;
        }

        if (!CanPurchaseUpgrade(team, upgradeType))
        {
            Debug.LogWarning($"Cannot purchase upgrade {upgradeType} for {team}");
            return false;
        }

        PlayerEconomy economy = playerEconomies[team];
        int currentLevel = economy.upgradeLevels[upgradeType];
        int cost = GetUpgradeCost(upgradeType, currentLevel);

        photonView.RPC("RPCProcessUpgradePurchase", RpcTarget.All, team, (int)upgradeType, cost);
        return true;
    }

    [PunRPC]
    private void RPCProcessUpgradePurchase(string team, int upgradeTypeInt, int cost)
    {
        UpgradeType upgradeType = (UpgradeType)upgradeTypeInt;
        PlayerEconomy economy = playerEconomies[team];
        
        economy.supplyPoints -= cost;
        economy.upgradeLevels[upgradeType]++;

        Debug.Log($"{team} purchased {upgradeType} upgrade. New level: {economy.upgradeLevels[upgradeType]}");
        
        OnSupplyPointsChanged?.Invoke(team, economy.supplyPoints);
        OnUpgradePurchased?.Invoke(team, upgradeType, economy.upgradeLevels[upgradeType]);
    }

    private int GetUpgradeCost(UpgradeType upgradeType, int currentLevel)
    {
        return currentLevel == 0 ? 10 : 20;
    }

    private int GetMaxUpgradeLevel(UpgradeType upgradeType)
    {
        return 2;
    }

    public float GetUpgradeMultiplier(string team, UpgradeType upgradeType)
    {
        if (!playerEconomies.ContainsKey(team)) return 1f;

        int level = playerEconomies[team].upgradeLevels[upgradeType];
        float multiplier;

        switch (upgradeType)
        {
            case UpgradeType.Armor:
                multiplier = 1f + (level * 0.25f); // 25% per level
                break;
            case UpgradeType.Training:
                multiplier = 1f + (level * 0.3f);  // 30% per level
                break;
            case UpgradeType.Speed:
                multiplier = 1f + (level * 0.2f);  // 20% per level
                break;
            case UpgradeType.AttackSpeed:
                multiplier = 1f + (level * 0.25f); // 25% per level
                break;
            default:
                multiplier = 1f;
                break;
        }

        Debug.Log($"{team} {upgradeType} upgrade level: {level}, multiplier: {multiplier}");
        return multiplier;
    }

    [PunRPC]
    private void RPCAddSupplyPoints(string team, int points)
    {
        if (!playerEconomies.ContainsKey(team))
        {
            Debug.LogError($"Cannot add points to nonexistent team: {team}");
            return;
        }

        playerEconomies[team].supplyPoints += points;
        Debug.Log($"Added {points} supply points to {team}. New total: {playerEconomies[team].supplyPoints}");
        OnSupplyPointsChanged?.Invoke(team, playerEconomies[team].supplyPoints);
    }

    public int GetSupplyPoints(string team)
    {
        return playerEconomies.ContainsKey(team) ? playerEconomies[team].supplyPoints : 0;
    }

    public int GetUpgradeLevel(string team, UpgradeType upgradeType)
    {
        if (!playerEconomies.ContainsKey(team)) return 0;
        return playerEconomies[team].upgradeLevels[upgradeType];
    }

    private void OnDestroy()
    {
        if (battleRoundManager != null)
        {
            battleRoundManager.OnRoundEnd -= HandleRoundEnd;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Write TeamA data
            stream.SendNext(playerEconomies["TeamA"].supplyPoints);
            foreach (UpgradeType upgrade in Enum.GetValues(typeof(UpgradeType)))
            {
                stream.SendNext(playerEconomies["TeamA"].upgradeLevels[upgrade]);
            }

            // Write TeamB data
            stream.SendNext(playerEconomies["TeamB"].supplyPoints);
            foreach (UpgradeType upgrade in Enum.GetValues(typeof(UpgradeType)))
            {
                stream.SendNext(playerEconomies["TeamB"].upgradeLevels[upgrade]);
            }
        }
        else
        {
            // Read TeamA data
            playerEconomies["TeamA"].supplyPoints = (int)stream.ReceiveNext();
            foreach (UpgradeType upgrade in Enum.GetValues(typeof(UpgradeType)))
            {
                playerEconomies["TeamA"].upgradeLevels[upgrade] = (int)stream.ReceiveNext();
            }

            // Read TeamB data
            playerEconomies["TeamB"].supplyPoints = (int)stream.ReceiveNext();
            foreach (UpgradeType upgrade in Enum.GetValues(typeof(UpgradeType)))
            {
                playerEconomies["TeamB"].upgradeLevels[upgrade] = (int)stream.ReceiveNext();
            }
        }
    }
}