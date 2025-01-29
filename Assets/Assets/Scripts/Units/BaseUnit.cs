using UnityEngine;

public enum UnitType
{
    Fighter,
    Mage,
    Range,
    Tank
}

public enum UnitState
{
    Idle,
    Moving,
    Attacking,
    Dead
}

public class BaseUnit : MonoBehaviour
{
    [Header("Unit Properties")]
    [SerializeField] protected UnitType unitType;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float attackDamage = 10f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] protected float attackRange = 1f;
    [SerializeField] protected float moveSpeed = 3f;

    protected float currentHealth;
    protected UnitState currentState;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        currentState = UnitState.Idle;
        
        // Initialize health system
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.Initialize(maxHealth);
        }
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        healthSystem?.TakeDamage(damage);
        
        if (currentHealth <= 0)
        {
            Die();
        }   
    }

    protected virtual void Die()
    {
        currentState = UnitState.Dead;
        // Will implement death handling later
    }

    public virtual void UpdateState(UnitState newState)
    {
        currentState = newState;
    }

    public virtual UnitState GetCurrentState()
    {
        return currentState;
    }

    // Make all getter methods virtual so they can be properly overridden
    public virtual float GetAttackRange()
    {
        Debug.Log($"[{gameObject.name}] Getting attack range from BaseUnit: {attackRange}");
        return attackRange;
    }

    public virtual float GetAttackDamage()
    {
        return attackDamage;
    }

    public virtual float GetAttackSpeed()
    {
        return attackSpeed;
    }

    public virtual float GetMoveSpeed()
    {
        return moveSpeed;
    }

    public virtual UnitType GetUnitType()
    {
        return unitType;
    }
}