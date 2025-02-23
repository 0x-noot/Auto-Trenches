using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

public abstract class BaseUnit : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Base Stats")]
    protected UnitType unitType;
    protected float baseHealth;
    protected float baseDamage;
    protected float baseAttackSpeed;
    protected float baseMoveSpeed;
    protected float attackRange;

    // Current stats
    protected float maxHealth;
    protected float currentHealth;
    protected float attackDamage;
    protected float attackSpeed;
    protected float moveSpeed;

    // Stats with upgrades applied
    protected float currentMaxHealth;
    protected float currentAttackDamage;
    protected float currentAttackSpeed;
    protected float currentMoveSpeed;

    [Header("Team Settings")]
    [SerializeField] protected string teamId;

    [Header("Death Settings")]
    [SerializeField] protected float deathAnimationDuration = 1f;
    [SerializeField] protected bool useDeathAnimation = true;

    [Header("Ability Settings")]
    [SerializeField] protected float baseAbilityCooldown = 15f;
    [SerializeField] protected float abilityChance = 0.2f;
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
    private bool isDirty = false;

    public event Action<BaseUnit> OnUnitDeath;
    public event Action<BaseUnit> OnAbilityActivated;
    public event Action<BaseUnit> OnAbilityDeactivated;

    protected virtual void Awake()
    {
        // Get components
        healthSystem = GetComponent<HealthSystem>();
        movementSystem = GetComponent<MovementSystem>();
        combatSystem = GetComponent<CombatSystem>();

        if (healthSystem == null) Debug.LogError($"Missing HealthSystem on {gameObject.name}");
        if (movementSystem == null) Debug.LogError($"Missing MovementSystem on {gameObject.name}");
        if (combatSystem == null) Debug.LogError($"Missing CombatSystem on {gameObject.name}");
    }

    protected virtual void Start()
    {
        if (!photonView.IsMine) return;

        InitializeBaseStats();
        ApplyUpgrades();
        if (!isInitialized)
        {
            InitializeUnit();
        }
    }

    private void InitializeUnit()
    {
        if (isInitialized) return;

        currentHealth = currentMaxHealth;
        currentState = UnitState.Idle;

        // Initialize health system
        if (healthSystem != null)
        {
            healthSystem.Initialize(currentMaxHealth);
        }

        if (movementSystem != null)
        {
            movementSystem.SetMoveSpeed(currentMoveSpeed);
        }

        nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);

        // Subscribe to events
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
        }

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

    protected void ApplyUpgrades()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        
        if (EconomyManager.Instance == null) return;

        float armorMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Armor);
        float damageMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Training);
        float speedMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Speed);
        float attackSpeedMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.AttackSpeed);

        photonView.RPC("RPCApplyUpgrades", RpcTarget.All, 
            armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    [PunRPC]
    protected virtual void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, 
        float speedMultiplier, float attackSpeedMultiplier)
    {
        if (!gameObject.activeInHierarchy) return;

        currentMaxHealth = maxHealth * armorMultiplier;
        currentAttackDamage = attackDamage * damageMultiplier;
        currentMoveSpeed = moveSpeed * speedMultiplier;
        currentAttackSpeed = attackSpeed * attackSpeedMultiplier;

        // Update current health proportionally
        if (currentHealth > 0)
        {
            float healthPercentage = currentHealth / maxHealth;
            currentHealth = currentMaxHealth * healthPercentage;
            
            if (healthSystem != null && healthSystem.enabled)
            {
                healthSystem.Initialize(currentMaxHealth);
            }
        }

        if (movementSystem != null && movementSystem.enabled)
        {
            movementSystem.SetMoveSpeed(currentMoveSpeed);
        }

        isDirty = true;
    }

    private void HandleUpgradePurchased(string team, UpgradeType type, int level)
    {
        if (team == teamId && photonView.IsMine && gameObject.activeInHierarchy)
        {
            ApplyUpgrades();
        }
    }

    protected virtual void OnDestroy()
    {
        CleanupUnit();
    }

    private void OnDisable()
    {
        CleanupUnit();
    }

    private void CleanupUnit()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
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

        if (GameManager.Instance != null && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive && 
            currentState == UnitState.Attacking &&
            currentState != UnitState.Dead && 
            Time.time >= nextAbilityTime)
        {
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
                healthSystem.SetHealth(currentHealth, currentMaxHealth);
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
            healthSystem.SetHealth(currentHealth, currentMaxHealth);
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

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Only send critical unit data, minimize bandwidth
            stream.SendNext(currentHealth);
            stream.SendNext((int)currentState);
            stream.SendNext(isAbilityActive);
            // Don't need to send teamId - it won't change during battle
        }
        else
        {
            // Receive unit data
            float newHealth = (float)stream.ReceiveNext();
            UnitState newState = (UnitState)stream.ReceiveNext();
            bool newAbilityState = (bool)stream.ReceiveNext();
            
            // Only update health if it changed significantly
            if (Mathf.Abs(currentHealth - newHealth) > 0.1f)
            {
                currentHealth = newHealth;
                if (healthSystem != null && healthSystem.enabled)
                {
                    healthSystem.TakeDamage(0); // Force healthbar update
                }
            }
            
            // Only update state if it changed
            if (currentState != newState)
            {
                currentState = newState;
            }
            
            // Only update ability state if it changed
            if (isAbilityActive != newAbilityState)
            {
                isAbilityActive = newAbilityState;
            }
        }
    }

    // Getters
    public string GetTeamId() => teamId;
    public virtual UnitState GetCurrentState() => currentState;
    public virtual float GetAttackRange() => attackRange;
    public virtual float GetAttackDamage() => currentAttackDamage;
    public virtual float GetAttackSpeed() => currentAttackSpeed;
    public virtual float GetMoveSpeed() => currentMoveSpeed;
    public virtual UnitType GetUnitType() => unitType;
    public float GetDeathAnimationDuration() => deathAnimationDuration;
    public bool IsAbilityActive() => isAbilityActive;
}