using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Barbarian : BaseUnit
{
    [Header("Barbarian-Specific Settings")]
    [SerializeField] private float baseCriticalStrikeChance = 0.15f;
    [SerializeField] private float currentCriticalStrikeChance;

    [Header("Primal Strike Ability Settings")]
    [SerializeField] private float stunDuration = 1.5f;
    [SerializeField] private float damageBonus = 0.5f; // 50% damage bonus
    [SerializeField] private GameObject stunEffectPrefab;
    [SerializeField] private float stunEffectScale = 1.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem strikeParticles;
    [SerializeField] private Color primalStrikeColor = new Color(1f, 0.4f, 0.0f, 1f); // Orange-red color
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private BaseUnit currentStunnedTarget;
    private bool abilityStarted = false;

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Barbarian;
        orderType = OrderType.Wild;
        baseHealth = 850f;
        baseDamage = 100f;
        baseAttackSpeed = 0.9f;
        baseMoveSpeed = 3.4f;
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

        if (strikeParticles == null)
        {
            strikeParticles = GetComponent<ParticleSystem>();
        }
        
        Debug.Log($"Barbarian unit initialized with type: {unitType}, order: {orderType}");
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState != GameState.BattleActive && isAbilityActive)
        {
            StopAllCoroutines();
            if (photonView.IsMine)
            {
                ReleaseStunnedTarget();
            }
        }
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Attacking && newState != UnitState.Attacking && isAbilityActive)
        {
            if (photonView.IsMine)
            {
                ReleaseStunnedTarget();
            }
        }
        
        base.UpdateState(newState);
    }

    public override float GetAttackDamage()
    {
        float damage = currentAttackDamage;
        
        // Check for critical strike
        if (Random.value < currentCriticalStrikeChance)
        {
            damage *= 1.5f;
        }
        
        // Check if this is a Primal Strike hit
        if (isAbilityActive && currentStunnedTarget != null)
        {
            damage *= (1 + damageBonus);
        }
        
        return damage;
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Barbarian TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating Primal Strike ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        Debug.Log("Barbarian RPCActivateAbility called");
        // Directly start the ability coroutine
        abilityStarted = true;
    }

    protected override void PerformAbilityActivation()
    {
        Debug.Log("Barbarian PerformAbilityActivation called");
        if (!abilityStarted)
        {
            abilityStarted = true;
            
            // Find the current target to stun
            if (photonView.IsMine)
            {
                EnemyTargeting targeting = GetComponent<EnemyTargeting>();
                if (targeting != null)
                {
                    Transform targetTransform = targeting.GetCurrentTarget();
                    if (targetTransform != null)
                    {
                        BaseUnit targetUnit = targetTransform.GetComponent<BaseUnit>();
                        if (targetUnit != null && targetUnit.GetCurrentState() != UnitState.Dead)
                        {
                            photonView.RPC("RPCStunTarget", RpcTarget.All, targetUnit.photonView.ViewID);
                        }
                    }
                }
            }
        }
    }

    [PunRPC]
    private void RPCStunTarget(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;
        
        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null || target.GetCurrentState() == UnitState.Dead) return;
        
        // Store the target
        currentStunnedTarget = target;
        
        // Visual feedback
        if (spriteRenderer != null)
        {
            spriteRenderer.color = primalStrikeColor;
        }
        
        // Play particles if any
        if (strikeParticles != null)
        {
            strikeParticles.Play();
        }
        
        // Stun the target
        StunUnit(target);
        
        // Create stun effect
        if (stunEffectPrefab != null && photonView.IsMine)
        {
            GameObject stunEffect = PhotonNetwork.Instantiate(
                stunEffectPrefab.name,
                target.transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );
            
            // Scale the effect
            stunEffect.transform.localScale = Vector3.one * stunEffectScale;
            
            // Parent to the target
            stunEffect.transform.SetParent(target.transform);
            
            // Auto-destroy
            StartCoroutine(DestroyAfterDelay(stunEffect, stunDuration + 0.2f));
        }
        
        // Start timer to release the target
        if (photonView.IsMine)
        {
            StartCoroutine(ReleaseTargetAfterDelay(stunDuration));
        }
    }

    private void StunUnit(BaseUnit unit)
    {
        // Disable unit's movement and targeting
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.StopMovement();
            movement.enabled = false;
        }
        
        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.StopTargeting();
            targeting.enabled = false;
        }
        
        // Update unit state
        unit.UpdateState(UnitState.Idle);
    }

    private void UnstunUnit(BaseUnit unit)
    {
        if (unit == null) return;
        
        // Re-enable unit's movement and targeting
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.enabled = true;
        }
        
        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.enabled = true;
            targeting.StartTargeting();
        }
        
        // Update unit state to reset its behavior
        unit.UpdateState(UnitState.Idle);
    }

    private IEnumerator ReleaseTargetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReleaseStunnedTarget();
    }

    private void ReleaseStunnedTarget()
    {
        if (!photonView.IsMine) return;
        
        if (currentStunnedTarget != null)
        {
            photonView.RPC("RPCReleaseTarget", RpcTarget.All);
        }
        
        DeactivateAbility();
    }

    [PunRPC]
    private void RPCReleaseTarget()
    {
        // Unstun the target
        if (currentStunnedTarget != null)
        {
            UnstunUnit(currentStunnedTarget);
            currentStunnedTarget = null;
        }
        
        // Reset visual feedback
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        // Stop particles if any
        if (strikeParticles != null)
        {
            strikeParticles.Stop();
        }
        
        abilityStarted = false;
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Destroy(obj);
        }
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        
        isAbilityActive = false;
        abilityStarted = false;
        
        base.DeactivateAbility();
    }

    public float GetAbilityCooldownRemaining()
    {
        return Mathf.Max(0, nextAbilityTime - Time.time);
    }
}