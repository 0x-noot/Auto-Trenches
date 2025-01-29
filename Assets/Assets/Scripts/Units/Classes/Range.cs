using UnityEngine;

public class Range : BaseUnit
{
    private void Awake()
    {
        unitType = UnitType.Range;
        maxHealth = 100;
        attackDamage = 20;
        attackRange = 3f;
        moveSpeed = 3.5f;
    }

    protected override void Start()
    {
        base.Start();
    }

    public override float GetAttackRange()
    {
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
}