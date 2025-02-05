using UnityEngine;
using System.Collections;

public class Fighter : BaseUnit
{
    [Header("Fighter-Specific Settings")]
    [SerializeField] private float baseCriticalStrikeChance = 0.2f;
    [SerializeField] private float currentCriticalStrikeChance;

    [Header("ApeShit Ability Settings")]
    [SerializeField] private float apeShitDuration = 5f;
    [SerializeField] private float apeShitAttackSpeedMultiplier = 2f;
    [SerializeField] private float apeShitCritChanceBonus = 0.3f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem rageParticles;
    
    private float baseAttackSpeed;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        unitType = UnitType.Fighter;
        maxHealth = 800f;
        attackDamage = 150f;
        attackRange = 3.5f;
        moveSpeed = 3.5f;
        attackSpeed = 1.2f;
        
        // Store base values
        baseAttackSpeed = attackSpeed;
        currentCriticalStrikeChance = baseCriticalStrikeChance;

        // Get and store sprite renderer reference
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // Get particle system reference if not set
        if (rageParticles == null)
        {
            rageParticles = GetComponent<ParticleSystem>();
        }
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState != GameState.BattleActive && isAbilityActive)
        {
            StopAllCoroutines();
            ResetAbilityEffects();
        }
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Attacking && newState != UnitState.Attacking && isAbilityActive)
        {
            StopAllCoroutines();
            ResetAbilityEffects();
        }
        
        base.UpdateState(newState);
    }

    public override float GetAttackDamage()
    {
        if (Random.value < currentCriticalStrikeChance)
        {
            return attackDamage * 1.5f;
        }
        return attackDamage;
    }

    protected override void ActivateAbility()
    {
        if (!isAbilityActive && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive && 
            currentState == UnitState.Attacking)
        {
            Debug.Log($"Fighter {gameObject.name} activating ability!");
            base.ActivateAbility();
            StartCoroutine(ApeShitAbility());
        }
    }

    private IEnumerator ApeShitAbility()
    {
        Debug.Log($"{gameObject.name} activating ApeShit ability!");

        // Apply buffs
        attackSpeed = baseAttackSpeed * apeShitAttackSpeedMultiplier;
        currentCriticalStrikeChance += apeShitCritChanceBonus;

        // Visual feedback
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }

        // Start particle effect
        if (rageParticles != null)
        {
            rageParticles.Play();
        }

        float elapsedTime = 0f;
        while (elapsedTime < apeShitDuration && currentState == UnitState.Attacking)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        ResetAbilityEffects();
        Debug.Log($"{gameObject.name} ApeShit ability ended!");
    }

    private void ResetAbilityEffects()
    {
        // Reset stats
        attackSpeed = baseAttackSpeed;
        currentCriticalStrikeChance = baseCriticalStrikeChance;

        // Reset visual feedback
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Stop particle effect
        if (rageParticles != null)
        {
            rageParticles.Stop();
        }

        DeactivateAbility();
    }

    public float GetAbilityCooldownRemaining()
    {
        return Mathf.Max(0, nextAbilityTime - Time.time);
    }
}