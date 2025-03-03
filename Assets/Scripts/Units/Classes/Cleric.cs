using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class Cleric : BaseUnit
{
    [Header("Cleric-Specific Settings")]
    [SerializeField] private float healAmount = 200f;
    [SerializeField] private float healRadius = 5f;

    [Header("Divine Blessing Ability Settings")]
    [SerializeField] private float divineHealDuration = 1.5f;
    [SerializeField] private GameObject healEffectPrefab;
    [SerializeField] private Color healColor = new Color(0.5f, 1f, 0.5f, 1f);
    
    private bool isHealingActive = false;
    private List<int> healedUnitsViewIDs = new List<int>();

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Cleric;
        orderType = OrderType.Shield;
        baseHealth = 800f;
        baseDamage = 60f;
        baseAttackSpeed = 0.7f;
        baseMoveSpeed = 3f;
        attackRange = 5f;
        abilityChance = 0.06f;
        
        // Now call base.Awake after setting type and order
        base.Awake();
        
        // Initialize
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        
        Debug.Log($"Cleric unit initialized with type: {unitType}, order: {orderType}");
    }

    protected override void Update()
    {
        base.Update();
        
        // Check if healing is active but effect hasn't started
        if (isAbilityActive && !isHealingActive && photonView.IsMine)
        {
            isHealingActive = true;
            StartCoroutine(DivineHealingAbility());
            Debug.Log($"{GetUnitType()} ability is active");
        }
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Cleric TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating Divine Blessing ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }
    
    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        Debug.Log("Cleric RPCActivateAbility called - should trigger visual effects");
        // Make sure this immediately triggers the visual change
        isHealingActive = true;
        StartCoroutine(DivineHealingAbility());
    }

    protected override void PerformAbilityActivation()
    {
        Debug.Log("Cleric PerformAbilityActivation called");
        if (!isHealingActive)
        {
            isHealingActive = true;
            StartCoroutine(DivineHealingAbility());
        }
    }

    private IEnumerator DivineHealingAbility()
    {
        Debug.Log("Cleric DivineHealingAbility coroutine started");
        healedUnitsViewIDs.Clear();
        
        // Only execute actual healing if we're the owner
        if (photonView.IsMine)
        {
            // Find nearby allies to heal
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
                transform.position,
                healRadius,
                LayerMask.GetMask(teamId)
            );

            // Shuffle the array to randomize healing priority
            ShuffleArray(hitColliders);

            // Find a valid ally to heal
            bool healedAnyone = false;
            foreach (Collider2D col in hitColliders)
            {
                BaseUnit ally = col.GetComponent<BaseUnit>();
                if (ally != null && 
                    ally != this && // Don't heal self
                    ally.GetCurrentState() != UnitState.Dead)
                {
                    // Get health information to see if healing is needed
                    HealthSystem allyHealth = ally.GetComponent<HealthSystem>();
                    if (allyHealth != null && allyHealth.GetCurrentHealth() < allyHealth.GetMaxHealth())
                    {
                        // Store who we healed to avoid healing twice
                        healedUnitsViewIDs.Add(ally.photonView.ViewID);
                        
                        // Heal the ally
                        Debug.Log($"Healing ally: {ally.GetUnitType()} for {healAmount} HP");
                        photonView.RPC("RPCHealAlly", RpcTarget.AllBuffered, ally.photonView.ViewID, healAmount);
                        
                        // Spawn heal effect if prefab is assigned
                        if (healEffectPrefab != null)
                        {
                            GameObject healEffect = PhotonNetwork.Instantiate(
                                healEffectPrefab.name, 
                                ally.transform.position, 
                                Quaternion.identity
                            );
                            
                            // Auto-destroy the heal effect
                            StartCoroutine(DestroyAfterDelay(healEffect, divineHealDuration + 0.2f));
                        }
                        
                        healedAnyone = true;
                        break; // Heal only one ally per activation
                    }
                }
            }
            
            // If no valid ally was found, try to heal self
            if (!healedAnyone && GetComponent<HealthSystem>() != null)
            {
                HealthSystem ownHealth = GetComponent<HealthSystem>();
                if (ownHealth.GetCurrentHealth() < ownHealth.GetMaxHealth())
                {
                    Debug.Log($"No allies to heal, healing self for {healAmount} HP");
                    photonView.RPC("RPCHealAlly", RpcTarget.AllBuffered, photonView.ViewID, healAmount);
                    
                    // Spawn heal effect
                    if (healEffectPrefab != null)
                    {
                        GameObject healEffect = PhotonNetwork.Instantiate(
                            healEffectPrefab.name, 
                            transform.position, 
                            Quaternion.identity
                        );
                        
                        StartCoroutine(DestroyAfterDelay(healEffect, divineHealDuration + 0.2f));
                    }
                }
            }
        }
        
        // Visual feedback for ability activation
        photonView.RPC("RPCShowHealingEffect", RpcTarget.All);
        
        // Wait for the healing duration
        yield return new WaitForSeconds(divineHealDuration);
        
        // Reset and deactivate
        if (photonView.IsMine)
        {
            isHealingActive = false;
            DeactivateAbility();
        }
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState == GameState.BattleEnd || newState == GameState.PlayerAPlacement)
        {
            // Immediately cleanup any active abilities and effects
            StopAllCoroutines();
            
            // Clean up effects
            CleanupAllEffects();
            
            // Reset healing state
            isAbilityActive = false;
            isHealingActive = false;
            healedUnitsViewIDs.Clear();
            
            // Reset visual feedback
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
            
            Debug.Log("Cleric: Cleaned up healing effects due to game state change");
        }
    }

    private void CleanupAllEffects()
    {
        // If we're the master client, we can try to destroy any orphaned network objects
        if (PhotonNetwork.IsMasterClient)
        {
            // Find all effects by tag
            GameObject[] effects = GameObject.FindGameObjectsWithTag("HealEffect");
            
            foreach (GameObject effect in effects)
            {
                PhotonView view = effect.GetComponent<PhotonView>();
                if (view != null)
                {
                    PhotonNetwork.Destroy(view);
                    Debug.Log($"Master client cleaning up heal effect: {effect.name}");
                }
            }
        }
        else
        {
            // For clients, only clean up effects they own
            GameObject[] effects = GameObject.FindGameObjectsWithTag("HealEffect");
            
            foreach (GameObject effect in effects)
            {
                PhotonView view = effect.GetComponent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    PhotonNetwork.Destroy(view);
                    Debug.Log($"Client cleaning up owned heal effect: {effect.name}");
                }
            }
        }
    }


    // Helper to randomize ally selection
    private void ShuffleArray<T>(T[] array)
    {
        int n = array.Length;
        for (int i = 0; i < n; i++)
        {
            int r = i + Random.Range(0, n - i);
            T temp = array[r];
            array[r] = array[i];
            array[i] = temp;
        }
    }

    [PunRPC]
    private void RPCShowHealingEffect()
    {
        // Add visual feedback for the cleric (glow effect, particles, etc.)
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // Store the original color to restore later
            Color originalColor = spriteRenderer.color;
            
            // Set to heal color
            spriteRenderer.color = healColor;
            
            // Start a coroutine to reset the color after the duration
            StartCoroutine(ResetColorAfterDelay(spriteRenderer, originalColor, divineHealDuration));
        }
        
        // Play particles if any
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
        foreach (var particle in particles)
        {
            if (particle != null)
            {
                particle.Play();
            }
        }
    }

    private IEnumerator ResetColorAfterDelay(SpriteRenderer renderer, Color originalColor, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (renderer != null)
        {
            renderer.color = originalColor;
        }
    }

    [PunRPC]
    private void RPCHealAlly(int allyViewID, float amount)
    {
        PhotonView allyView = PhotonView.Find(allyViewID);
        if (allyView == null) return;
        
        BaseUnit ally = allyView.GetComponent<BaseUnit>();
        if (ally == null || ally.GetCurrentState() == UnitState.Dead) return;
        
        // Use inverse of TakeDamage - need to add a new method to BaseUnit for this
        // For now, use reflection to access health directly
        var healthSystem = ally.GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            // Get current health before healing
            float currentHealth = healthSystem.GetCurrentHealth();
            float maxHealth = healthSystem.GetMaxHealth();
            
            // Calculate new health
            float newHealth = Mathf.Min(currentHealth + amount, maxHealth);
            
            // Set the health
            healthSystem.SetHealth(newHealth, maxHealth);
            
            Debug.Log($"Healed {ally.GetUnitType()} from {currentHealth} to {newHealth}");
        }
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
        
        isHealingActive = false;
        healedUnitsViewIDs.Clear();
        
        base.DeactivateAbility();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = healColor;
        Gizmos.DrawWireSphere(transform.position, healRadius);
    }
}