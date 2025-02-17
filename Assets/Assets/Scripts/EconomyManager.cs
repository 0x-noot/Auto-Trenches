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
    [SerializeField] private GameManager gameManager;
    private BattleRoundManager battleRoundManager;

    public event Action<string, int> OnSupplyPointsChanged;
    public event Action<string, UpgradeType, int> OnUpgradePurchased;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
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
    }

    private void HandleRoundEnd(string winner, int survivingUnits)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        string winningTeam = winner == "player" ? "TeamA" : "TeamB";
        
        // Log upgrade levels at round end
        foreach (var team in playerEconomies.Keys)
        {
            Debug.Log($"[{team}] Round end upgrade levels:");
            foreach (UpgradeType upgrade in System.Enum.GetValues(typeof(UpgradeType)))
            {
                Debug.Log($"  {upgrade}: {playerEconomies[team].upgradeLevels[upgrade]}");
            }
        }

        int basePoints = survivingUnits;
        int killPoints = CalculateKillPoints(winningTeam);
        int victoryPoints = 3;
        int streakBonus = GetWinStreakBonus(winningTeam);

        int totalPoints = basePoints + killPoints + victoryPoints + streakBonus;
        
        Debug.Log($"Point Breakdown:" +
                $"\nBase Points (Surviving Units): {basePoints}" +
                $"\nKill Points: {killPoints}" +
                $"\nVictory Points: {victoryPoints}" +
                $"\nStreak Bonus: {streakBonus}" +
                $"\nTotal Points: {totalPoints}");

        photonView.RPC("RPCAddSupplyPoints", RpcTarget.All, winningTeam, totalPoints);
    }

    private int CalculateKillPoints(string winner)
    {
        List<BaseUnit> enemyUnits = winner == "TeamA" ? 
            gameManager.GetEnemyUnits() : gameManager.GetPlayerUnits();

        int deadCount = 0;
        foreach (BaseUnit unit in enemyUnits)
        {
            if (unit != null && unit.GetCurrentState() == UnitState.Dead)
            {
                deadCount++;
            }
        }
        return deadCount;
    }

    private int GetWinStreakBonus(string team)
    {
        PlayerHP playerHP = GameObject.Find($"{team}HP")?.GetComponent<PlayerHP>();
        if (playerHP == null) return 0;
        return playerHP.winStreak;
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
        // Only process purchase requests for the local player's team
        string localTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        if (team != localTeam) return false;

        if (!CanPurchaseUpgrade(team, upgradeType)) return false;

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

        Debug.Log($"[{team}] Purchased {upgradeType} upgrade. New level: {economy.upgradeLevels[upgradeType]}");
        
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
                // 25% more HP per level (1.0, 1.25, 1.5)
                multiplier = 1f + (level * 0.25f);
                break;

            case UpgradeType.Training:
                // 30% more damage per level (1.0, 1.3, 1.6)
                multiplier = 1f + (level * 0.3f);
                break;

            case UpgradeType.Speed:
                // 20% more speed per level (1.0, 1.2, 1.4)
                multiplier = 1f + (level * 0.2f);
                break;

            case UpgradeType.AttackSpeed:
                // 25% faster attacks per level (1.0, 1.25, 1.5)
                multiplier = 1f + (level * 0.25f);
                break;

            default:
                multiplier = 1f;
                break;
        }

        Debug.Log($"[{team}] {upgradeType} upgrade level: {level}, multiplier: {multiplier}");
        return multiplier;
    }

    [PunRPC]
    private void RPCAddSupplyPoints(string team, int points)
    {
        if (!playerEconomies.ContainsKey(team)) return;

        playerEconomies[team].supplyPoints += points;
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