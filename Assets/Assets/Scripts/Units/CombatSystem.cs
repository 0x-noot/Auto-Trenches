using UnityEngine;
using System.Collections;
using Photon.Pun;

public class CombatSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    private BaseUnit unit;
    private float nextAttackTime = 0f;
    
    [Header("Combat Settings")]
    [SerializeField] private float attackAnimationDuration = 0.5f;
    
    [Header("Melee Attack Settings")]
    [SerializeField] private GameObject meleeAttackEffectPrefab;
    [SerializeField] private float meleeAttackRecoil = 0.3f;
    [SerializeField] private float meleeAttackLunge = 0.5f;
    
    [Header("Range Attack Settings")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowSpeed = 15f;
    [SerializeField] private float arrowArcHeight = 1f;
    [SerializeField] private float arrowHomingStrength = 0.8f;
    
    [Header("Mage Attack Settings")]
    [SerializeField] private GameObject spellPrefab;
    [SerializeField] private float spellSpeed = 10f;
    [SerializeField] private float spellCastDelay = 0.2f;
    
    private void Awake()
    {
        unit = GetComponent<BaseUnit>();
        if (!TryGetComponent<EnemyTargeting>(out var targeting))
        {
            Debug.LogError($"CombatSystem requires EnemyTargeting component!");
        }

        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (unit.GetUnitType() == UnitType.Range && arrowPrefab == null)
            Debug.LogError("Arrow prefab is missing for Range unit!");
        if (unit.GetUnitType() == UnitType.Mage && spellPrefab == null)
            Debug.LogError("Spell prefab is missing for Mage unit!");
        if ((unit.GetUnitType() == UnitType.Fighter || unit.GetUnitType() == UnitType.Tank) 
            && meleeAttackEffectPrefab == null)
            Debug.LogError("Melee attack effect prefab is missing for melee unit!");
    }

    public bool CanAttack()
    {
        return Time.time >= nextAttackTime && unit.GetCurrentState() != UnitState.Dead;
    }

    public void ExecuteAttack(BaseUnit target)
    {
        if (!CanAttack() || target == null || target.GetCurrentState() == UnitState.Dead || !photonView.IsMine)
            return;

        nextAttackTime = Time.time + (1f / unit.GetAttackSpeed());
        photonView.RPC("RPCExecuteAttack", RpcTarget.All, target.photonView.ViewID);
    }

    [PunRPC]
    private void RPCExecuteAttack(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null || target.GetCurrentState() == UnitState.Dead) return;
        
        switch (unit.GetUnitType())
        {
            case UnitType.Tank:
            case UnitType.Fighter:
                StartCoroutine(PerformMeleeAttackSequence(target));
                break;
                
            case UnitType.Range:
                StartCoroutine(PerformRangedAttackSequence(target));
                break;
                
            case UnitType.Mage:
                StartCoroutine(PerformMageAttackSequence(target));
                break;
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
            photonView.RPC("RPCSpawnMeleeEffect", RpcTarget.All, originalPosition, targetPosition);
            photonView.RPC("RPCApplyDamage", RpcTarget.All, target.photonView.ViewID, unit.GetAttackDamage());
        }

        yield return StartCoroutine(PerformRecoil(originalPosition, attackDirection));
    }

    private IEnumerator PerformRangedAttackSequence(BaseUnit target)
    {
        if (target == null || target.GetCurrentState() == UnitState.Dead)
        {
            yield break;
        }

        Vector3 spawnOffset = transform.up * 0.5f;
        if (photonView.IsMine)
        {
            photonView.RPC("RPCSpawnArrow", RpcTarget.All, 
                transform.position + spawnOffset, 
                target.photonView.ViewID,
                unit is Range ? (unit as Range).IsExplosiveArrow() : false);
        }

        yield return null;
    }

    [PunRPC]
    private void RPCSpawnArrow(Vector3 spawnPosition, int targetViewID, bool isExplosive)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null) return;

        GameObject arrowObj = ObjectPool.Instance.SpawnFromPool("Arrow", spawnPosition, Quaternion.identity);
        if (arrowObj == null) return;

        ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow == null)
        {
            ObjectPool.Instance.ReturnToPool("Arrow", arrowObj);
            return;
        }

        StartCoroutine(AnimateArrow(arrow, target, isExplosive));
    }

    private IEnumerator AnimateArrow(ArrowProjectile arrow, BaseUnit target, bool isExplosive)
    {
        Vector3 startPos = arrow.transform.position;
        float initialDistance = Vector3.Distance(startPos, target.transform.position);
        float duration = initialDistance / arrowSpeed;
        float elapsedTime = 0f;

        arrow.StartFlight();

        Vector3 previousPosition = startPos;
        while (elapsedTime < duration && target != null && target.GetCurrentState() != UnitState.Dead)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            Vector3 currentTargetPos = target.transform.position;
            Vector3 midPoint = Vector3.Lerp(startPos, currentTargetPos, 0.5f);
            midPoint.y += arrowArcHeight * Mathf.Sin(t * Mathf.PI);

            Vector3 idealPosition = Vector3.Lerp(
                Vector3.Lerp(startPos, midPoint, t),
                Vector3.Lerp(midPoint, currentTargetPos, t),
                t
            );

            Vector3 directPosition = Vector3.Lerp(arrow.transform.position, currentTargetPos, arrowHomingStrength * Time.deltaTime * arrowSpeed);
            arrow.transform.position = Vector3.Lerp(idealPosition, directPosition, t);

            if (arrow.transform.position != previousPosition)
            {
                Vector3 direction = (arrow.transform.position - previousPosition).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrow.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            previousPosition = arrow.transform.position;
            arrow.UpdateArrowInFlight(t);

            if (Vector3.Distance(arrow.transform.position, currentTargetPos) < 0.5f)
            {
                break;
            }

            yield return null;
        }

        if (arrow != null && target != null && target.GetCurrentState() != UnitState.Dead)
        {
            arrow.transform.position = target.transform.position;
            arrow.OnHit();
            if (photonView.IsMine)
            {
                photonView.RPC("RPCApplyDamage", RpcTarget.All, target.photonView.ViewID, unit.GetAttackDamage());
                if (isExplosive && unit is Range rangeUnit)
                {
                    rangeUnit.CreateExplosion(target.transform.position, target);
                }
            }
        }
        else if (arrow != null)
        {
            ObjectPool.Instance.ReturnToPool("Arrow", arrow.gameObject);
        }
    }

    private IEnumerator PerformMageAttackSequence(BaseUnit target)
    {
        yield return new WaitForSeconds(spellCastDelay);

        if (photonView.IsMine)
        {
            photonView.RPC("RPCSpawnSpell", RpcTarget.All, transform.position, target.photonView.ViewID);
        }
    }

    [PunRPC]
    private void RPCSpawnSpell(Vector3 spawnPosition, int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null) return;

        GameObject spellObj = ObjectPool.Instance.SpawnFromPool("MagicProjectile", spawnPosition, Quaternion.identity);
        if (spellObj == null) return;

        MagicProjectile spell = spellObj.GetComponent<MagicProjectile>();
        if (spell == null)
        {
            ObjectPool.Instance.ReturnToPool("MagicProjectile", spellObj);
            return;
        }

        StartCoroutine(AnimateSpell(spell, target));
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
            if (spell == null) break;

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
                photonView.RPC("RPCApplyDamage", RpcTarget.All, target.photonView.ViewID, unit.GetAttackDamage());
            }
        }
    }

    [PunRPC]
    private void RPCSpawnMeleeEffect(Vector3 attackerPos, Vector3 targetPos)
    {
        GameObject effectObj = ObjectPool.Instance.SpawnFromPool("MeleeEffect", attackerPos, Quaternion.identity);
        if (effectObj != null)
        {
            MeleeAttackEffect effect = effectObj.GetComponent<MeleeAttackEffect>();
            if (effect != null)
            {
                effect.SetupEffect(attackerPos, targetPos);
            }
            else
            {
                ObjectPool.Instance.ReturnToPool("MeleeEffect", effectObj);
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
            stream.SendNext(nextAttackTime);
        }
        else
        {
            this.nextAttackTime = (float)stream.ReceiveNext();
        }
    }
}