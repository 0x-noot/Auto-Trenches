using UnityEngine;

public class Range : BaseUnit
{
    [Header("Range-Specific Settings")]
    [SerializeField] private float longRangeBonus = 5f;
    [SerializeField] private float longRangeThreshold = 4f;

    private void Awake()
    {
        unitType = UnitType.Range;
        maxHealth = 900f;
        attackDamage = 125f;
        attackRange = 6f;
        moveSpeed = 3.2f;
        attackSpeed = 1f;
    }

    public override float GetAttackDamage()
    {
        if (currentTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget >= longRangeThreshold)
            {
                return attackDamage + longRangeBonus;
            }
        }
        return attackDamage;
    }
}