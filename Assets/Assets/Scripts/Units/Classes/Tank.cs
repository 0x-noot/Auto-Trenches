using UnityEngine;

public class Tank : BaseUnit
{
    private void Awake()
    {
        // Set Tank-specific stats
        unitType = UnitType.Tank;
        maxHealth = 200;
        attackDamage = 10;
        attackRange = 1f;
        moveSpeed = 2f;
    }

    protected override void Start()
    {
        base.Start();
    }

    // Properly override the getter methods
    public override float GetAttackRange()
    {
        Debug.Log($"[{gameObject.name}] Getting attack range from Tank: {attackRange}");
        return attackRange;
    }

    public override float GetAttackDamage()
    {
        return attackDamage;
    }

    public override float GetMoveSpeed()
    {
        return moveSpeed;
    }

    public override UnitType GetUnitType()
    {
        return unitType;
    }
}