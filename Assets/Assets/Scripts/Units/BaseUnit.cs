using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseUnit : MonoBehaviour
{
    protected UnitType unitType;
    protected float maxHealth;
    protected float currentHealth;
    protected float attackDamage;
    protected float attackSpeed;
    protected float attackRange;
    protected float moveSpeed;
    
    [Header("Team Settings")]
    [SerializeField] protected string teamId;

    [Header("Death Settings")]
    [SerializeField] protected float deathAnimationDuration = 1f;
    [SerializeField] protected bool useDeathAnimation = true;

    [Header("Ability Settings")]
    [SerializeField] protected float baseAbilityCooldown = 15f;
    [SerializeField] protected float abilityChance = 0.2f; // 20% chance to trigger ability
    protected bool isAbilityActive = false;
    protected float nextAbilityTime = 0f;

    protected UnitState currentState;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;

    public event Action<BaseUnit> OnUnitDeath;
    public event Action<BaseUnit> OnAbilityActivated;
    public event Action<BaseUnit> OnAbilityDeactivated;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        currentState = UnitState.Idle;
        
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.Initialize(maxHealth);
        }

        // Initialize ability cooldown
        nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);

        // Subscribe to game state changes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    protected virtual void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    protected virtual void HandleGameStateChanged(GameState newState)
    {
        // Reset ability cooldown when battle starts
        if (newState == GameState.BattleActive)
        {
            nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);
        }
    }

    protected virtual void Update()
    {
        // Only check for ability activation during battle and when attacking
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

        var movementSystem = GetComponent<MovementSystem>();
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

    public string GetTeamId() => teamId;
    public virtual UnitState GetCurrentState() => currentState;
    public virtual float GetAttackRange() => attackRange;
    public virtual float GetAttackDamage() => attackDamage;
    public virtual float GetAttackSpeed() => attackSpeed;
    public virtual float GetMoveSpeed() => moveSpeed;
    public virtual UnitType GetUnitType() => unitType;
    public float GetDeathAnimationDuration() => deathAnimationDuration;
    public bool IsAbilityActive() => isAbilityActive;
}