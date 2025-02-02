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

    protected UnitState currentState;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;

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

    private string[] GetAllLayerNames()
    {
        List<string> layers = new List<string>();
        for(int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if(!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
            }
        }
        return layers.ToArray();
    }

    public void SetTeam(string newTeamId)
    {
        Debug.Log($"SetTeam called on {gameObject.name} with team: {newTeamId}");
        teamId = newTeamId;
        
        // Match layer name exactly with team ID
        string layerName = newTeamId;  // Since we're using TeamA/TeamB consistently
        
        Debug.Log($"Attempting to set layer to: {layerName}");
        int layerIndex = LayerMask.NameToLayer(layerName);
        
        Debug.Log($"Layer index found: {layerIndex}");
        if (layerIndex != -1)
        {
            gameObject.layer = layerIndex;
            Debug.Log($"Successfully set {gameObject.name}'s layer to {layerName} (index: {layerIndex})");
        }
        else
        {
            Debug.LogError($"Failed to find layer: {layerName}. Available layers: {string.Join(", ", GetAllLayerNames())}");
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