using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class Range : BaseUnit
{
    [Header("Range-Specific Settings")]
    [SerializeField] private float longRangeBonus = 15f;
    [SerializeField] private float longRangeThreshold = 4f;

    [Header("Explosion Ability Settings")]
    [SerializeField] private float explosionRadius = 3.5f;
    [SerializeField] private float explosionDamageMultiplier = 0.3f;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private bool isExplosiveArrow = false;

    private void Awake()
    {
        unitType = UnitType.Range;
        maxHealth = 700f;
        attackDamage = 120f;
        attackRange = 12f;
        moveSpeed = 3f;
        attackSpeed = 0.9f;
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            ActivateAbility();
            nextAbilityTime = Time.time + baseAbilityCooldown;
        }
    }

    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        photonView.RPC("RPCSetExplosiveArrow", RpcTarget.All, true);
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCSetExplosiveArrow", RpcTarget.All, false);
        base.DeactivateAbility();
    }

    [PunRPC]
    private void RPCSetExplosiveArrow(bool explosive)
    {
        isExplosiveArrow = explosive;
    }

    public bool IsExplosiveArrow()
    {
        return isExplosiveArrow;
    }

    public void CreateExplosion(Vector3 position, BaseUnit primaryTarget)
    {
        if (!isExplosiveArrow || !photonView.IsMine) return;

        photonView.RPC("RPCCreateExplosion", RpcTarget.All, position, primaryTarget.photonView.ViewID);
    }

    [PunRPC]
    private void RPCCreateExplosion(Vector3 position, int primaryTargetViewID)
    {
        // Spawn explosion effect
        if (explosionEffectPrefab != null)
        {
            GameObject explosionEffect = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
            Destroy(explosionEffect, 2f);
        }

        // Only the owner calculates and applies damage
        if (!photonView.IsMine) return;

        // Get primary target
        PhotonView primaryTargetView = PhotonView.Find(primaryTargetViewID);
        if (primaryTargetView == null) return;
        BaseUnit primaryTarget = primaryTargetView.GetComponent<BaseUnit>();
        if (primaryTarget == null) return;

        // Get all units in explosion radius
        string enemyLayer = teamId == "TeamA" ? "TeamB" : "TeamA";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            position,
            explosionRadius,
            LayerMask.GetMask(enemyLayer)
        );

        HashSet<BaseUnit> affectedUnits = new HashSet<BaseUnit>();
        
        foreach (Collider2D col in hitColliders)
        {
            BaseUnit enemy = col.GetComponent<BaseUnit>();
            if (enemy != null && 
                enemy != primaryTarget && 
                enemy.GetCurrentState() != UnitState.Dead)
            {
                affectedUnits.Add(enemy);
            }
        }

        // Calculate and apply explosion damage
        float explosionDamage = GetAttackDamage() * explosionDamageMultiplier;
        foreach (BaseUnit unit in affectedUnits)
        {
            unit.TakeDamage(explosionDamage);
        }

        // Small chance to deactivate ability after explosion
        if (Random.value < 0.3f) // 30% chance to end
        {
            DeactivateAbility();
        }
    }

    public override float GetAttackDamage()
    {
        float baseDamage = attackDamage;
        
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

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        if (isExplosiveArrow)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}