using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Photon.Pun;

public class PeasantMilitia : BaseUnit
{
    [Header("Peasant Militia-Specific Settings")]
    [SerializeField] private float groupRadius = 5f;
    [SerializeField] private float groupMovementSpeedBonus = 0.1f; // 10% per nearby militia
    [SerializeField] private float groupMaxSpeedBonus = 0.4f; // 40% max bonus

    [Header("Strength in Numbers Visual Effects")]
    [SerializeField] private GameObject strengthEffectPrefab;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color strengthColor = new Color(1f, 0.8f, 0.2f, 1f); // Golden
    [SerializeField] private int particlesSortingOrder = 5;
    
    private SpriteRenderer spriteRenderer;
    private float lastGroupCheckTime = 0f;
    private const float GROUP_CHECK_INTERVAL = 1f;
    private int nearbyMilitiaCount = 0;
    private List<GameObject> strengthEffects = new List<GameObject>();
    
    // Speed calculation vars
    private float baseSpeedValue;
    private float currentSpeedBonus = 0f;

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.PeasantMilitia;
        orderType = OrderType.Realm;
        baseHealth = 500f;
        baseDamage = 40f;
        baseAttackSpeed = 1.2f;
        baseMoveSpeed = 3.1f;
        attackRange = 2.8f;
        abilityChance = 0f; // No special ability, uses passive synergy instead
        
        // Now call base.Awake after setting type and order
        base.Awake();
        
        // Initialize
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        baseSpeedValue = baseMoveSpeed;
        
