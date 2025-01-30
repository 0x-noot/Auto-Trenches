using UnityEngine;
using System;
using System.Collections;

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
    [SerializeField] protected string teamId;  // Add this

    [Header("Death Settings")]
    [SerializeField] protected float deathAnimationDuration = 1f;
    [SerializeField] protected bool useDeathAnimation = true;

    protected UnitState currentState;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;

    // Event that other systems can subscribe to
    public event Action<BaseUnit> OnUnitDeath;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        currentState = UnitState.Idle;
        
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.Initialize(maxHealth);
        }
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
        Debug.Log($"[{gameObject.name}] Unit died. Team: {teamId}");

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

        // Notify GameManager about unit death
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

    // Add these new methods for team management
    public void SetTeam(string newTeamId)
    {
        teamId = newTeamId;
        Debug.Log($"[{gameObject.name}] Setting team to: {newTeamId}");
        
        // Convert team name to layer name (PlayerTeam -> PlayerLayer, EnemyTeam -> EnemyLayer)
        string layerName = newTeamId.Replace("Team", "Layer");
        int layerIndex = LayerMask.NameToLayer(layerName);
        
        if (layerIndex != -1)
        {
            gameObject.layer = layerIndex;
            Debug.Log($"[{gameObject.name}] Set layer to: {layerName} (index: {layerIndex})");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Failed to find layer: {layerName}");
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
}