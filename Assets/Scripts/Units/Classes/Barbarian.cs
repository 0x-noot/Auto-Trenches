using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Barbarian : BaseUnit
{
    [Header("Barbarian-Specific Settings")]
    [SerializeField] private float baseCriticalStrikeChance = 0.15f;
    [SerializeField] private float currentCriticalStrikeChance;

    [Header("Primal Strike Ability Settings")]
    [SerializeField] private float stunDuration = 2.0f;
    [SerializeField] private float damageBonus = 0.5f; // 50% damage bonus
    [SerializeField] private GameObject stunEffectPrefab;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem strikeParticles;
    [SerializeField] private Color primalStrikeColor = new Color(1f, 0.4f, 0.0f, 1f); // Orange-red color
    [SerializeField] private int particlesSortingOrder = 10; // Added sorting order
    
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
        baseDamage = 110f;
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
        
        // Set particle system sorting order
        if (strikeParticles != null)
        {
            var renderer = strikeParticles.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = particlesSortingOrder;
            }
        }
        
        Debug.Log($"Barbarian unit initialized with type: {unitType}, order: {orderType}");
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState == GameState.BattleEnd || newState == GameState.PlayerAPlacement || 
            newState == GameState.GameOver)
        {
            // Immediately cleanup any active abilities and effects
            if (isAbilityActive)
            {
                Debug.Log("Game state changed - cleaning up stun effects");
                StopAllCoroutines();
                
                // Clean up effects
                CleanupAllEffects();
                
                isAbilityActive = false;
            }
        }
    }

    private void CleanupAllEffects()
    {
        // Clean up stun effects created by this client
        if (photonView.IsMine)
        {
            try {
                GameObject[] stunEffects = GameObject.FindGameObjectsWithTag("StunEffect");
                
                foreach (GameObject effect in stunEffects)
                {
                    PhotonView view = effect.GetComponent<PhotonView>();
                    // Only destroy effects we own
                    if (view != null && view.IsMine)
                    {
                        PhotonNetwork.Destroy(view);
                        Debug.Log($"Cleaned up owned stun effect: {effect.name}");
                    }
                }
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"StunEffect tag issue: {ex.Message}");
            }
        }
        
        // Release any stunned target
        if (currentStunnedTarget != null && photonView.IsMine)
        {
            ReleaseStunnedTarget();
        }
        
        // Reset state
        abilityStarted = false;
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
        float damage = attackDamage;
        
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
        
        // Immediately start the ability process (don't just set a flag)
        abilityStarted = true;
        
        // Find the current target to stun right away
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
                        Debug.Log($"Found target to stun: {targetUnit.GetUnitType()}");
                        photonView.RPC("RPCStunTarget", RpcTarget.All, targetUnit.photonView.ViewID);
                    }
                    else
                    {
                        Debug.LogWarning("Target unit is null or dead");
                    }
                }
                else
                {
                    Debug.LogWarning("No target transform found");
                }
            }
            else
            {
                Debug.LogWarning("No targeting component found");
            }
        }
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
                            Debug.Log($"Found target to stun: {targetUnit.GetUnitType()}");
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
        
        Debug.Log($"Stunning target: {target.GetUnitType()}");
        
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
            // Ensure visible sorting order
            var renderer = strikeParticles.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = particlesSortingOrder;
            }
            
            strikeParticles.Play();
            Debug.Log("Playing strike particles");
        }
        
        // Stun the target
        StunUnit(target);
        
        // Create stun effect
        if (stunEffectPrefab != null && photonView.IsMine)
        {
            Debug.Log($"Creating stun effect at {target.transform.position}");
            GameObject stunEffect = PhotonNetwork.Instantiate(
                stunEffectPrefab.name,
                target.transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );
            
            
            // Set sorting layer for the stun effect
            var stunRenderers = stunEffect.GetComponentsInChildren<Renderer>();
            foreach (var renderer in stunRenderers)
            {
                renderer.sortingOrder = particlesSortingOrder;
            }
            
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
        // Store the view ID for debugging
        int viewID = unit.photonView.ViewID;
        Debug.Log($"Stunning unit with ViewID {viewID}");
        
        // Disable ALL components that control unit actions
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            // Call RPC to ensure movement stops network-wide
            movement.photonView.RPC("RPCStopMovement", RpcTarget.All);
            movement.enabled = false;
            Debug.Log($"Disabled MovementSystem on unit {viewID}");
        }
        
        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            // Call RPC to ensure targeting stops network-wide
            targeting.photonView.RPC("RPCStopTargeting", RpcTarget.All);
            targeting.enabled = false;
            Debug.Log($"Disabled EnemyTargeting on unit {viewID}");
        }
        
        // Disable combat system to prevent attacks - critical!
        CombatSystem combat = unit.GetComponent<CombatSystem>();
        if (combat != null)
        {
            combat.enabled = false;
            Debug.Log($"Disabled CombatSystem on unit {viewID}");
        }
        
        // Use the unit's RPC to update state to ensure network synchronization
        unit.photonView.RPC("RPCUpdateState", RpcTarget.All, (int)UnitState.Idle);
        
        // Add visual indicator for stunned state
        SpriteRenderer renderer = unit.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            // Add a slight blue tint to indicate stun
            renderer.color = new Color(0.7f, 0.7f, 1.0f);
        }
    }

    private void UnstunUnit(BaseUnit unit)
    {
        if (unit == null) return;
        
        int viewID = unit.photonView.ViewID;
        Debug.Log($"Unstunning unit with ViewID {viewID}");
        
        // Re-enable all components
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.enabled = true;
            Debug.Log($"Re-enabled MovementSystem on unit {viewID}");
        }
        
        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.enabled = true;
            targeting.photonView.RPC("RPCStartTargeting", RpcTarget.All);
            Debug.Log($"Re-enabled EnemyTargeting on unit {viewID}");
        }
        
        CombatSystem combat = unit.GetComponent<CombatSystem>();
        if (combat != null)
        {
            combat.enabled = true;
            Debug.Log($"Re-enabled CombatSystem on unit {viewID}");
        }
        
        // Reset sprite color
        SpriteRenderer renderer = unit.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.white;
        }
        
        // Update unit state to reset its behavior
        unit.photonView.RPC("RPCUpdateState", RpcTarget.All, (int)UnitState.Idle);
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
        Debug.Log("Releasing stunned target");
        
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