        // Get sprite renderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
        }
        
        Debug.Log($"PeasantMilitia unit initialized with type: {unitType}, order: {orderType}");
    }

    protected override void Start()
    {
        base.Start();
        
        // Do an initial check for group bonuses
        if (photonView.IsMine)
        {
            CheckForNearbyMilitia();
        }
    }

    protected override void Update()
    {
        base.Update();
        
        // Periodically check for other militia units
        if (photonView.IsMine && Time.time - lastGroupCheckTime > GROUP_CHECK_INTERVAL)
        {
            lastGroupCheckTime = Time.time;
            CheckForNearbyMilitia();
        }
    }
    
    private void LateUpdate()
    {
        // If we have strength effects, make sure they stay positioned correctly
        foreach (GameObject effect in strengthEffects.ToList())
        {
            if (effect != null)
            {
                if (effect.transform.parent != transform)
                {
                    effect.transform.SetParent(transform);
                    effect.transform.localPosition = Vector3.up * 0.5f;
                }
            }
            else
            {
                // Remove null references
                strengthEffects.Remove(effect);
            }
        }
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState == GameState.BattleEnd || newState == GameState.PlayerAPlacement)
        {
            // Clean up strength effects
            CleanupStrengthEffects();
        }
    }
    
    protected override void Die()
    {
        // Make sure to clean up effects before dying
        CleanupStrengthEffects();
        base.Die();
    }

    private void CheckForNearbyMilitia()
    {
        if (currentState == UnitState.Dead) return;
        
        // Find all militia in radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            transform.position,
            groupRadius,
            LayerMask.GetMask(teamId)
        );
        
        // Count how many are other militia units (not this one)
        int militiaCount = 0;
        foreach (Collider2D col in hitColliders)
        {
            if (col.gameObject == gameObject) continue;
            
            PeasantMilitia otherMilitia = col.GetComponent<PeasantMilitia>();
            if (otherMilitia != null && otherMilitia.GetCurrentState() != UnitState.Dead)
            {
                militiaCount++;
            }
        }
        
        // Only update if the count changed
        if (militiaCount != nearbyMilitiaCount)
        {
            // Update count and sync to other clients
            nearbyMilitiaCount = militiaCount;
            photonView.RPC("RPCUpdateMilitiaCount", RpcTarget.All, militiaCount);
        }
    }

    [PunRPC]
    private void RPCUpdateMilitiaCount(int count)
    {
        // Store the new count
        nearbyMilitiaCount = count;
        
        // Calculate speed bonus
        float newSpeedBonus = Mathf.Min(count * groupMovementSpeedBonus, groupMaxSpeedBonus);
        
        // Update visuals
        UpdateStrengthVisuals(count);
        
        // If the bonus actually changed, update movement speed
        if (Mathf.Abs(newSpeedBonus - currentSpeedBonus) > 0.01f)
        {
            // Remove old bonus first
            if (currentSpeedBonus > 0)
            {
                currentMoveSpeed = baseSpeedValue;
            }
            
            // Apply new bonus
            currentSpeedBonus = newSpeedBonus;
            currentMoveSpeed = baseSpeedValue * (1 + currentSpeedBonus);
            
            // Update movement component
            if (movementSystem != null && movementSystem.enabled)
            {
                movementSystem.SetMoveSpeed(currentMoveSpeed);
            }
            
            Debug.Log($"Militia group bonus updated: {count} nearby units, {currentSpeedBonus:P0} speed bonus");
        }
    }

    private void UpdateStrengthVisuals(int count)
    {
        // Clear old effects first
        foreach (GameObject effect in strengthEffects.ToList())
        {
            if (effect != null)
            {
                if (effect.GetComponent<PhotonView>() != null && effect.GetComponent<PhotonView>().IsMine)
                {
                    PhotonNetwork.Destroy(effect);
                }
                else
                {
                    Destroy(effect);
                }
                strengthEffects.Remove(effect);
            }
        }
        
        // Nothing more to do if no nearby militia
        if (count <= 0)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
            return;
        }
        
        // Update sprite color based on number of nearby militia
        if (spriteRenderer != null)
        {
            float intensity = Mathf.Min(count * 0.2f, 1f);
            spriteRenderer.color = Color.Lerp(baseColor, strengthColor, intensity);
        }
        
        // Spawn strength effect if enough nearby militia (2+)
        if (count >= 2 && strengthEffectPrefab != null && photonView.IsMine)
        {
            // Use PhotonNetwork.Instantiate to create a networked effect
            GameObject effect = PhotonNetwork.Instantiate(
                strengthEffectPrefab.name,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );
            
            // Scale effect based on bonus
            float scale = 1f + (count * 0.1f);
            effect.transform.localScale = Vector3.one * scale;
            
            // Use RPC to ensure all clients attach the effect to the right unit
            int effectViewID = effect.GetComponent<PhotonView>().ViewID;
            photonView.RPC("RPCAttachStrengthEffect", RpcTarget.AllBuffered, effectViewID);
            
            // Add a safety timer to destroy effect if it gets orphaned
            StartCoroutine(SafetyDestroyEffect(effect, 30f));
        }
    }
    
    [PunRPC]
    private void RPCAttachStrengthEffect(int effectViewID)
    {
        // Find the effect by its view ID
        PhotonView effectView = PhotonView.Find(effectViewID);
        if (effectView == null) return;
        
        GameObject effect = effectView.gameObject;
        
        // Set the parent
        effect.transform.SetParent(transform);
        
        // Reset local position to be above the unit
        effect.transform.localPosition = Vector3.up * 0.5f;
        
        // Set sorting order on any renderers in the effect
        Renderer[] renderers = effect.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.sortingOrder = particlesSortingOrder;
        }
        
        // Add to our list if this is our effect
        if (effectView.IsMine && !strengthEffects.Contains(effect))
        {
            strengthEffects.Add(effect);
        }
        
        Debug.Log($"Attached strength effect {effectViewID} to unit {gameObject.name}");
    }
    
    private IEnumerator SafetyDestroyEffect(GameObject effect, float maxLifetime)
    {
        yield return new WaitForSeconds(maxLifetime);
        if (effect != null)
        {
            PhotonView view = effect.GetComponent<PhotonView>();
            if (view != null && view.IsMine)
            {
                PhotonNetwork.Destroy(effect);
                Debug.Log("Safety cleanup of strength effect");
            }
        }
    }

    public override float GetAttackDamage()
    {
        float baseDamage = base.GetAttackDamage();
        
        // Add bonus damage based on number of nearby militia
        if (nearbyMilitiaCount > 0)
        {
            // Each nearby militia adds 10% damage, up to 40%
            float damageBonus = Mathf.Min(nearbyMilitiaCount * 0.1f, 0.4f);
            baseDamage *= (1 + damageBonus);
        }
        
        return baseDamage;
    }

    private void CleanupStrengthEffects()
    {
        // Clean up any effects we own
        foreach (GameObject effect in strengthEffects.ToList())
        {
            if (effect != null)
            {
                PhotonView view = effect.GetComponent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    // Only destroy if we own it
                    PhotonNetwork.Destroy(effect);
                    Debug.Log($"Cleaned up strength effect: {effect.name}");
                }
                else if (view != null)
                {
                    // We don't own it, so just detach it from our unit but don't destroy
                    effect.transform.SetParent(null);
                    Debug.Log($"Detached non-owned strength effect: {effect.name}");
                }
                strengthEffects.Remove(effect);
            }
        }
        strengthEffects.Clear();
        
        // Master client can clean up orphaned effects, but only ones it owns
        if (PhotonNetwork.IsMasterClient)
        {
            try {
                GameObject[] effects = GameObject.FindGameObjectsWithTag("StrengthEffect");
                foreach (GameObject effect in effects)
                {
                    PhotonView view = effect.GetComponent<PhotonView>();
                    // Only destroy if we are the owner
                    if (view != null && (view.IsMine || view.CreatorActorNr == 0))
                    {
                        PhotonNetwork.Destroy(effect);
                        Debug.Log($"Master cleaned up orphaned strength effect: {effect.name}");
                    }
                }
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"StrengthEffect tag issue: {ex.Message}");
            }
        }
    }

    // Helper methods to access group info
    public int GetNearbyMilitiaCount()
    {
        return nearbyMilitiaCount;
    }

    public float GetGroupSpeedBonus()
    {
        return currentSpeedBonus;
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
        
        // Make sure to update the base speed value used for group bonus calculations
        baseSpeedValue = baseMoveSpeed * speedMultiplier;
        
        // Re-apply group bonus if any
        if (currentSpeedBonus > 0)
        {
            currentMoveSpeed = baseSpeedValue * (1 + currentSpeedBonus);
            
            if (movementSystem != null && movementSystem.enabled)
            {
                movementSystem.SetMoveSpeed(currentMoveSpeed);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, groupRadius);
    }
}