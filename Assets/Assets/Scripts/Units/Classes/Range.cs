using UnityEngine;
using System.Collections.Generic;

public class Range : BaseUnit
{
    [Header("Range-Specific Settings")]
    [SerializeField] private float longRangeBonus = 5f;
    [SerializeField] private float longRangeThreshold = 4f;

    [Header("Explosion Ability Settings")]
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionDamageMultiplier = 0.5f; // 50% of normal damage
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private bool isExplosiveArrow = false;

    private void Awake()
    {
        unitType = UnitType.Range;
        maxHealth = 900f;
        attackDamage = 125f;
        attackRange = 15f;
        moveSpeed = 3.2f;
        attackSpeed = 1f;
    }

    protected override void TryActivateAbility()
    {
        Debug.Log($"Range unit {gameObject.name}: Trying to activate ability. Can activate: {!isAbilityActive && UnityEngine.Random.value < abilityChance}");
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
            Debug.Log($"Range unit {gameObject.name}: Explosive arrows activated!");
        }
    }

    protected override void DeactivateAbility()
    {
        isExplosiveArrow = false;
        base.DeactivateAbility();
        Debug.Log($"Range unit {gameObject.name} deactivated explosive arrows!");
    }

    public bool IsExplosiveArrow()
    {
        return isExplosiveArrow;
    }

    public void CreateExplosion(Vector3 position, BaseUnit primaryTarget)
    {
        Debug.Log($"Range unit {gameObject.name}: Creating explosion at {position}. IsExplosiveArrow: {isExplosiveArrow}");
        if (!isExplosiveArrow) return;

        // Spawn explosion effect
        if (explosionEffectPrefab != null)
        {
            Debug.Log($"Range unit {gameObject.name}: Spawning explosion effect");
            Instantiate(explosionEffectPrefab, position, Quaternion.identity);
        }
        else
        {
            Debug.LogError($"Range unit {gameObject.name}: No explosion effect prefab assigned!");
        }

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
                enemy != primaryTarget && // Don't damage primary target again
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