using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Photon.Pun;

public class OrderSynergyUI : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class OrderUIElement
    {
        public OrderType orderType;
        public GameObject panel;
        public Image iconImage;
        public TextMeshProUGUI countText;
        public TextMeshProUGUI descriptionText;
        public Image backgroundImage;
        [HideInInspector] public bool isActive = false;
    }

    [Header("UI References")]
    [SerializeField] private GameObject synergyPanel;
    [SerializeField] private List<OrderUIElement> orderElements;
    
    [Header("Visual Settings")]
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private Color activeColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float activationAnimDuration = 0.5f;
    
    [Header("Order Descriptions")]
    [SerializeField] private string shieldOrderDescription = "Shield units gain +15% health, +10% ability trigger chance";
    [SerializeField] private string wildOrderDescription = "Wild units gain +0.2 attack speed, +15% damage when below 50% HP";
    [SerializeField] private string arcaneOrderDescription = "Arcane units deal +15% damage to targets affected by abilities, abilities leave lingering effects";
    [SerializeField] private string realmOrderDescription = "Each additional Militia grants +15% health and damage to all Militia";
    
    private string currentTeam;
    private Dictionary<OrderType, int> unitCounts = new Dictionary<OrderType, int>();
    private Animator panelAnimator;

    private void Start()
    {
        // Set current team based on player's network role
        currentTeam = PhotonNetwork.IsMasterClient ? "TeamA" : "TeamB";
        
        // Initialize order counts
        foreach (OrderType orderType in System.Enum.GetValues(typeof(OrderType)))
        {
            if (orderType != OrderType.None)
            {
                unitCounts[orderType] = 0;
            }
        }
        
        // Initialize UI elements
        InitializeOrderDescriptions();
        
        // Get panel animator if any
        panelAnimator = synergyPanel.GetComponent<Animator>();
        
        // Subscribe to order system events
        if (OrderSystem.Instance != null)
        {
            OrderSystem.Instance.OnOrderCountChanged += HandleOrderCountChanged;
            OrderSystem.Instance.OnSynergyActivated += HandleSynergyActivated;
        }
        
        // Subscribe to game state events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            
            // Force check game state on start
            GameState currentState = GameManager.Instance.GetCurrentState();
            HandleGameStateChanged(currentState);
        }
    }

    private void Update()
    {
        // Keep checking to make sure panel is active when it should be
        if (GameManager.Instance != null)
        {
            GameState currentState = GameManager.Instance.GetCurrentState();
            bool shouldBeActive = (currentState == GameState.BattleActive || 
                                currentState == GameState.BattleStart ||
                                currentState == GameState.PlayerAPlacement ||
                                currentState == GameState.PlayerBPlacement);
                                
            if (shouldBeActive && !synergyPanel.activeSelf)
            {
                synergyPanel.SetActive(true);
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (OrderSystem.Instance != null)
        {
            OrderSystem.Instance.OnOrderCountChanged -= HandleOrderCountChanged;
            OrderSystem.Instance.OnSynergyActivated -= HandleSynergyActivated;
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void InitializeOrderDescriptions()
    {
        foreach (OrderUIElement element in orderElements)
        {
            // Set description text
            if (element.descriptionText != null)
            {
                switch (element.orderType)
                {
                    case OrderType.Shield:
                        element.descriptionText.text = shieldOrderDescription;
                        break;
                    case OrderType.Wild:
                        element.descriptionText.text = wildOrderDescription;
                        break;
                    case OrderType.Arcane:
                        element.descriptionText.text = arcaneOrderDescription;
                        break;
                    case OrderType.Realm:
                        element.descriptionText.text = realmOrderDescription;
                        break;
                }
            }
            
            // Set initial count to 0
            if (element.countText != null)
            {
                element.countText.text = "0";
            }
            
            // Set initial color to inactive
            if (element.backgroundImage != null)
            {
                element.backgroundImage.color = inactiveColor;
            }
            
            // Always show the panel
            if (element.panel != null)
            {
                element.panel.SetActive(true);
            }
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        // Show during both battle AND placement
        bool shouldShow = (newState == GameState.BattleActive || 
                        newState == GameState.BattleStart ||
                        newState == GameState.PlayerAPlacement ||
                        newState == GameState.PlayerBPlacement);
        
        // Force show based on our rules, not hiding on state change
        if (!synergyPanel.activeSelf && shouldShow)
        {
            synergyPanel.SetActive(true);
            
            // Trigger animation if available
            if (panelAnimator != null)
            {
                panelAnimator.SetTrigger("Show");
            }
        }
        
        // Add failsafe activation
        if (!synergyPanel.activeSelf && shouldShow)
        {
            // Double-check after 0.5s in case another script deactivates it
            Invoke("ForceActivatePanel", 0.5f);
        }
    }

    private void ForceActivatePanel()
    {
        if (!synergyPanel.activeSelf)
        {
            synergyPanel.SetActive(true);
            Debug.Log("OrderSynergyUI: Forced panel activation");
        }
    }

    private void HandleOrderCountChanged(string team, OrderType orderType, int count)
    {
        // Only process for current player's team
        if (team != currentTeam) return;
        
        // Update internal count
        unitCounts[orderType] = count;
        
        // Find matching UI element
        OrderUIElement element = orderElements.Find(e => e.orderType == orderType);
        if (element == null) return;
        
        // Update count text
        if (element.countText != null)
        {
            element.countText.text = count.ToString();
        }
        
        // Show/hide panel based on count
        if (element.panel != null)
        {
            bool shouldShow = count > 0;
            if (element.panel.activeSelf != shouldShow)
            {
                element.panel.SetActive(shouldShow);
            }
        }
    }

    private void HandleSynergyActivated(string team, OrderType orderType, int count, bool isActivated)
    {
        // Only process for current player's team
        if (team != currentTeam) return;
        
        // Skip if panel is inactive
        if (!synergyPanel.activeSelf) return;
        
        // Find matching UI element
        OrderUIElement element = orderElements.Find(e => e.orderType == orderType);
        if (element == null) return;
        
        // Skip if already in the correct state
        if (element.isActive == isActivated) return;
        
        // Update active state
        element.isActive = isActivated;
        
        // Update visuals
        if (element.backgroundImage != null)
        {
            if (isActivated)
            {
                // Start animation to active color
                StartCoroutine(AnimateColor(element.backgroundImage, inactiveColor, activeColor, activationAnimDuration));
                
                // Flash highlight
                StartCoroutine(FlashHighlight(element.backgroundImage));
            }
            else
            {
                // Start animation to inactive color
                StartCoroutine(AnimateColor(element.backgroundImage, activeColor, inactiveColor, activationAnimDuration));
            }
        }
        
        // Update text color
        if (element.countText != null)
        {
            element.countText.color = isActivated ? highlightColor : Color.white;
        }
        
        if (element.descriptionText != null)
        {
            element.descriptionText.color = isActivated ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            element.descriptionText.fontStyle = isActivated ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private System.Collections.IEnumerator AnimateColor(Image image, Color startColor, Color endColor, float duration)
    {
        float elapsedTime = 0;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            image.color = Color.Lerp(startColor, endColor, t);
            
            yield return null;
        }
        
        image.color = endColor;
    }

    private System.Collections.IEnumerator FlashHighlight(Image image)
    {
        Color originalColor = image.color;
        
        // Flash to highlight color
        image.color = highlightColor;
        
        // Wait a moment
        yield return new WaitForSeconds(0.2f);
        
        // Return to active color
        image.color = activeColor;
    }

    public void ShowSynergyPanel(bool show)
    {
        synergyPanel.SetActive(show);
    }
}