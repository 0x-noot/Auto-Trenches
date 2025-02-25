using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class Range : BaseUnit
{
    [Header("Range-Specific Settings")]
    [SerializeField] private float longRangeBonus = 15f;
    [SerializeField] private float longRangeThreshold = 4f;

    [Header("Explosion Ability Settings")]
    [SerializeField] private float explosionRadius = 3.5f;
    [SerializeField] private float explosionDamageMultiplier = 0.3f;
    [SerializeField] private GameObject explosionEffectPrefab;
    private bool isExplosiveArrow = false;

    private void Awake()
    {
        unitType = UnitType.Range;
        maxHealth = 700f;
        attackDamage = 120f;
        attackRange = 12f;
        moveSpeed = 3f;
        attackSpeed = 0.9f;
        abilityChance = 0.06f;
        base.Awake();
        Debug.Log($"Range unit initialized: {gameObject.name}");
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"TryActivateAbility called. Current chance: {abilityChance}, isActive: {isAbilityActive}");
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            Debug.Log("Activating explosive arrow ability!");
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }
    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        isExplosiveArrow = true;
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        isExplosiveArrow = false;
        base.DeactivateAbility();
    }

    public bool IsExplosiveArrow()
    {
        return isExplosiveArrow;
    }

    public void CreateExplosion(Vector3 position, BaseUnit primaryTarget)
    {
        Debug.Log($"CreateExplosion called on {gameObject.name}");
        Debug.Log($"isExplosiveArrow: {isExplosiveArrow}, IsMine: {photonView.IsMine}");


        if (!isExplosiveArrow || !photonView.IsMine)
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
        if (isExplosiveArrow)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}