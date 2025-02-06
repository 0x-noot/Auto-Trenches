using UnityEngine;
using System.Collections;

public class Tank : BaseUnit
{
    [Header("Tank-Specific Settings")]
    [SerializeField] private float baseArmorBonus = 20f;
    private float currentArmorBonus;

    [Header("Shield Ability Settings")]
    [SerializeField] private float shieldDuration = 5f;
    [SerializeField] private float shieldArmorMultiplier = 3f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject shieldEffectPrefab;
    [SerializeField] private Color shieldActiveColor = new Color(0, 0.8f, 1f, 1f);
    
    private GameObject activeShieldEffect;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        unitType = UnitType.Tank;
        maxHealth = 2000f;
        attackDamage = 50f;
        attackRange = 3.5f;
        moveSpeed = 2f;
        attackSpeed = 0.8f;
        
        currentArmorBonus = baseArmorBonus;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    protected override void OnDestroy()
    {
        ResetShieldEffects();
        base.OnDestroy();
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Dead)
        {
            ResetShieldEffects();
        }
        base.UpdateState(newState);
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);
        if (newState != GameState.BattleActive && isAbilityActive)
        {
            ResetShieldEffects();
        }
    }

    public override void TakeDamage(float damage)
    {
        float reducedDamage = damage * (100f / (100f + currentArmorBonus));
        base.TakeDamage(reducedDamage);
    }

    protected override void ActivateAbility()
    {
        if (!isAbilityActive && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive)
        {
            base.ActivateAbility();
            StartCoroutine(ShieldAbility());
        }
    }

    private IEnumerator ShieldAbility()
    {
        // Activate shield effects
        ActivateShieldEffects();

        // Increase own armor
        currentArmorBonus = baseArmorBonus * shieldArmorMultiplier;

        float elapsedTime = 0f;
        while (elapsedTime < shieldDuration && currentState != UnitState.Dead)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Reset everything
        ResetShieldEffects();
    }

    private void ActivateShieldEffects()
    {
        // Visual feedback on tank
        if (spriteRenderer != null)
        {
            spriteRenderer.color = shieldActiveColor;
        }

        // Spawn shield effect if prefab is assigned
        if (shieldEffectPrefab != null && activeShieldEffect == null)
        {
            activeShieldEffect = Instantiate(shieldEffectPrefab, transform);
        }
    }

    private void ResetShieldEffects()
    {
        // Reset armor
        currentArmorBonus = baseArmorBonus;

        // Reset tank visuals
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Clean up shield effect
        if (activeShieldEffect != null)
        {
            Destroy(activeShieldEffect);
            activeShieldEffect = null;
        }

        DeactivateAbility();
    }
}