using UnityEngine;
using System;
using System.Collections;

public abstract class BaseUnit : MonoBehaviour
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

    protected UnitState currentState;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;

    private MovementSystem movementSystem;
    private CombatSystem combatSystem;

    public event Action<BaseUnit> OnUnitDeath;
    public event Action<BaseUnit> OnAbilityActivated;
    public event Action<BaseUnit> OnAbilityDeactivated;

    protected virtual void Start()
    {
        InitializeBaseStats();
        ApplyUpgrades();
        currentHealth = currentMaxHealth; // Use upgraded max health
        currentState = UnitState.Idle;

        // Get required components
        healthSystem = GetComponent<HealthSystem>();
        movementSystem = GetComponent<MovementSystem>();
        combatSystem = GetComponent<CombatSystem>();

        if (healthSystem != null)
        {
            healthSystem.Initialize(currentMaxHealth); // Use upgraded max health
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

        // Apply movement speed to MovementSystem
        if (movementSystem != null)
        {
            movementSystem.SetMoveSpeed(currentMoveSpeed);
        }

        Debug.Log($"Unit {gameObject.name} initialized with stats - Health: {currentMaxHealth}, Damage: {currentAttackDamage}, Speed: {currentMoveSpeed}, Attack Speed: {currentAttackSpeed}");
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
        Debug.Log($"[{teamId}] Unit {unitType} applying upgrades - Round {BattleRoundManager.Instance?.GetCurrentRound()}");
        if (EconomyManager.Instance == null) return;

        float armorMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Armor);
        float damageMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Training);
        float speedMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Speed);
        float attackSpeedMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.AttackSpeed);

        currentMaxHealth = maxHealth * armorMultiplier;
        currentAttackDamage = attackDamage * damageMultiplier;
        currentMoveSpeed = moveSpeed * speedMultiplier;
        currentAttackSpeed = attackSpeed * attackSpeedMultiplier;

        if (currentHealth > 0)
        {
            float healthPercentage = currentHealth / maxHealth;
            currentHealth = currentMaxHealth * healthPercentage;
            
            if (healthSystem != null)
            {
                healthSystem.Initialize(currentMaxHealth);
            }
        }

        // Update MovementSystem with new speed
        if (movementSystem != null)
        {
            movementSystem.SetMoveSpeed(currentMoveSpeed);
        }

        Debug.Log($"Upgrades applied to {gameObject.name} - New stats - Health: {currentMaxHealth}, Damage: {currentAttackDamage}, Speed: {currentMoveSpeed}, Attack Speed: {currentAttackSpeed}");
    }

    private void HandleUpgradePurchased(string team, UpgradeType type, int level)
    {
        if (team == teamId)
        {
            Debug.Log($"Handling upgrade purchase for {gameObject.name} - Type: {type}, Level: {level}");
            ApplyUpgrades();
        }
    }

    protected virtual void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
        }
    }

    protected virtual void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.BattleActive)
        {
            nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);
        }
    }

    protected virtual void Update()
    {
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
        isAbilityActive = true;
        OnAbilityActivated?.Invoke(this);
    }

    protected virtual void DeactivateAbility()
    {
        isAbilityActive = false;
        OnAbilityDeactivated?.Invoke(this);
    }

    public virtual void UpdateState(UnitState newState)
    {
        currentState = newState;
    }

    public virtual void TakeDamage(float damage)
    {
        if (currentState == UnitState.Dead) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        healthSystem?.TakeDamage(damage);
        
        if (currentHealth <= 0)
        {
            Die();
        }   
    }

    protected virtual void Die()
    {
        if (currentState == UnitState.Dead) return;
        
        currentState = UnitState.Dead;

        if (movementSystem != null)
        {
            movementSystem.StopMovement();
        }

        var enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyTargeting != null)
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
            Destroy(gameObject);
        }

        GameManager.Instance?.HandleUnitDeath(this);
    }

    private IEnumerator DeathSequence()
    {
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float elapsedTime = 0f;
            Color startColor = spriteRenderer.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsedTime < deathAnimationDuration)
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

        Destroy(gameObject);
    }

    public void SetTeam(string newTeamId)
    {
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

    // Getters that return upgraded stats
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