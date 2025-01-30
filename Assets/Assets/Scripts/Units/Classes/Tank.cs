using UnityEngine;

public class Tank : BaseUnit
{
    [Header("Tank-Specific Settings")]
    [SerializeField] private float armorBonus = 20f;

    private void Awake()
    {
        unitType = UnitType.Tank;
        maxHealth = 200f;
        attackDamage = 10f;
        attackRange = 3.5f;
        moveSpeed = 2f;
        attackSpeed = 0.8f;
    }

    public override void TakeDamage(float damage)
    {
        float reducedDamage = damage * (100f / (100f + armorBonus));
        base.TakeDamage(reducedDamage);
    }
}