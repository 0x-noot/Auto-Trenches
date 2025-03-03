using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Berserker : BaseUnit
{
    [Header("Berserker-Specific Settings")]
    [SerializeField] private float baseCriticalStrikeChance = 0.10f;
    [SerializeField] private float currentCriticalStrikeChance;

    [Header("Blood Rage Ability Settings")]
    [SerializeField] private float bloodRageDuration = 4f;
    [SerializeField] private float bloodRageAttackSpeedMultiplier = 2.0f;
    [SerializeField] private float bloodRageCritChanceBonus = 0.20f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem rageParticles;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool abilityStarted = false;

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Berserker;
        orderType = OrderType.Wild;
        baseHealth = 950f;
        baseDamage = 110f;
        baseAttackSpeed = 1.2f;
        baseMoveSpeed = 3.5f;
        attackRange = 3.5f;
        abilityChance = 0.08f;
        
        // Now call base.Awake after setting type and order
        base.Awake();
        
        // Set current stats equal to base stats initially
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        currentCriticalStrikeChance = baseCriticalStrikeChance;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

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
            if (photonView.IsMine)
            {
                ResetAbilityEffects();
            }
        }
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Attacking && newState != UnitState.Attacking && isAbilityActive)
        {
            StopAllCoroutines();
            if (photonView.IsMine)
            {
                ResetAbilityEffects();
            }
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

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Berserker TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating Blood Rage ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        Debug.Log("Berserker RPCActivateAbility called");
        // Directly start the ability coroutine
        abilityStarted = true;
        StartCoroutine(BloodRageAbility());
    }

    protected override void PerformAbilityActivation()
    {
        Debug.Log("Berserker PerformAbilityActivation called");
        if (!abilityStarted)
        {
            abilityStarted = true;
            StartCoroutine(BloodRageAbility());
        }
    }

    private IEnumerator BloodRageAbility()
    {
        Debug.Log("Berserker BloodRageAbility coroutine started");
        photonView.RPC("RPCApplyAbilityBuffs", RpcTarget.All);

        float elapsedTime = 0f;
        while (elapsedTime < bloodRageDuration && currentState == UnitState.Attacking)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (photonView.IsMine)
        {
            ResetAbilityEffects();
        }
    }

    [PunRPC]
    private void RPCApplyAbilityBuffs()
    {
        Debug.Log("Berserker RPCApplyAbilityBuffs called");
        // Apply buffs
        attackSpeed = baseAttackSpeed * bloodRageAttackSpeedMultiplier;
        currentCriticalStrikeChance += bloodRageCritChanceBonus;

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
    }

    private void ResetAbilityEffects()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCResetAbilityEffects", RpcTarget.All);
    }

    [PunRPC]
    private void RPCResetAbilityEffects()
    {
        Debug.Log("Berserker RPCResetAbilityEffects called");
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

        abilityStarted = false;
        DeactivateAbility();
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    public float GetAbilityCooldownRemaining()
    {
        return Mathf.Max(0, nextAbilityTime - Time.time);
    }
}