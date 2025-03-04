using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Knight : BaseUnit
{
    [Header("Knight-Specific Settings")]
    [SerializeField] private float baseArmorBonus = 25f;
    private float currentArmorBonus;

    [Header("Divine Aegis Ability Settings")]
    [SerializeField] private float divineAegisDuration = 6f;
    [SerializeField] private float divineAegisArmorMultiplier = 2.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject divineAegisEffectPrefab;
    [SerializeField] private Color divineAegisActiveColor = new Color(1f, 0.9f, 0.4f, 1f); // Golden glow
    
    private GameObject activeShieldEffect;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool shieldEffectStarted = false;

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Knight;
        orderType = OrderType.Shield;
        baseHealth = 2000f;
        baseDamage = 55f;
        baseAttackSpeed = 0.8f;
        baseMoveSpeed = 2.5f;
        attackRange = 3.5f;
        abilityChance = 0.04f;
        
        // Now call base.Awake after setting type and order
        base.Awake();
        
        // Initialize
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        currentArmorBonus = baseArmorBonus;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    protected override void OnDestroy()
    {
        ResetDivineAegisEffects();
        base.OnDestroy();
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Dead)
        {
            ResetDivineAegisEffects();
        }
        base.UpdateState(newState);
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);
        if (newState != GameState.BattleActive && isAbilityActive)
        {
            ResetDivineAegisEffects();
        }
    }

    public override void TakeDamage(float damage)
    {
        if (!photonView.IsMine) return;
        float reducedDamage = damage * (100f / (100f + currentArmorBonus));
        base.TakeDamage(reducedDamage);
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Knight TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating Divine Aegis ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        Debug.Log("Knight RPCActivateAbility called");
        // Directly start the ability coroutine
        shieldEffectStarted = true;
        StartCoroutine(DivineAegisAbility());
    }

    protected override void PerformAbilityActivation()
    {
        Debug.Log("Knight PerformAbilityActivation called");
        if (!shieldEffectStarted)
        {
            shieldEffectStarted = true;
            StartCoroutine(DivineAegisAbility());
        }
    }

    private IEnumerator DivineAegisAbility()
    {
        Debug.Log("Knight DivineAegisAbility coroutine started");
        photonView.RPC("RPCActivateDivineAegisEffects", RpcTarget.All);

        float elapsedTime = 0f;
        while (elapsedTime < divineAegisDuration && currentState != UnitState.Dead)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (photonView.IsMine)
        {
            ResetDivineAegisEffects();
        }
    }

    [PunRPC]
    private void RPCActivateDivineAegisEffects()
    {
        Debug.Log("Knight RPCActivateDivineAegisEffects called");
        // Increase armor
        currentArmorBonus = baseArmorBonus * divineAegisArmorMultiplier;

        // Visual feedback on knight
        if (spriteRenderer != null)
        {
            spriteRenderer.color = divineAegisActiveColor;
        }

        // Spawn shield effect if prefab is assigned
        if (divineAegisEffectPrefab != null && activeShieldEffect == null)
        {
            activeShieldEffect = Instantiate(divineAegisEffectPrefab, transform);
            var shieldEffect = activeShieldEffect.GetComponent<ShieldEffect>();
            if (shieldEffect != null)
            {
                shieldEffect.ActivateShield();
            }
        }
    }

    private void ResetDivineAegisEffects()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCResetDivineAegisEffects", RpcTarget.All);
    }

    [PunRPC]
    private void RPCResetDivineAegisEffects()
    {
        Debug.Log("Knight RPCResetDivineAegisEffects called");
        // Reset armor
        currentArmorBonus = baseArmorBonus;

        // Reset knight visuals
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Clean up shield effect
        if (activeShieldEffect != null)
        {
            var shieldEffect = activeShieldEffect.GetComponent<ShieldEffect>();
            if (shieldEffect != null)
            {
                shieldEffect.DeactivateShield();
            }
            Destroy(activeShieldEffect);
            activeShieldEffect = null;
        }

        shieldEffectStarted = false;
        DeactivateAbility();
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
        // Update base armor bonus with multiplier
        baseArmorBonus = 25f * armorMultiplier;
        if (!isAbilityActive)
        {
            currentArmorBonus = baseArmorBonus;
        }
        else
        {
            currentArmorBonus = baseArmorBonus * divineAegisArmorMultiplier;
        }
    }
}