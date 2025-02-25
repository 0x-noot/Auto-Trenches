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
    [SerializeField] private float freezeRadius = 10f;  // Increased to match attack range
    [SerializeField] private GameObject freezeEffectPrefab;
    [HideInInspector] public bool freezeEffectStarted = false;

    private List<int> frozenUnitViewIDs = new List<int>();
    private Coroutine freezeCoroutine;
    private const float AUTO_UNFREEZE_SAFETY = 5f; // Maximum time any unit can be frozen

    private void Awake()
    {
        unitType = UnitType.Mage;
        baseHealth = 700f;
        baseDamage = 180f;
        attackRange = 10f;
        baseMoveSpeed = 3f;
        baseAttackSpeed = 0.7f;
        
        // Explicitly set ability chance from BaseUnit
        abilityChance = 0.03f;
        
        // Set current stats equal to base stats initially
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
            freezeCoroutine = StartCoroutine(FreezeAbility());
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
            freezeCoroutine = StartCoroutine(FreezeAbility());
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

    private IEnumerator FreezeAbility()
    {
        // Find all BaseUnit components in the scene
        BaseUnit[] allUnits = FindObjectsOfType<BaseUnit>();
        
        foreach (BaseUnit unit in allUnits)
        {
            // Check if it's an enemy unit in range
            if (unit != null && 
                unit != this && 
                unit.GetTeamId() != teamId && 
                unit.GetCurrentState() != UnitState.Dead)
            {
                float distance = Vector3.Distance(transform.position, unit.transform.position);
                
                if (distance <= freezeRadius)
                {
                    PhotonView enemyView = unit.GetComponent<PhotonView>();
                    if (enemyView != null)
                    {
                        photonView.RPC("RPCFreezeUnit", RpcTarget.All, enemyView.ViewID);
                    }
                }
            }
        }

        // Safety timer to ensure units are unfrozen
        float timeElapsed = 0;
        while (timeElapsed < freezeDuration && photonView != null && photonView.IsMine)
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
        if (freezeEffectPrefab != null)
        {
            GameObject freezeEffect = Instantiate(
                freezeEffectPrefab,
                enemy.transform.position,
                Quaternion.identity,
                enemy.transform
            );
            
            // Destroy the effect after the freeze duration
            Destroy(freezeEffect, freezeDuration);
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
        
        // Handle Mage units specially to prevent freeze effects
        Mage mageUnit = unit.GetComponent<Mage>();
        if (mageUnit != null && mageUnit != this) // Prevent recursion if freezing another mage
        {
            // Cancel any ongoing freeze effects
            if (mageUnit.freezeEffectStarted)
            {
                if (mageUnit.freezeCoroutine != null)
                {
                    mageUnit.StopCoroutine(mageUnit.freezeCoroutine);
                    mageUnit.freezeCoroutine = null;
                }
                mageUnit.freezeEffectStarted = false;
                mageUnit.SafeUnfreezeAllUnits();
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
        Gizmos.DrawWireSphere(transform.position, freezeRadius);
    }
}