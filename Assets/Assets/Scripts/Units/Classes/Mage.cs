using UnityEngine;

public class Mage : BaseUnit
{
    private void Awake()
    {
        unitType = UnitType.Mage;
        maxHealth = 100;
        attackDamage = 30;
        attackRange = 5f;
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