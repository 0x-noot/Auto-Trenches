using UnityEngine;
using System.Collections;
using Photon.Pun;

public class CombatSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    // Backing field for lazy initialization
    private BaseUnit _unit;
    // Property that initializes _unit on first access
    private BaseUnit unit 
    {
        get 
        {
            if (_unit == null) 
            {
                _unit = GetComponent<BaseUnit>();
                Debug.Log($"Lazy init: {gameObject.name} unit type = {_unit.GetUnitType()}");
            }
            return _unit;
        }
    }
    
    private float nextAttackTime = 0f;
    
    [Header("Combat Settings")]
    [SerializeField] private float attackAnimationDuration = 0.5f;
    
    [Header("Melee Attack Settings")]
    [SerializeField] private GameObject meleeAttackEffectPrefab;
    [SerializeField] private float meleeAttackRecoil = 0.3f;
    [SerializeField] private float meleeAttackLunge = 0.5f;
    
    [Header("Archer Attack Settings")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowSpeed = 15f;
    [SerializeField] private float arrowArcHeight = 1f;
    [SerializeField] private float arrowHomingStrength = 0.8f;
    
    [Header("Sorcerer Attack Settings")]
    [SerializeField] private GameObject spellPrefab;
    [SerializeField] private float spellSpeed = 10f;
    [SerializeField] private float spellCastDelay = 0.2f;
    
    private void Awake()
    {
        // Note: Not setting _unit directly here, will use lazy-loaded property instead
        if (!TryGetComponent<EnemyTargeting>(out var targeting))
        {
            Debug.LogError($"CombatSystem requires EnemyTargeting component on {gameObject.name}!");
        }

        // Delay validation to Start to ensure unit type is initialized
        Invoke("ValidateReferences", 0.1f);
    }

    private void ValidateReferences()
    {
        if (unit == null)
        {
            Debug.LogError($"No BaseUnit component found on {gameObject.name}!");
            return;
        }

        // Add debug to see what type is being reported
        Debug.Log($"Unit {gameObject.name} has type: {unit.GetUnitType()}");

        // Properly check the unit type
        UnitType unitType = unit.GetUnitType();
        
        // Handle each unit type correctly
        if (unitType == UnitType.Archer)
        {
            if (arrowPrefab == null)
                Debug.LogError($"Arrow prefab is missing for Archer unit {gameObject.name}!");
        }
        else if (unitType == UnitType.Sorcerer)
        {
            if (spellPrefab == null)
                Debug.LogError($"Spell prefab is missing for Sorcerer unit {gameObject.name}!");
        }
        else if (unitType == UnitType.Knight || unitType == UnitType.Berserker)
        {
            if (meleeAttackEffectPrefab == null)
                Debug.LogError($"Melee attack effect prefab is missing for {unitType} unit {gameObject.name}!");
        }
        else
        {
            Debug.LogWarning($"Unknown unit type {unitType} for {gameObject.name}");
        }
    }

    public bool CanAttack()
    {
        return Time.time >= nextAttackTime && unit.GetCurrentState() != UnitState.Dead;
    }

    public void ExecuteAttack(BaseUnit target)
    {
        if (!CanAttack() || target == null || target.GetCurrentState() == UnitState.Dead || !photonView.IsMine)
        {
            return;
        }

        nextAttackTime = Time.time + (1f / unit.GetAttackSpeed());
        
        // Store target view ID before RPC
        int targetViewID = target.photonView.ViewID;
        photonView.RPC("RPCExecuteAttack", RpcTarget.All, targetViewID);
    }

    [PunRPC]
    private void RPCExecuteAttack(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null)
        {
            Debug.LogError($"Cannot find target with ViewID {targetViewID}");
            return;
        }

        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null || target.GetCurrentState() == UnitState.Dead)
        {
            return;
        }

        // Use the same approach for determining attack type as in ValidateReferences
        UnitType unitType = unit.GetUnitType();
        
        // Add your new units to the existing conditionals
        if (unitType == UnitType.Knight || unitType == UnitType.Berserker || 
            unitType == UnitType.Barbarian || unitType == UnitType.PeasantMilitia)
        {
            StartCoroutine(PerformMeleeAttackSequence(target));
        }
        else if (unitType == UnitType.Archer)
        {
            StartCoroutine(PerformRangedAttackSequence(target));
        }
        else if (unitType == UnitType.Sorcerer || unitType == UnitType.Cleric)
        {
            StartCoroutine(PerformMageAttackSequence(target));
        }
    }

    [PunRPC]
    private void RPCApplyDamage(int targetViewID, float damage)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target != null && target.GetCurrentState() != UnitState.Dead)
        {
            // Directly call TakeDamage
            target.TakeDamage(damage);
        }
    }
    
    private IEnumerator PerformMeleeAttackSequence(BaseUnit target)
    {
        Vector3 originalPosition = transform.position;
        Vector3 targetPosition = target.transform.position;
        Vector3 attackDirection = (targetPosition - originalPosition).normalized;

        yield return StartCoroutine(PerformLunge(originalPosition, attackDirection));

        if (photonView.IsMine)
        {
            // For melee effects, use PhotonNetwork.Instantiate
            if (meleeAttackEffectPrefab != null)
            {
                GameObject meleeEffect = PhotonNetwork.Instantiate(
                    meleeAttackEffectPrefab.name, 
                    transform.position, 
                    Quaternion.identity
                );
                
                if (meleeEffect != null)
                {
                    MeleeAttackEffect effect = meleeEffect.GetComponent<MeleeAttackEffect>();
                    if (effect != null)
                    {
                        effect.SetupEffect(originalPosition, targetPosition);
                    }
                    
                    // Destroy after delay - need to use PhotonNetwork.Destroy
                    StartCoroutine(DestroyAfterDelay(meleeEffect, 1f));
                }
            }
            
            // Apply damage with buffered RPC
            photonView.RPC("RPCApplyDamage", RpcTarget.AllBuffered, target.photonView.ViewID, unit.GetAttackDamage());
        }

        yield return StartCoroutine(PerformRecoil(originalPosition, attackDirection));
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Destroy(obj);
        }
    }

    private IEnumerator PerformRangedAttackSequence(BaseUnit target)
    {
        if (target == null || target.GetCurrentState() == UnitState.Dead)
        {
            yield break;
        }

        // Apply damage immediately
        if (photonView.IsMine && target != null && target.GetCurrentState() != UnitState.Dead)
        {
            float damage = unit.GetAttackDamage();
            photonView.RPC("RPCApplyDamage", RpcTarget.AllBuffered, target.photonView.ViewID, damage);
        
            // Only spawn arrow if we're the owner
            Vector3 spawnOffset = transform.up * 0.5f;
            Vector3 spawnPosition = transform.position + spawnOffset;
            
            GameObject arrowObj = PhotonNetwork.Instantiate(
                arrowPrefab.name,
                spawnPosition,
                Quaternion.identity
            );
            
            if (arrowObj != null)
            {
                ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
                if (arrow != null)
                {
                    Archer archerUnit = unit as Archer;
                    arrow.Initialize(archerUnit, target);
                    arrow.StartFlight();
                    arrow.MoveToTarget(target.transform.position, arrowSpeed);
                }
            }
        }
    }

    private IEnumerator PerformMageAttackSequence(BaseUnit target)
    {
        if (target == null || target.GetCurrentState() == UnitState.Dead)
        {
            yield break;
        }
        
        yield return new WaitForSeconds(spellCastDelay);

        // Apply damage immediately
        if (photonView.IsMine && target != null && target.GetCurrentState() != UnitState.Dead)
        {
            float damage = unit.GetAttackDamage();
            photonView.RPC("RPCApplyDamage", RpcTarget.AllBuffered, target.photonView.ViewID, damage);
            
            // Only spawn spell if we're the owner
            GameObject spellObj = PhotonNetwork.Instantiate(
                spellPrefab.name,
                transform.position,
                Quaternion.identity
            );
            
            if (spellObj != null)
            {
                MagicProjectile spell = spellObj.GetComponent<MagicProjectile>();
                if (spell != null)
                {
                    spell.MoveToTarget(target.transform.position, spellSpeed);
                }
            }
        }
    }

    private IEnumerator AnimateSpell(MagicProjectile spell, BaseUnit target)
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = target.transform.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / spellSpeed;

        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            if (spell == null)
            {
                break;
            }

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            spell.transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            yield return null;
        }

        if (spell != null)
        {
            spell.OnSpellHit();
            if (photonView.IsMine && target != null && target.GetCurrentState() != UnitState.Dead)
            {
                photonView.RPC("RPCApplyDamage", RpcTarget.AllBuffered, target.photonView.ViewID, unit.GetAttackDamage());
            }
        }
    }

    private IEnumerator PerformLunge(Vector3 originalPosition, Vector3 direction)
    {
        float elapsedTime = 0f;
        float lungeDuration = attackAnimationDuration * 0.3f;
        Vector3 lungePosition = originalPosition + (direction * meleeAttackLunge);
        
        while (elapsedTime < lungeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / lungeDuration;
            transform.position = Vector3.Lerp(originalPosition, lungePosition, t);
            yield return null;
        }
    }

    private IEnumerator PerformRecoil(Vector3 originalPosition, Vector3 direction)
    {
        Vector3 currentPos = transform.position;
        float elapsedRecoilTime = 0f;
        float recoilDuration = attackAnimationDuration * 0.4f;
        Vector3 recoilPosition = currentPos - (direction * meleeAttackRecoil);
        
        while (elapsedRecoilTime < recoilDuration)
        {
            elapsedRecoilTime += Time.deltaTime;
            float t = elapsedRecoilTime / recoilDuration;
            transform.position = Vector3.Lerp(currentPos, recoilPosition, t);
            yield return null;
        }

        float elapsedReturnTime = 0f;
        float returnDuration = attackAnimationDuration * 0.3f;
        while (elapsedReturnTime < returnDuration)
        {
            elapsedReturnTime += Time.deltaTime;
            float t = elapsedReturnTime / returnDuration;
            transform.position = Vector3.Lerp(recoilPosition, originalPosition, t);
            yield return null;
        }

        transform.position = originalPosition;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Just send the attack timer
            stream.SendNext(nextAttackTime);
        }
        else
        {
            this.nextAttackTime = (float)stream.ReceiveNext();
        }
    }
}