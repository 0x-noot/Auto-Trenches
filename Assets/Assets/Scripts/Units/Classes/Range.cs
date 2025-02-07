using UnityEngine;
using System.Collections.Generic;

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
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            ActivateAbility();
            nextAbilityTime = Time.time + baseAbilityCooldown;
        }
    }

    protected override void ActivateAbility()
    {
        if (!isAbilityActive && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive)
        {
            base.ActivateAbility();
            isExplosiveArrow = true;
        }
    }

    protected override void DeactivateAbility()
    {
        isExplosiveArrow = false;
        base.DeactivateAbility();
    }

    public bool IsExplosiveArrow()
    {
        return isExplosiveArrow;
    }

    public void CreateExplosion(Vector3 position, BaseUnit primaryTarget)
    {
        if (!isExplosiveArrow) return;

        // Use object pool instead of Instantiate
        GameObject explosionObj = ObjectPool.Instance.SpawnFromPool("ExplosionEffect", position, Quaternion.identity);
        
        if (explosionObj == null)
        {
            Debug.LogError("Failed to spawn explosion effect from pool");
            return;
        }

        // Get affected units and apply damage
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

        float explosionDamage = GetAttackDamage() * explosionDamageMultiplier;
        foreach (BaseUnit unit in affectedUnits)
        {
            unit.TakeDamage(explosionDamage);
        }

        if (Random.value < 0.3f)
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
        // Draw the explosion radius in the editor
        if (isExplosiveArrow)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}