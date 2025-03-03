using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using System.Linq;

public class Blacksmith : BaseUnit
{
    [Header("Blacksmith-Specific Settings")]
    [SerializeField] private float baseKnockbackDistance = 3.0f;
    [SerializeField] private float knockbackBoostPerRealmUnit = 0.5f;
    [SerializeField] private float crashDamage = 75f;
    [SerializeField] private float crashRadius = 2.5f;

    [Header("Anvil Crash Ability Settings")]
    [SerializeField] private float crashDuration = 1.0f;
    [SerializeField] private GameObject crashEffectPrefab;
    [SerializeField] private float effectScale = 1.0f;
    [SerializeField] private int effectSortingOrder = 10;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem crashParticles;
    [SerializeField] private Color crashColor = new Color(0.8f, 0.4f, 0.0f, 1f); // Bronze/orange color
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool abilityStarted = false;
    private List<GameObject> crashEffects = new List<GameObject>();

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Blacksmith;
        orderType = OrderType.Realm;
        baseHealth = 1200f;
        baseDamage = 80f;
        baseAttackSpeed = 0.7f;
        baseMoveSpeed = 2.8f;
        attackRange = 3.0f;
        abilityChance = 0.05f;
        
        // Now call base.Awake after setting type and order
        base.Awake();
        
        // Initialize
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        
        // Get sprite renderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // Get particles if not assigned
        if (crashParticles == null)
        {
            crashParticles = GetComponent<ParticleSystem>();
        }
        
