using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class Sorcerer : BaseUnit
{
    [Header("Sorcerer-Specific Settings")]
    [SerializeField] private float magicPenetration = 15f;

    [Header("Frostbind Ability Settings")]
    [SerializeField] private float frostbindDuration = 2.5f;
    [SerializeField] private float frostbindRadius = 10f;  // Increased to match attack range
    [SerializeField] private GameObject frostbindEffectPrefab;
    [HideInInspector] public bool freezeEffectStarted = false;

    private List<int> frozenUnitViewIDs = new List<int>();
    private Coroutine freezeCoroutine;
    private const float AUTO_UNFREEZE_SAFETY = 5f; // Maximum time any unit can be frozen

    protected override void Awake()
    {
        // Set unit-specific properties BEFORE calling base.Awake()
        unitType = UnitType.Sorcerer;
        orderType = OrderType.Arcane;
        baseHealth = 750f;
        baseDamage = 150f;
        baseAttackSpeed = 0.65f;
        baseMoveSpeed = 3f;
        attackRange = 10f;
        abilityChance = 0.05f;
        
        // Now call base.Awake after setting type and order
        base.Awake();
        
        // Initialize
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
    }

    protected override void Update()
    {
        base.Update();
        
        // Check if ability is active but freeze effect hasn't started
        if (isAbilityActive && !freezeEffectStarted && photonView.IsMine)
        {
            freezeEffectStarted = true;
            freezeCoroutine = StartCoroutine(FrostbindAbility());
        }
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        // Explicitly use the configured ability chance from inspector
        float currentChance = abilityChance;
        
        if (!isAbilityActive && UnityEngine.Random.value < currentChance)
        {
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
    }

    protected override void PerformAbilityActivation()
    {
        // This is called by the base class when ability is activated
        if (!freezeEffectStarted)
        {
            freezeEffectStarted = true;
            freezeCoroutine = StartCoroutine(FrostbindAbility());
        }
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState != GameState.BattleActive && freezeEffectStarted)
        {
            SafeUnfreezeAllUnits();
            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }
            freezeEffectStarted = false;
        }
    }

    protected override void OnDestroy()
    {
        SafeUnfreezeAllUnits();
        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
        base.OnDestroy();
    }

    public override void OnDisable()
    {
        if (photonView != null && photonView.IsMine && freezeEffectStarted)
        {
            SafeUnfreezeAllUnits();
        }
        base.OnDisable();
    }

    public override float GetAttackDamage()
    {
        return attackDamage + magicPenetration;
    }

    private IEnumerator FrostbindAbility()
    {
        // Get target from the EnemyTargeting component
        EnemyTargeting targeting = GetComponent<EnemyTargeting>();
        BaseUnit targetUnit = null;
        
        if (targeting != null)
        {
            // Access the current target from EnemyTargeting
            Transform targetTransform = targeting.GetCurrentTarget();
            if (targetTransform != null)
            {
                targetUnit = targetTransform.GetComponent<BaseUnit>();
            }
        }
        
        if (targetUnit != null && 
            targetUnit.GetTeamId() != teamId && 
            targetUnit.GetCurrentState() != UnitState.Dead)
        {
            Debug.Log($"Sorcerer freezing target from targeting component: {targetUnit.gameObject.name}");
            PhotonView enemyView = targetUnit.GetComponent<PhotonView>();
            if (enemyView != null)
            {
                photonView.RPC("RPCFreezeUnit", RpcTarget.All, enemyView.ViewID);
            }
        }
        else
        {
            Debug.Log("Sorcerer has no valid target to freeze from targeting component");
        }

        // Safety timer to ensure units are unfrozen
        float timeElapsed = 0;
        while (timeElapsed < frostbindDuration && photonView != null && photonView.IsMine)
        {
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        if (photonView != null && photonView.IsMine)
        {
            SafeUnfreezeAllUnits();
            DeactivateAbility();
            freezeEffectStarted = false;
        }
        
        freezeCoroutine = null;
    }

    [PunRPC]
    private void RPCFreezeUnit(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit enemy = targetView.GetComponent<BaseUnit>();
        if (enemy == null || enemy.GetCurrentState() == UnitState.Dead) return;

        // Skip if already frozen
        if (frozenUnitViewIDs.Contains(targetViewID)) return;

        // Add to frozen list
        frozenUnitViewIDs.Add(targetViewID);

        // Apply blue tint
        SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.7f, 0.7f, 1.0f); // Light blue tint
        }

        // Spawn freeze effect
        if (frostbindEffectPrefab != null)
        {
            GameObject freezeEffect = Instantiate(
                frostbindEffectPrefab,
                enemy.transform.position,
                Quaternion.identity,
                enemy.transform
            );
            
            // Destroy the effect after the freeze duration
            Destroy(freezeEffect, frostbindDuration);
        }

        // Disable the enemy unit's components
        DisableUnit(enemy);
        
        // Start a safety timer to auto-unfreeze this unit
        if (photonView.IsMine)
        {
            StartCoroutine(SafetyUnfreezeTimer(targetViewID));
        }
    }
    
    private IEnumerator SafetyUnfreezeTimer(int targetViewID)
    {
        yield return new WaitForSeconds(AUTO_UNFREEZE_SAFETY);
        
        // Check if this unit is still frozen
        if (frozenUnitViewIDs.Contains(targetViewID))
        {
            try
            {
                photonView.RPC("RPCUnfreezeUnit", RpcTarget.All, targetViewID);
            }
            catch (System.Exception)
            {
                // Ignore exceptions, just ensuring safety
            }
        }
    }
    
    private void DisableUnit(BaseUnit unit)
    {
        // Immediately update state to Idle
        unit.photonView.RPC("RPCUpdateState", RpcTarget.All, (int)UnitState.Idle);
        
        // Disable all important control components
        DisableComponent<MovementSystem>(unit);
        DisableComponent<CombatSystem>(unit);
        DisableComponent<EnemyTargeting>(unit);
        
        // Deactivate any active ability for all unit types
        unit.photonView.RPC("RPCDeactivateAbility", RpcTarget.All);
        
        // Handle Sorcerer units specially to prevent freeze effects
        Sorcerer sorcererUnit = unit.GetComponent<Sorcerer>();
        if (sorcererUnit != null && sorcererUnit != this) // Prevent recursion if freezing another sorcerer
        {
            // Cancel any ongoing freeze effects
            if (sorcererUnit.freezeEffectStarted)
            {
                if (sorcererUnit.freezeCoroutine != null)
                {
                    sorcererUnit.StopCoroutine(sorcererUnit.freezeCoroutine);
                    sorcererUnit.freezeCoroutine = null;
                }
                sorcererUnit.freezeEffectStarted = false;
                sorcererUnit.SafeUnfreezeAllUnits();
            }
        }
    }
    
    private void DisableComponent<T>(BaseUnit unit) where T : MonoBehaviour
    {
        T component = unit.GetComponent<T>();
        if (component != null)
        {
            component.enabled = false;
            
            // Special handling for movement and targeting
            if (component is MovementSystem movement)
            {
                movement.StopMovement();
            }
            else if (component is EnemyTargeting targeting)
            {
                targeting.StopTargeting();
            }
        }
    }

    // Main unfreeze method now has its own safety measures
    private void UnfreezeAllUnits()
    {
        if (photonView.IsMine)
        {
            // Create a copy of the list to avoid modifying during enumeration
            int[] frozenIDs = frozenUnitViewIDs.ToArray();
            foreach (int viewID in frozenIDs)
            {
                photonView.RPC("RPCUnfreezeUnit", RpcTarget.All, viewID);
            }
            frozenUnitViewIDs.Clear();
        }
    }
    
    // This is our safer version with error handling
    private void SafeUnfreezeAllUnits()
    {
        if (photonView == null || !photonView.IsMine) return;
        
        // Create a copy of the list to avoid modifying during enumeration
        int[] frozenIDs = frozenUnitViewIDs.ToArray();
        foreach (int viewID in frozenIDs)
        {
            try {
                photonView.RPC("RPCUnfreezeUnit", RpcTarget.All, viewID);
            }
            catch (System.Exception) {
                // Just ignore errors to ensure other units get unfrozen
            }
        }
        frozenUnitViewIDs.Clear();
    }

    [PunRPC]
    private void RPCUnfreezeUnit(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit enemy = targetView.GetComponent<BaseUnit>();
        if (enemy == null) return;

        // Restore color
        SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        // Re-enable components
        EnableUnit(enemy);
        
        // Remove from frozen list
        frozenUnitViewIDs.Remove(targetViewID);
    }
    
    private void EnableUnit(BaseUnit unit)
    {
        // Re-enable all important control components
        EnableComponent<MovementSystem>(unit);
        EnableComponent<CombatSystem>(unit);
        EnableComponent<EnemyTargeting>(unit);
        
        // Reset state to Idle to restart behaviors
        unit.photonView.RPC("RPCUpdateState", RpcTarget.All, (int)UnitState.Idle);
    }
    
    private void EnableComponent<T>(BaseUnit unit) where T : MonoBehaviour
    {
        T component = unit.GetComponent<T>();
        if (component != null)
        {
            component.enabled = true;
            
            // Special handling for targeting
            if (component is EnemyTargeting targeting)
            {
                targeting.StartTargeting();
            }
        }
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        
        freezeEffectStarted = false;
        
        // Make sure to stop any ongoing coroutine
        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
        
        base.DeactivateAbility();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Don't call base since it's not overridable (not marked as virtual)
        // Instead, manually replicate any needed base class sync logic
        
        if (stream.IsWriting)
        {
            // Send freeze state
            stream.SendNext(freezeEffectStarted);
            stream.SendNext(frozenUnitViewIDs.Count);
            foreach(int viewID in frozenUnitViewIDs)
            {
                stream.SendNext(viewID);
            }
        }
        else
        {
            // Receive freeze state
            freezeEffectStarted = (bool)stream.ReceiveNext();
            int frozenCount = (int)stream.ReceiveNext();
            
            // Only update the list if we're not the owner
            if (!photonView.IsMine)
            {
                frozenUnitViewIDs.Clear();
                for(int i = 0; i < frozenCount; i++)
                {
                    frozenUnitViewIDs.Add((int)stream.ReceiveNext());
                }
            }
            else
            {
                // Skip the viewIDs if we're the owner
                for(int i = 0; i < frozenCount; i++)
                {
                    stream.ReceiveNext();
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, frostbindRadius);
    }
}