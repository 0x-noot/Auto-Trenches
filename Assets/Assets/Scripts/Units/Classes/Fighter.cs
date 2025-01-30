using UnityEngine;

public class Fighter : BaseUnit
{
    [Header("Fighter-Specific Settings")]
    [SerializeField] private float criticalStrikeChance = 0.2f;

    private void Awake()
    {
        unitType = UnitType.Fighter;
        maxHealth = 100f;
        attackDamage = 30f;
        attackRange = 3.5f;
        moveSpeed = 3.5f;
        attackSpeed = 1.2f;
    }

    public override float GetAttackDamage()
    {
        // Chance for critical strike
        if (Random.value < criticalStrikeChance)
        {
            return attackDamage * 1.5f;
        }
        return attackDamage;
    }
}