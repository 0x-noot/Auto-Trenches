using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class Mage : BaseUnit
{
    [Header("Mage-Specific Settings")]
    [SerializeField] private float magicPenetration = 15f;

    [Header("Freeze Ability Settings")]
    [SerializeField] private float freezeDuration = 2.5f;
    [SerializeField] private float freezeRadius = 4f;
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
    private HashSet<int> frozenUnitViewIDs = new HashSet<int>();

    private void Awake()
    {
        unitType = UnitType.Mage;
        maxHealth = 700f;
        attackDamage = 180f;
        attackRange = 10f;
        moveSpeed = 3f;
        attackSpeed = 0.7f;
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

    protected override void RPCActivateAbility()
    {
        if (!isAbilityActive && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive &&
            photonView.IsMine)
        {
            base.RPCActivateAbility();
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
                PhotonView enemyView = enemy.GetComponent<PhotonView>();
                if (enemyView != null)
                {
                    photonView.RPC("RPCFreezeUnit", RpcTarget.All, enemyView.ViewID);
                }
            }
        }

        yield return new WaitForSeconds(freezeDuration);
        
        if (photonView.IsMine)
        {
            UnfreezeAllUnits();
            DeactivateAbility();
        }
    }

    [PunRPC]
    private void RPCFreezeUnit(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit enemy = targetView.GetComponent<BaseUnit>();
        if (enemy == null || enemy.GetCurrentState() == UnitState.Dead) return;

        // First unfreeze if already frozen to prevent stacking effects
        UnfreezeUnit(enemy);

        SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        Color originalColor = spriteRenderer.color;
        Coroutine flashRoutine = StartCoroutine(FlashUnit(enemy, spriteRenderer, originalColor));
        
        frozenUnits.Add(new FrozenUnitData(enemy, originalColor, flashRoutine, spriteRenderer));
        frozenUnitViewIDs.Add(targetViewID);

        // Spawn freeze effect
        if (freezeEffectPrefab != null)
        {
            GameObject freezeEffect = Instantiate(
                freezeEffectPrefab,
                enemy.transform.position,
                Quaternion.identity,
                enemy.transform
            );
        }

        DisableUnitSystems(enemy);
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

            PhotonView unitView = unit.GetComponent<PhotonView>();
            if (unitView != null)
            {
                frozenUnitViewIDs.Remove(unitView.ViewID);
            }
        }
    }

    private void UnfreezeAllUnits()
    {
        if (photonView.IsMine)
        {
            photonView.RPC("RPCUnfreezeAllUnits", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPCUnfreezeAllUnits()
    {
        foreach (var frozenUnit in frozenUnits.ToArray())
        {
            if (frozenUnit.unit != null)
            {
                UnfreezeUnit(frozenUnit.unit);
            }
        }
        frozenUnits.Clear();
        frozenUnitViewIDs.Clear();
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, freezeRadius);
    }
}