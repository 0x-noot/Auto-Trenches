using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Mage : BaseUnit
{
    [Header("Mage-Specific Settings")]
    [SerializeField] private float magicPenetration = 10f;

    [Header("Freeze Ability Settings")]
    [SerializeField] private float freezeDuration = 3f;
    [SerializeField] private float freezeRadius = 5f;
    [SerializeField] private GameObject freezeEffectPrefab;
    [SerializeField] private float flashInterval = 0.2f;

    private class FrozenUnitData
    {
        public BaseUnit unit;
        public Color originalColor;
        public Coroutine flashRoutine;
        public SpriteRenderer spriteRenderer;

        public FrozenUnitData(BaseUnit unit, Color originalColor, Coroutine flashRoutine, SpriteRenderer spriteRenderer)
        {
            this.unit = unit;
            this.originalColor = originalColor;
            this.flashRoutine = flashRoutine;
            this.spriteRenderer = spriteRenderer;
        }
    }

    private List<FrozenUnitData> frozenUnits = new List<FrozenUnitData>();

    private void Awake()
    {
        unitType = UnitType.Mage;
        maxHealth = 800f;
        attackDamage = 200f;
        attackRange = 10f;
        moveSpeed = 3f;
        attackSpeed = 0.8f;
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState != GameState.BattleActive && isAbilityActive)
        {
            StopAllCoroutines();
            UnfreezeAllUnits();
        }
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Dead)
        {
            StopAllCoroutines();
            UnfreezeAllUnits();
        }
        
        base.UpdateState(newState);
    }

    protected override void OnDestroy()
    {
        StopAllCoroutines();
        UnfreezeAllUnits();
        base.OnDestroy();
    }

    public override float GetAttackDamage()
    {
        return attackDamage + magicPenetration;
    }

    protected override void ActivateAbility()
    {
        if (!isAbilityActive && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive)
        {
            base.ActivateAbility();
            StartCoroutine(FreezeAbility());
        }
    }

    private IEnumerator FreezeAbility()
    {
        string enemyLayer = teamId == "TeamA" ? "TeamB" : "TeamA";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            transform.position, 
            freezeRadius, 
            LayerMask.GetMask(enemyLayer)
        );

        foreach (Collider2D col in hitColliders)
        {
            BaseUnit enemy = col.GetComponent<BaseUnit>();
            if (enemy != null && enemy.GetCurrentState() != UnitState.Dead)
            {
                FreezeUnit(enemy);
            }
        }

        yield return new WaitForSeconds(freezeDuration);
        UnfreezeAllUnits();
        DeactivateAbility();
    }

    private void FreezeUnit(BaseUnit unit)
    {
        // First unfreeze if already frozen to prevent stacking effects
        UnfreezeUnit(unit);

        SpriteRenderer spriteRenderer = unit.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        Color originalColor = spriteRenderer.color;
        Coroutine flashRoutine = StartCoroutine(FlashUnit(unit, spriteRenderer, originalColor));
        
        frozenUnits.Add(new FrozenUnitData(unit, originalColor, flashRoutine, spriteRenderer));

        // Spawn freeze effect
        if (freezeEffectPrefab != null)
        {
            GameObject freezeEffect = Instantiate(
                freezeEffectPrefab,
                unit.transform.position,
                Quaternion.identity,
                unit.transform
            );
            
            ParticleSystem particles = freezeEffect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                particles.Play();
            }
        }

        DisableUnitSystems(unit);
    }

    private IEnumerator FlashUnit(BaseUnit unit, SpriteRenderer spriteRenderer, Color originalColor)
    {
        while (unit != null && unit.GetCurrentState() != UnitState.Dead && isAbilityActive)
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(flashInterval);
            
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(flashInterval);
        }
    }

    private void DisableUnitSystems(BaseUnit unit)
    {
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.StopMovement();
            movement.enabled = false;
        }

        CombatSystem combat = unit.GetComponent<CombatSystem>();
        if (combat != null) combat.enabled = false;

        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.StopTargeting();
            targeting.enabled = false;
        }
    }

    private void EnableUnitSystems(BaseUnit unit)
    {
        if (unit == null || unit.GetCurrentState() == UnitState.Dead) return;

        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null) movement.enabled = true;

        CombatSystem combat = unit.GetComponent<CombatSystem>();
        if (combat != null) combat.enabled = true;

        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.enabled = true;
            targeting.StartTargeting();
        }
    }

    private void UnfreezeUnit(BaseUnit unit)
    {
        var frozenUnit = frozenUnits.Find(fu => fu.unit == unit);
        if (frozenUnit != null)
        {
            if (frozenUnit.flashRoutine != null)
            {
                StopCoroutine(frozenUnit.flashRoutine);
            }

            if (frozenUnit.spriteRenderer != null)
            {
                frozenUnit.spriteRenderer.color = frozenUnit.originalColor;
            }

            EnableUnitSystems(frozenUnit.unit);
            frozenUnits.Remove(frozenUnit);
        }
    }

    private void UnfreezeAllUnits()
    {
        foreach (var frozenUnit in frozenUnits.ToArray())
        {
            if (frozenUnit.unit != null)
            {
                UnfreezeUnit(frozenUnit.unit);
            }
        }
        frozenUnits.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, freezeRadius);
    }
}