using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Fighter : BaseUnit, IPunObservable
{
    [Header("Fighter-Specific Settings")]
    [SerializeField] private float baseCriticalStrikeChance = 0.15f;
    [SerializeField] private float currentCriticalStrikeChance;

    [Header("ApeShit Ability Settings")]
    [SerializeField] private float apeShitDuration = 4f;
    [SerializeField] private float apeShitAttackSpeedMultiplier = 2.2f;
    [SerializeField] private float apeShitCritChanceBonus = 0.25f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem rageParticles;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        unitType = UnitType.Fighter;
        baseHealth = 900f;
        baseDamage = 130f;
        baseAttackSpeed = 1.3f;
        baseMoveSpeed = 3.5f;
        attackRange = 3.5f;
        
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

    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        StartCoroutine(ApeShitAbility());
    }

    private IEnumerator ApeShitAbility()
    {
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

        if (photonView.IsMine)
        {
            ResetAbilityEffects();
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

    // Override RPCApplyUpgrades to include fighter-specific stats
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
        
        // No need for RPC here as this is called from an RPC
        currentCriticalStrikeChance = baseCriticalStrikeChance;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send Fighter-specific data
            stream.SendNext(currentCriticalStrikeChance);
            stream.SendNext(isAbilityActive);
        }
        else
        {
            // Receive Fighter-specific data
            this.currentCriticalStrikeChance = (float)stream.ReceiveNext();
            bool wasAbilityActive = isAbilityActive;
            this.isAbilityActive = (bool)stream.ReceiveNext();

            // Handle visual updates if ability state changed
            if (wasAbilityActive != isAbilityActive)
            {
                if (isAbilityActive)
                {
                    if (spriteRenderer != null) spriteRenderer.color = Color.red;
                    if (rageParticles != null) rageParticles.Play();
                }
                else
                {
                    if (spriteRenderer != null) spriteRenderer.color = originalColor;
                    if (rageParticles != null) rageParticles.Stop();
                }
            }
        }
    }

}