        Debug.Log($"Blacksmith unit initialized with type: {unitType}, order: {orderType}");
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState != GameState.BattleActive && isAbilityActive)
        {
            StopAllCoroutines();
            if (photonView.IsMine)
            {
                CleanupAllEffects();
            }
        }
    }

    public override void UpdateState(UnitState newState)
    {
        // If we were attacking and now we're not, clean up any active ability
        if (currentState == UnitState.Attacking && newState != UnitState.Attacking && isAbilityActive)
        {
            if (photonView.IsMine)
            {
                DeactivateAbility();
            }
        }
        
        base.UpdateState(newState);
    }

    protected override void Die()
    {
        CleanupAllEffects();
        base.Die();
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Blacksmith TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating Anvil Crash ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        Debug.Log("Blacksmith RPCActivateAbility called");
        
        // Immediately start the ability process (don't just set a flag)
        abilityStarted = true;
        
        // Find enemies to affect with our crash
        if (photonView.IsMine)
        {
            PerformAnvilCrash();
        }
    }

    protected override void PerformAbilityActivation()
    {
        Debug.Log("Blacksmith PerformAbilityActivation called");
        if (!abilityStarted)
        {
            abilityStarted = true;
            
            if (photonView.IsMine)
            {
                PerformAnvilCrash();
            }
        }
    }

    private void PerformAnvilCrash()
    {
        if (!photonView.IsMine) return;
        
        // Get facing direction (assume we're facing the current target)
        Vector3 crashDirection = Vector3.right;
        EnemyTargeting targeting = GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            Transform targetTransform = targeting.GetCurrentTarget();
            if (targetTransform != null)
            {
                crashDirection = (targetTransform.position - transform.position).normalized;
            }
        }
        
        // Create the crash effect
        if (crashEffectPrefab != null)
        {
            // Position the effect in front of the blacksmith in the direction of target
            Vector3 effectPosition = transform.position + (crashDirection * 1.5f);
            
            // Create the network effect
            GameObject crashEffect = PhotonNetwork.Instantiate(
                crashEffectPrefab.name,
                effectPosition,
                Quaternion.LookRotation(Vector3.forward, crashDirection)
            );
            
            // Scale the effect
            crashEffect.transform.localScale = Vector3.one * effectScale;
            
            // Set sorting layer for the effect
            var effectRenderers = crashEffect.GetComponentsInChildren<Renderer>();
            foreach (var renderer in effectRenderers)
            {
                renderer.sortingOrder = effectSortingOrder;
            }
            
            // Track the effect
            crashEffects.Add(crashEffect);
            
            // Auto-destroy
            StartCoroutine(DestroyAfterDelay(crashEffect, crashDuration + 0.2f));
        }
        
        // Apply visual feedback
        photonView.RPC("RPCShowCrashEffect", RpcTarget.All, crashDirection);
        
        // Find all enemies in the crash radius
        string enemyLayer = teamId == "TeamA" ? "TeamB" : "TeamA";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            transform.position + (crashDirection * crashRadius * 0.5f),
            crashRadius,
            LayerMask.GetMask(enemyLayer)
        );
        
        // Calculate the knockback boost based on nearby Realm units
        float knockbackBoost = GetRealmUnitBoost();
        float totalKnockbackDistance = baseKnockbackDistance + knockbackBoost;
        
        // Apply damage and knockback to each enemy
        foreach (Collider2D col in hitColliders)
        {
            BaseUnit enemy = col.GetComponent<BaseUnit>();
            if (enemy != null && enemy.GetCurrentState() != UnitState.Dead)
            {
                // Apply damage
                photonView.RPC("RPCApplyCrashDamage", RpcTarget.All, enemy.photonView.ViewID, crashDamage);
                
                // Calculate knockback direction (away from blacksmith)
                Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
                
                // Apply knockback using RPC
                photonView.RPC("RPCApplyKnockback", RpcTarget.All, 
                    enemy.photonView.ViewID, 
                    knockbackDirection,
                    totalKnockbackDistance);
            }
        }
        
        // Start cooldown timer
        StartCoroutine(DeactivateAfterDelay(crashDuration));
    }

    [PunRPC]
    private void RPCShowCrashEffect(Vector3 crashDirection)
    {
        // Visual feedback on the blacksmith
        if (spriteRenderer != null)
        {
            spriteRenderer.color = crashColor;
            StartCoroutine(ResetColorAfterDelay(crashDuration));
        }
        
        // Play particles if any
        if (crashParticles != null)
        {
            // Ensure visible sorting order
            var renderer = crashParticles.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = effectSortingOrder;
            }
            
            crashParticles.transform.rotation = Quaternion.LookRotation(Vector3.forward, crashDirection);
            crashParticles.Play();
            Debug.Log("Playing crash particles");
        }
    }

    [PunRPC]
    private void RPCApplyCrashDamage(int targetViewID, float damage)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;
        
        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null || target.GetCurrentState() == UnitState.Dead) return;
        
        // Apply damage directly to avoid double-damaging
        if (photonView.IsMine)
        {
            target.TakeDamage(damage);
        }
    }

    [PunRPC]
    private void RPCApplyKnockback(int targetViewID, Vector3 direction, float distance)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;
        
        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null || target.GetCurrentState() == UnitState.Dead) return;
        
        // Apply knockback by moving the unit
        StartCoroutine(KnockbackUnit(target, direction, distance));
    }

    private IEnumerator KnockbackUnit(BaseUnit unit, Vector3 direction, float distance)
    {
        // Disable movement while being knocked back
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.StopMovement();
            movement.enabled = false;
        }
        
        // Calculate start and end positions
        Vector3 startPos = unit.transform.position;
        Vector3 endPos = startPos + (direction * distance);
        
        // Ensure the end position is valid (not in walls, etc.)
        // You may need to add additional logic here based on your game
        
        // Perform knockback over time
        float knockbackDuration = 0.5f;
        float elapsedTime = 0f;
        
        while (elapsedTime < knockbackDuration && unit != null && unit.GetCurrentState() != UnitState.Dead)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / knockbackDuration;
            
            // Use smooth step for more natural movement
            progress = Mathf.SmoothStep(0, 1, progress);
            
            // Move the unit
            unit.transform.position = Vector3.Lerp(startPos, endPos, progress);
            
            yield return null;
        }
        
        // Re-enable movement
        if (unit != null && movement != null)
        {
            movement.enabled = true;
        }
    }

    private float GetRealmUnitBoost()
    {
        // Count nearby Realm units
        int realmCount = 0;
        
        // Find all units on our team
        Collider2D[] teamUnits = Physics2D.OverlapCircleAll(
            transform.position,
            5f, // Search radius
            LayerMask.GetMask(teamId)
        );
        
        // Count Realm units (excluding self)
        foreach (Collider2D col in teamUnits)
        {
            if (col.gameObject == gameObject) continue;
            
            BaseUnit ally = col.GetComponent<BaseUnit>();
            if (ally != null && ally.GetCurrentState() != UnitState.Dead && ally.GetOrderType() == OrderType.Realm)
            {
                realmCount++;
            }
        }
        
        // Calculate boost
        float boost = realmCount * knockbackBoostPerRealmUnit;
        Debug.Log($"Blacksmith knockback boost: {boost} from {realmCount} Realm units");
        
        return boost;
    }

    private IEnumerator ResetColorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DeactivateAbility();
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && obj.GetComponent<PhotonView>() != null && obj.GetComponent<PhotonView>().IsMine)
        {
            PhotonNetwork.Destroy(obj);
        }
    }

    private void CleanupAllEffects()
    {
        // Clean up any effects we own
        foreach (GameObject effect in crashEffects.ToList())
        {
            if (effect != null)
            {
                PhotonView view = effect.GetComponent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    PhotonNetwork.Destroy(effect);
                    Debug.Log($"Cleaned up crash effect: {effect.name}");
                }
                crashEffects.Remove(effect);
            }
        }
        crashEffects.Clear();
        
        // Clean up crash effects created by this client
        if (photonView.IsMine)
        {
            try {
                GameObject[] crashEffects = GameObject.FindGameObjectsWithTag("CrashEffect");
                
                foreach (GameObject effect in crashEffects)
                {
                    PhotonView view = effect.GetComponent<PhotonView>();
                    // Only destroy effects we own
                    if (view != null && view.IsMine)
                    {
                        PhotonNetwork.Destroy(view);
                        Debug.Log($"Cleaned up owned crash effect: {effect.name}");
                    }
                }
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"CrashEffect tag issue: {ex.Message}");
            }
        }
        
        // Reset ability state
        isAbilityActive = false;
        abilityStarted = false;
        
        // Reset visual feedback
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        // Stop particles if any
        if (crashParticles != null)
        {
            crashParticles.Stop();
        }
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        
        CleanupAllEffects();
        base.DeactivateAbility();
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, crashRadius);
    }
}