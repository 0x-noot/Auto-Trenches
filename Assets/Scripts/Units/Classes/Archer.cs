using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class Archer : BaseUnit
{
    [Header("Archer-Specific Settings")]
    [SerializeField] private float longRangeBonus = 20f;
    [SerializeField] private float longRangeThreshold = 4f;

    [Header("Blazing Volley Ability Settings")]
    [SerializeField] private float explosionRadius = 3.5f;
    [SerializeField] private float explosionDamageMultiplier = 0.35f;
    [SerializeField] private GameObject explosionEffectPrefab;
    private bool isBlazingVolleyActive = false;


    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Archer;
        orderType = OrderType.Arcane;
        baseHealth = 750f;
        baseDamage = 100f;
        baseAttackSpeed = 0.8f;
        baseMoveSpeed = 3f;
        attackRange = 12f;
        abilityChance = 0.06f;
        
        base.Awake();
        
        // Initialize
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        
        Debug.Log($"Archer unit initialized with type: {unitType}, order: {orderType}");
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating Blazing Volley ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }
    
    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        isBlazingVolleyActive = true;
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        isBlazingVolleyActive = false;
        base.DeactivateAbility();
    }

    public bool IsBlazingVolleyActive()
    {
        return isBlazingVolleyActive;
    }

    public void CreateExplosion(Vector3 position, BaseUnit primaryTarget)
    {
        Debug.Log($"CreateExplosion called on {gameObject.name}");
        Debug.Log($"isBlazingVolleyActive: {isBlazingVolleyActive}, IsMine: {photonView.IsMine}");

        if (!isBlazingVolleyActive || !photonView.IsMine)
        {
            Debug.Log("CreateExplosion early return - conditions not met");
            return;
        }

        if (explosionEffectPrefab != null)
        {
            Debug.Log($"Attempting to instantiate explosion at position: {position}");
            GameObject explosion = PhotonNetwork.Instantiate(
                explosionEffectPrefab.name, 
                position, 
                Quaternion.identity
            );
            Debug.Log($"Explosion instantiated: {explosion != null}");
        }
        else
        {
            Debug.LogError("ExplosionEffectPrefab is null!");
        }

        // Apply explosion damage
        string enemyLayer = teamId == "TeamA" ? "TeamB" : "TeamA";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            position,
            explosionRadius,
            LayerMask.GetMask(enemyLayer)
        );

        float explosionDamage = GetAttackDamage() * explosionDamageMultiplier;
        
        foreach (Collider2D col in hitColliders)
        {
            BaseUnit enemy = col.GetComponent<BaseUnit>();
            if (enemy != null && 
                enemy != primaryTarget && 
                enemy.GetCurrentState() != UnitState.Dead)
            {
                enemy.TakeDamage(explosionDamage);
            }
        }

        // 30% chance to end ability after explosion
        if (UnityEngine.Random.value < 0.3f)
        {
            DeactivateAbility();
        }
    }

    public override float GetAttackDamage()
    {
        float baseDamage = attackDamage;
        
        // Apply Arcane order synergy if target is affected by ability
        if (orderType == OrderType.Arcane && 
            currentTarget != null && 
            currentTarget.IsAbilityActive() &&
            synergyBonuses.ContainsKey("Arcane_affectedTargetDamage"))
        {
            float bonusMultiplier = synergyBonuses["Arcane_affectedTargetDamage"];
            baseDamage *= (1f + bonusMultiplier);
        }
        
        // Apply long range bonus
        if (currentTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget >= longRangeThreshold)
            {
                baseDamage += longRangeBonus;
            }
        }

        return baseDamage;
    }

    private void OnDrawGizmosSelected()
    {
        if (isBlazingVolleyActive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}