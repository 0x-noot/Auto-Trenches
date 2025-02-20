using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;

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

        photonView.RPC("RPCApplyUpgrades", RpcTarget.All, armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    [PunRPC]
    protected virtual void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        if (!gameObject.activeInHierarchy) return;

        currentMaxHealth = maxHealth * armorMultiplier;
        currentAttackDamage = attackDamage * damageMultiplier;
        currentMoveSpeed = moveSpeed * speedMultiplier;
        currentAttackSpeed = attackSpeed * attackSpeedMultiplier;

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
        // Unsubscribe from events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
        }

        // Clear any ongoing effects or coroutines
        StopAllCoroutines();
        isAbilityActive = false;
        currentTarget = null;
        
        // Reset state
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
        }
    }

    protected virtual void ActivateAbility()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        photonView.RPC("RPCActivateAbility", RpcTarget.All);
    }

    [PunRPC]
    protected virtual void RPCActivateAbility()
    {
        if (!gameObject.activeInHierarchy) return;
        isAbilityActive = true;
        OnAbilityActivated?.Invoke(this);
    }

    protected virtual void DeactivateAbility()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        photonView.RPC("RPCDeactivateAbility", RpcTarget.All);
    }

    [PunRPC]
    protected virtual void RPCDeactivateAbility()
    {
        if (!gameObject.activeInHierarchy) return;
        isAbilityActive = false;
        OnAbilityDeactivated?.Invoke(this);
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
            photonView.RPC("RPCTakeDamage", RpcTarget.All, damage);
        }
        finally
        {
            isProcessingRPC = false;
        }
    }

    [PunRPC]
    protected virtual void RPCTakeDamage(float damage)
    {
        if (!gameObject.activeInHierarchy) return;
            
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        if (healthSystem != null && healthSystem.enabled)
        {
            healthSystem.TakeDamage(damage);
        }
        
        if (currentHealth <= 0 && currentState != UnitState.Dead)
        {
            Die();
        }   
    }

    public virtual void UpdateState(UnitState newState)
    {
        if (!photonView.IsMine || !PhotonNetwork.IsMessageQueueRunning) return;
        photonView.RPC("RPCUpdateState", RpcTarget.All, (int)newState);
    }

    [PunRPC]
    protected virtual void RPCUpdateState(int newState)
    {
        if (!gameObject.activeInHierarchy) return;
        currentState = (UnitState)newState;
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

        if (PhotonNetwork.IsMasterClient && gameObject.activeInHierarchy)
        {
            PhotonNetwork.Destroy(gameObject);
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
        else
        {
            Debug.LogError($"Failed to find layer: {layerName}");
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send unit data
            stream.SendNext(currentHealth);
            stream.SendNext((int)currentState);
            stream.SendNext(isAbilityActive);
            stream.SendNext(teamId);
        }
        else
        {
            // Receive unit data
            currentHealth = (float)stream.ReceiveNext();
            currentState = (UnitState)stream.ReceiveNext();
            isAbilityActive = (bool)stream.ReceiveNext();
            teamId = (string)stream.ReceiveNext();

            // Update healthbar if needed
            if (healthSystem != null && healthSystem.enabled)
            {
                healthSystem.TakeDamage(0); // Force healthbar update
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