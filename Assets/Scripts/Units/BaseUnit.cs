using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public abstract class BaseUnit : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Base Stats")]
    // Initialize to avoid default value
    protected UnitType unitType = UnitType.Berserker;
    protected float baseHealth;
    protected float baseDamage;
    protected float baseAttackSpeed;
    protected float baseMoveSpeed;
    protected float attackRange;

    [Header("Order Settings")]
    [SerializeField] protected OrderType orderType = OrderType.None;

    // Current stats
    protected float maxHealth;
    protected float currentHealth;
    protected float attackDamage;
    protected float attackSpeed;
    protected float moveSpeed;

    private float abilityCheckTimer = 0f;
    private const float ABILITY_CHECK_INTERVAL = 1.5f;

    [Header("Team Settings")]
    [SerializeField] protected string teamId;

    [Header("Death Settings")]
    [SerializeField] protected float deathAnimationDuration = 1f;
    [SerializeField] protected bool useDeathAnimation = true;

    [Header("Ability Settings")]
    [SerializeField] protected float baseAbilityCooldown = 15f;
    [SerializeField] protected float abilityChance = 0.05f;
    protected bool isAbilityActive = false;
    protected float nextAbilityTime = 0f;

    protected UnitState currentState = UnitState.Idle;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;
    protected MovementSystem movementSystem;
    protected CombatSystem combatSystem;
    protected bool isProcessingRPC = false;
    protected bool isInitialized = false;

    // Network optimization
    private float lastStateUpdateTime = 0f;
    private const float STATE_UPDATE_INTERVAL = 0.2f; // 5 updates per second max
    protected bool isDirty = false;

    // Track synergy bonuses
    protected Dictionary<string, float> synergyBonuses = new Dictionary<string, float>();

    public event Action<BaseUnit> OnUnitDeath;
    public event Action<BaseUnit> OnAbilityActivated;
    public event Action<BaseUnit> OnAbilityDeactivated;

    // Force initialization before other components
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeUnitTypes()
    {
        // Just forcing code to initialize the enum
        Debug.Log("Initializing unit type system");
        UnitType[] types = (UnitType[])Enum.GetValues(typeof(UnitType));
    }
    
    protected virtual void Awake()
    {
        // Get components
        healthSystem = GetComponent<HealthSystem>();
        movementSystem = GetComponent<MovementSystem>();
        combatSystem = GetComponent<CombatSystem>();

        if (healthSystem == null) Debug.LogError($"Missing HealthSystem on {gameObject.name}");
        if (movementSystem == null) Debug.LogError($"Missing MovementSystem on {gameObject.name}");
        if (combatSystem == null) Debug.LogError($"Missing CombatSystem on {gameObject.name}");
        
        // Debug the unit type for troubleshooting
        Debug.Log($"BaseUnit.Awake: {gameObject.name} has unitType={unitType}, orderType={orderType}");
    }

    protected virtual void Start()
    {
        if (!photonView.IsMine) return;

        Debug.Log($"BaseUnit.Start: {gameObject.name} has unitType={unitType}, orderType={orderType}");
        InitializeBaseStats();
        ApplyDefaultStats();
        if (!isInitialized)
        {
            InitializeUnit();
        }
        
        // Register with OrderSystem if applicable
        if (orderType != OrderType.None && OrderSystem.Instance != null)
        {
            OrderSystem.Instance.RegisterUnit(this);
        }
    }

    private void InitializeUnit()
    {
        if (isInitialized) return;

        currentHealth = maxHealth;
        currentState = UnitState.Idle;

        // Initialize health system
        if (healthSystem != null)
        {
            healthSystem.Initialize(maxHealth);
        }

        if (movementSystem != null)
        {
            movementSystem.SetMoveSpeed(moveSpeed);
        }

        nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);

        // Subscribe to events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        isInitialized = true;
        isDirty = true;
    }

    protected virtual void InitializeBaseStats()
    {
        baseHealth = maxHealth;
        baseDamage = attackDamage;
        baseAttackSpeed = attackSpeed;
        baseMoveSpeed = moveSpeed;
    }
    
    // Apply default stats without any multipliers
    public void ApplyDefaultStats(float armorMultiplier = 1.0f, float damageMultiplier = 1.0f, 
        float speedMultiplier = 1.0f, float attackSpeedMultiplier = 1.0f)
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        
        photonView.RPC("RPCApplyUpgrades", RpcTarget.All, 
            armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    [PunRPC]
    protected virtual void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, 
        float speedMultiplier, float attackSpeedMultiplier)
    {
        if (!gameObject.activeInHierarchy) return;

        // Apply multipliers directly to base stats
        maxHealth = baseHealth * armorMultiplier;
        attackDamage = baseDamage * damageMultiplier;
        moveSpeed = baseMoveSpeed * speedMultiplier;
        attackSpeed = baseAttackSpeed * attackSpeedMultiplier;

        // Update current health proportionally
        if (currentHealth > 0)
        {
            float healthPercentage = currentHealth / baseHealth;
            currentHealth = maxHealth * healthPercentage;
            
            if (healthSystem != null && healthSystem.enabled)
            {
                healthSystem.Initialize(maxHealth);
            }
        }

        if (movementSystem != null && movementSystem.enabled)
        {
            movementSystem.SetMoveSpeed(moveSpeed);
        }

        isDirty = true;
    }

    protected virtual void OnDestroy()
    {
        CleanupUnit();
        
        // Unregister from OrderSystem if applicable
        if (orderType != OrderType.None && OrderSystem.Instance != null)
        {
            OrderSystem.Instance.UnregisterUnit(this);
        }
    }

    public virtual void OnDisable()
    {
        CleanupUnit();
    }

    private void CleanupUnit()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        StopAllCoroutines();
        isAbilityActive = false;
        currentTarget = null;
        currentState = UnitState.Idle;
        isProcessingRPC = false;
    }

    protected virtual void HandleGameStateChanged(GameState newState)
    {
        if (!gameObject.activeInHierarchy) return;

        if (newState == GameState.BattleActive)
        {
            nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);
        }
        else if (newState == GameState.BattleEnd || newState == GameState.GameOver)
        {
            // Clean up when battle ends
            if (isAbilityActive)
            {
                DeactivateAbility();
            }
            StopAllCoroutines();
        }

        isDirty = true;
    }

    protected virtual void Update()
    {
        if (!photonView.IsMine || !gameObject.activeInHierarchy) return;

        abilityCheckTimer += Time.deltaTime;
        if (GameManager.Instance != null && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive && 
            currentState == UnitState.Attacking &&
            currentState != UnitState.Dead && 
            abilityCheckTimer >= ABILITY_CHECK_INTERVAL)
        {
            abilityCheckTimer = 0f;
            TryActivateAbility();
        }
    }

    protected virtual void TryActivateAbility()
    {
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            ActivateAbility();
            nextAbilityTime = Time.time + baseAbilityCooldown;
            isDirty = true;
        }
    }

    protected virtual void ActivateAbility()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        
        // Set ability active locally and notify subclasses
        isAbilityActive = true;
        OnAbilityActivated?.Invoke(this);
        isDirty = true;
        
        // Perform subclass-specific activation
        PerformAbilityActivation();
        
        // Sync via custom properties instead of RPC
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { "AbilityActive", true }
        };
        photonView.Owner.SetCustomProperties(properties);
    }
    [PunRPC]
    private void RPC_ActivateAbilityOnAll()
    {
        if (!gameObject.activeInHierarchy) return;
        isAbilityActive = true;
        OnAbilityActivated?.Invoke(this);
        isDirty = true;
        
        // Call the type-specific ability activation
        PerformAbilityActivation();
    }

    [PunRPC]
    protected virtual void RPCActivateAbility()
    {
        if (!gameObject.activeInHierarchy) return;
        isAbilityActive = true;
        OnAbilityActivated?.Invoke(this);
        isDirty = true;
    }

    protected virtual void PerformAbilityActivation()
    {
        // Base implementation does nothing
    }

    protected virtual void DeactivateAbility()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        
        isAbilityActive = false;
        OnAbilityDeactivated?.Invoke(this);
        
        // Sync via custom properties instead of RPC
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { "AbilityActive", false }
        };
        photonView.Owner.SetCustomProperties(properties);
    }

    [PunRPC]
    protected virtual void RPCDeactivateAbility()
    {
        if (!gameObject.activeInHierarchy) return;
        isAbilityActive = false;
        OnAbilityDeactivated?.Invoke(this);
        isDirty = true;
    }

    public virtual void TakeDamage(float damage)
    {
        if (!photonView.IsMine || !gameObject.activeInHierarchy || isProcessingRPC) return;
        
        // Prevent RPC during scene transitions/cleanup
        if (!PhotonNetwork.IsMessageQueueRunning)
            return;

        try
        {
            isProcessingRPC = true;
            
            // Apply damage directly without calling healthsystem's RPC
            currentHealth = Mathf.Max(0, currentHealth - damage);
            
            // Send RPCStateDamage to sync state, not RPCTakeDamage
            photonView.RPC("RPCStateDamage", RpcTarget.Others, currentHealth);
            
            // Update healthbar directly
            if (healthSystem != null && healthSystem.enabled)
            {
                healthSystem.SetHealth(currentHealth, maxHealth);
            }
            
            if (currentHealth <= 0 && currentState != UnitState.Dead)
            {
                Die();
            }
            
            // Mark as dirty for sync
            isDirty = true;
        }
        finally
        {
            isProcessingRPC = false;
        }
    }
    [PunRPC]
    protected virtual void RPCStateDamage(float newHealth)
    {
        if (!gameObject.activeInHierarchy) return;
            
        currentHealth = newHealth;
        
        if (healthSystem != null && healthSystem.enabled)
        {
            healthSystem.SetHealth(currentHealth, maxHealth);
        }
        
        if (currentHealth <= 0 && currentState != UnitState.Dead)
        {
            Die();
        }
        
        isDirty = true;
    }

    public virtual void UpdateState(UnitState newState)
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        
        // Throttle state updates to reduce network traffic
        if (Time.time - lastStateUpdateTime < STATE_UPDATE_INTERVAL && newState == currentState)
            return;

        lastStateUpdateTime = Time.time;
        photonView.RPC("RPCUpdateState", RpcTarget.All, (int)newState);
    }

    [PunRPC]
    protected virtual void RPCUpdateState(int newState)
    {
        if (!gameObject.activeInHierarchy) return;
        currentState = (UnitState)newState;
        isDirty = true;
    }

    protected virtual void Die()
    {
        if (currentState == UnitState.Dead || !gameObject.activeInHierarchy) return;
        
        if (photonView.IsMine && PhotonNetwork.IsMessageQueueRunning)
        {
            photonView.RPC("RPCDie", RpcTarget.All);
        }
    }

    [PunRPC]
    protected virtual void RPCDie()
    {
        if (!gameObject.activeInHierarchy) return;

        currentState = UnitState.Dead;

        if (movementSystem != null && movementSystem.enabled)
        {
            movementSystem.StopMovement();
        }

        var enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyTargeting != null && enemyTargeting.enabled)
        {
            enemyTargeting.StopTargeting();
        }

        OnUnitDeath?.Invoke(this);

        if (useDeathAnimation)
        {
            StartCoroutine(DeathSequence());
        }
        else
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleUnitDeath(this);
        }

        isDirty = true;
    }

    private IEnumerator DeathSequence()
    {
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            if (collider != null && collider.enabled)
                collider.enabled = false;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float elapsedTime = 0f;
            Color startColor = spriteRenderer.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsedTime < deathAnimationDuration && gameObject.activeInHierarchy)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / deathAnimationDuration;
                spriteRenderer.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(deathAnimationDuration);
        }

        // IMPORTANT: Only the owner or MasterClient should destroy network objects
        if (gameObject.activeInHierarchy)
        {
            // Check if this client can destroy the object
            bool canDestroy = PhotonNetwork.IsMasterClient || photonView.IsMine;
            
            // If we can't destroy it, just disable the object
            if (!canDestroy)
            {
                gameObject.SetActive(false);
            }
            else if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
    }

    public void SetTeam(string newTeamId)
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        photonView.RPC("RPCSetTeam", RpcTarget.All, newTeamId);
    }

    [PunRPC]
    protected void RPCSetTeam(string newTeamId)
    {
        if (!gameObject.activeInHierarchy) return;

        teamId = newTeamId;
        string layerName = newTeamId;
        int layerIndex = LayerMask.NameToLayer(layerName);
        
        if (layerIndex != -1)
        {
            gameObject.layer = layerIndex;
        }

        isDirty = true;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
        
        // Safety check - photonView might be null if object is being destroyed
        if (this == null || !this.gameObject || !this.enabled || photonView == null)
            return;
        
        // Check if this update is for our owner and contains ability state
        if (targetPlayer == photonView.Owner && changedProps.ContainsKey("AbilityActive"))
        {
            bool abilityState = (bool)changedProps["AbilityActive"];
            
            if (abilityState && !isAbilityActive)
            {
                // Only update if state is changing
                isAbilityActive = true;
                OnAbilityActivated?.Invoke(this);
                PerformAbilityActivation();
            }
            else if (!abilityState && isAbilityActive)
            {
                // Deactivate ability
                isAbilityActive = false;
                OnAbilityDeactivated?.Invoke(this);
            }
        }
    }
    
    // Apply a synergy bonus to a specific stat
    [PunRPC]
    protected virtual void RPCApplySynergyBonus(string orderName, string statName, float bonusMultiplier)
    {
        string bonusKey = $"{orderName}_{statName}";
        
        // Store the bonus for tracking
        synergyBonuses[bonusKey] = bonusMultiplier;
        
        // Apply the bonus based on the stat
        switch (statName.ToLower())
        {
            case "health":
                float healthBonus = maxHealth * bonusMultiplier;
                maxHealth += healthBonus;
                currentHealth += healthBonus;
                
                // Update health system if available
                if (healthSystem != null && healthSystem.enabled)
                {
                    healthSystem.Initialize(maxHealth);
                }
                break;
                
            case "damage":
                float damageBonus = attackDamage * bonusMultiplier;
                attackDamage += damageBonus;
                break;
                
            case "attackspeed":
                // For attack speed, we directly add the value rather than using a percentage
                attackSpeed += bonusMultiplier;
                break;
                
            case "movespeed":
                float speedBonus = moveSpeed * bonusMultiplier;
                moveSpeed += speedBonus;
                
                // Update movement system if available
                if (movementSystem != null && movementSystem.enabled)
                {
                    movementSystem.SetMoveSpeed(moveSpeed);
                }
                break;
                
            case "abilitychance":
                // For ability chance, we directly add the percentage points
                abilityChance += bonusMultiplier;
                break;
        }
        
        Debug.Log($"{gameObject.name} received {bonusMultiplier:P0} {orderName} synergy bonus to {statName}");
        
        // Mark as dirty for sync
        isDirty = true;
    }

    // Remove a synergy bonus
    [PunRPC]
    protected virtual void RPCRemoveSynergyBonus(string orderName, string statName)
    {
        string bonusKey = $"{orderName}_{statName}";
        
        // If we don't have this bonus stored, return
        if (!synergyBonuses.ContainsKey(bonusKey))
            return;
            
        float bonusMultiplier = synergyBonuses[bonusKey];
        
        // Remove the bonus based on the stat
        switch (statName.ToLower())
        {
            case "health":
                float healthBonus = maxHealth * bonusMultiplier;
                maxHealth -= healthBonus;
                currentHealth = Mathf.Min(currentHealth, maxHealth);
                
                // Update health system if available
                if (healthSystem != null && healthSystem.enabled)
                {
                    healthSystem.Initialize(maxHealth);
                }
                break;
                
            case "damage":
                float damageBonus = attackDamage * bonusMultiplier;
                attackDamage -= damageBonus;
                break;
                
            case "attackspeed":
                // For attack speed, we directly subtract the value
                attackSpeed -= bonusMultiplier;
                break;
                
            case "movespeed":
                float speedBonus = moveSpeed * bonusMultiplier;
                moveSpeed -= speedBonus;
                
                // Update movement system if available
                if (movementSystem != null && movementSystem.enabled)
                {
                    movementSystem.SetMoveSpeed(moveSpeed);
                }
                break;
                
            case "abilitychance":
                // For ability chance, we directly subtract the percentage points
                abilityChance -= bonusMultiplier;
                break;
        }
        
        // Remove the stored bonus
        synergyBonuses.Remove(bonusKey);
        
        Debug.Log($"{gameObject.name} removed {bonusMultiplier:P0} {orderName} synergy bonus from {statName}");
        
        // Mark as dirty for sync
        isDirty = true;
    }

    // Public method to apply a synergy bonus (calls RPC)
    public void ApplySynergyBonus(string orderName, string statName, float bonusMultiplier)
    {
        if (!photonView.IsMine) return;
        
        photonView.RPC("RPCApplySynergyBonus", RpcTarget.All, orderName, statName, bonusMultiplier);
    }

    // Public method to remove a synergy bonus (calls RPC)
    public void RemoveSynergyBonus(string orderName, string statName)
    {
        if (!photonView.IsMine) return;
        
        photonView.RPC("RPCRemoveSynergyBonus", RpcTarget.All, orderName, statName);
    }

    // Check if unit has lower than 50% health (for Wild synergy)
    public bool IsLowHealth()
    {
        return currentHealth < (maxHealth * 0.5f);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Writing data
            stream.SendNext(currentHealth);
            stream.SendNext((int)currentState);
            stream.SendNext(isAbilityActive);
            stream.SendNext((int)orderType);
            // Don't need to send teamId - it won't change during battle
        }
        else
        {
            // Reading data
            currentHealth = (float)stream.ReceiveNext();
            currentState = (UnitState)stream.ReceiveNext();
            isAbilityActive = (bool)stream.ReceiveNext();
            orderType = (OrderType)stream.ReceiveNext();
            
            // Update health display
            if (healthSystem != null && healthSystem.enabled)
            {
                healthSystem.SetHealth(currentHealth, maxHealth);
            }
        }
    }

    // Getters
    public string GetTeamId() => teamId;
    public virtual UnitState GetCurrentState() => currentState;
    public virtual float GetAttackRange() => attackRange;
    
    public virtual float GetAttackDamage()
    {
        float baseDamage = attackDamage;
        
        // Apply Wild synergy (bonus damage when below 50% health)
        if (orderType == OrderType.Wild && IsLowHealth() && synergyBonuses.ContainsKey("Wild_lowHealthDamage"))
        {
            float bonusMultiplier = synergyBonuses["Wild_lowHealthDamage"];
            baseDamage *= (1 + bonusMultiplier);
        }
        
        // Apply Arcane synergy (bonus damage to affected targets)
        if (orderType == OrderType.Arcane && 
            currentTarget != null && 
            currentTarget.IsAbilityActive() &&
            synergyBonuses.ContainsKey("Arcane_affectedTargetDamage"))
        {
            float bonusMultiplier = synergyBonuses["Arcane_affectedTargetDamage"];
            baseDamage *= (1f + bonusMultiplier);
        }
        
        return baseDamage;
    }
    
    public virtual float GetAttackSpeed() => attackSpeed;
    public virtual float GetMoveSpeed() => moveSpeed;
    public virtual UnitType GetUnitType() => unitType;
    public OrderType GetOrderType() => orderType;
    public float GetDeathAnimationDuration() => deathAnimationDuration;
    public bool IsAbilityActive() => isAbilityActive;
}