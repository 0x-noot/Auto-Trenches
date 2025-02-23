using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Photon.Pun;

public class UpgradeUI : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class UpgradeButton
    {
        public UpgradeType type;
        public Button button;
        public TextMeshProUGUI costText;
        public TextMeshProUGUI levelText;
    }

    [Header("UI References")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private TextMeshProUGUI supplyPointsText;
    [SerializeField] private List<UpgradeButton> upgradeButtons;
    
    private EconomyManager economyManager;
    private string currentTeam;

    private void Start()
    {
        Debug.Log($"UpgradeUI Start - IsMasterClient: {PhotonNetwork.IsMasterClient}");
        economyManager = EconomyManager.Instance;
        if (economyManager == null)
        {
            Debug.LogError("EconomyManager not found!");
            return;
        }

        // Set team based on network role
        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        Debug.Log($"UpgradeUI initialized for {currentTeam}");

        InitializeUI();
        
        economyManager.OnSupplyPointsChanged += UpdateSupplyPoints;
        economyManager.OnUpgradePurchased += UpdateUpgradeButton;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        // Force initial UI update
        UpdateSupplyPoints(currentTeam, economyManager.GetSupplyPoints(currentTeam));
        foreach (var upgradeButton in upgradeButtons)
        {
            UpdateUpgradeButton(currentTeam, upgradeButton.type, 
                economyManager.GetUpgradeLevel(currentTeam, upgradeButton.type));
        }
    }

    private void OnDestroy()
    {
        if (economyManager != null)
        {
            economyManager.OnSupplyPointsChanged -= UpdateSupplyPoints;
            economyManager.OnUpgradePurchased -= UpdateUpgradeButton;
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void InitializeUI()
    {
        Debug.Log("InitializeUI called");
        foreach (var upgradeButton in upgradeButtons)
        {
            if (upgradeButton.button != null)
            {
                UpgradeType type = upgradeButton.type;
                upgradeButton.button.onClick.RemoveAllListeners();
                upgradeButton.button.onClick.AddListener(() => 
                {
                    Debug.Log($"Upgrade button clicked: {type}");
                    PurchaseUpgrade(type);
                });
                UpdateUpgradeButton(currentTeam, type, 
                    economyManager.GetUpgradeLevel(currentTeam, type));
            }
            else
            {
                Debug.LogError($"Button for upgrade type {upgradeButton.type} is null!");
            }
        }
        UpdateSupplyPoints(currentTeam, economyManager.GetSupplyPoints(currentTeam));
    }

    private void HandleGameStateChanged(GameState newState)
    {
        Debug.Log($"UpgradeUI: Handling state change to {newState}");

        switch (newState)
        {
            case GameState.PlayerAPlacement:
            case GameState.PlayerBPlacement:
                // Always show panel during placement phase
                upgradePanel.SetActive(true);
                UpdateAllUI();
                break;
                    
            case GameState.BattleStart:
            case GameState.BattleActive:
                upgradePanel.SetActive(false);
                break;
        }
    }

    private void UpdateAllUI()
    {
        UpdateSupplyPoints(currentTeam, economyManager.GetSupplyPoints(currentTeam));
        foreach (var upgradeButton in upgradeButtons)
        {
            UpdateUpgradeButton(currentTeam, upgradeButton.type, 
                economyManager.GetUpgradeLevel(currentTeam, upgradeButton.type));
        }
    }

    private void UpdateSupplyPoints(string team, int points)
    {
        if (team != currentTeam) return;
        supplyPointsText.text = $"Supply Points: {points}";
        UpdateButtonStates();
    }

    private void UpdateUpgradeButton(string team, UpgradeType type, int level)
    {
        if (team != currentTeam) return;

        var upgradeButton = upgradeButtons.Find(ub => ub.type == type);
        if (upgradeButton == null) return;

        if (level >= 2)
        {
            upgradeButton.costText.text = $"{type}\nMAX";
            upgradeButton.button.interactable = false;
        }
        else
        {
            int cost = level == 0 ? 10 : 20;
            upgradeButton.costText.text = $"{type}\n{cost}";
            upgradeButton.button.interactable = economyManager.GetSupplyPoints(currentTeam) >= cost;
        }

        upgradeButton.levelText.text = $"Level: {level}";
    }

    private void UpdateButtonStates()
    {
        foreach (var upgradeButton in upgradeButtons)
        {
            if (upgradeButton.button != null)
            {
                bool canPurchase = economyManager.CanPurchaseUpgrade(currentTeam, upgradeButton.type);
                upgradeButton.button.interactable = canPurchase;
            }
        }
    }

    private void PurchaseUpgrade(UpgradeType type)
    {
        Debug.Log($"Attempting to purchase upgrade: {type}");
        Debug.Log($"Current team: {currentTeam}");
        Debug.Log($"Current supply points: {economyManager.GetSupplyPoints(currentTeam)}");
        
        if (economyManager.PurchaseUpgrade(currentTeam, type))
        {
            Debug.Log($"Successfully purchased {type} upgrade for {currentTeam}");
        }
        else
        {
            Debug.Log($"Failed to purchase {type} upgrade");
        }
    }
}