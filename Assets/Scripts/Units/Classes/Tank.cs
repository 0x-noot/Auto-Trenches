using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Tank : BaseUnit
{
    [Header("Tank-Specific Settings")]
    [SerializeField] private float baseArmorBonus = 25f;
    private float currentArmorBonus;

    [Header("Shield Ability Settings")]
    [SerializeField] private float shieldDuration = 6f;
    [SerializeField] private float shieldArmorMultiplier = 2.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject shieldEffectPrefab;
    [SerializeField] private Color shieldActiveColor = new Color(0, 0.8f, 1f, 1f);
    
    private GameObject activeShieldEffect;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool shieldEffectStarted = false;

    private void Awake()
    {
        unitType = UnitType.Tank;
        maxHealth = 1800f;
        attackDamage = 60f;
        attackRange = 3.5f;
        moveSpeed = 2.5f;
        attackSpeed = 0.9f;
        abilityChance = 0.04f;
        
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
        if (!photonView.IsMine) return;
        float reducedDamage = damage * (100f / (100f + currentArmorBonus));
        base.TakeDamage(reducedDamage);
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Tank TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating shield ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        Debug.Log("Tank RPCActivateAbility called");
        // Directly start the ability coroutine
        shieldEffectStarted = true;
        StartCoroutine(ShieldAbility());
    }

    protected override void PerformAbilityActivation()
    {
        Debug.Log("Tank PerformAbilityActivation called");
        if (!shieldEffectStarted)
        {
            shieldEffectStarted = true;
            StartCoroutine(ShieldAbility());
        }
    }

    private IEnumerator ShieldAbility()
    {
        Debug.Log("Tank ShieldAbility coroutine started");
        photonView.RPC("RPCActivateShieldEffects", RpcTarget.All);

        float elapsedTime = 0f;
        while (elapsedTime < shieldDuration && currentState != UnitState.Dead)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (photonView.IsMine)
        {
            ResetShieldEffects();
        }
    }

    [PunRPC]
    private void RPCActivateShieldEffects()
    {
        Debug.Log("Tank RPCActivateShieldEffects called");
        // Increase armor
        currentArmorBonus = baseArmorBonus * shieldArmorMultiplier;

        // Visual feedback on tank
        if (spriteRenderer != null)
        {
            spriteRenderer.color = shieldActiveColor;
        }

        // Spawn shield effect if prefab is assigned
        if (shieldEffectPrefab != null && activeShieldEffect == null)
        {
            activeShieldEffect = Instantiate(shieldEffectPrefab, transform);
            var shieldEffect = activeShieldEffect.GetComponent<ShieldEffect>();
            if (shieldEffect != null)
            {
                shieldEffect.ActivateShield();
            }
        }
    }

    private void ResetShieldEffects()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCResetShieldEffects", RpcTarget.All);
    }

    [PunRPC]
    private void RPCResetShieldEffects()
    {
        Debug.Log("Tank RPCResetShieldEffects called");
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
        // Update base armor bonus with upgrades
        baseArmorBonus = 25f * armorMultiplier;
        if (!isAbilityActive)
        {
            currentArmorBonus = baseArmorBonus;
        }
        else
        {
            currentArmorBonus = baseArmorBonus * shieldArmorMultiplier;
        }
    }
}