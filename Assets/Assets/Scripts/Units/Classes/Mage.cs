using UnityEngine;

public class Mage : BaseUnit
{
    [Header("Mage-Specific Settings")]
    [SerializeField] private float magicPenetration = 10f;

    private void Awake()
    {
        unitType = UnitType.Mage;
        maxHealth = 80f;
        attackDamage = 40f;
        attackRange = 5f;
        moveSpeed = 3f;
        attackSpeed = 0.8f;
    }

    public override float GetAttackDamage()
    {
        return attackDamage + magicPenetration;
    }
}
