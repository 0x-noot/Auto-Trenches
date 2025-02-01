using UnityEngine;
using System.Collections;

public class CombatSystem : MonoBehaviour
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
    [SerializeField] private float arrowHomingStrength = 0.8f; // Added homing strength parameter
    
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
        if (!CanAttack() || target == null || target.GetCurrentState() == UnitState.Dead)
            return;

        // Set next attack time based on attack speed
        nextAttackTime = Time.time + (1f / unit.GetAttackSpeed());
        
        // Start attack sequence based on unit type
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

    private IEnumerator PerformMeleeAttackSequence(BaseUnit target)
    {
        Vector3 originalPosition = transform.position;
        Vector3 targetPosition = target.transform.position;
        Vector3 attackDirection = (targetPosition - originalPosition).normalized;

        // Lunge forward
        yield return StartCoroutine(PerformLunge(originalPosition, attackDirection));

        // Spawn attack effect and apply damage
        SpawnMeleeEffect(transform.position, targetPosition);
        ApplyDamage(target);

        // Recoil and return
        yield return StartCoroutine(PerformRecoil(originalPosition, attackDirection));
    }

    private IEnumerator PerformRangedAttackSequence(BaseUnit target)
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("Arrow prefab is missing!");
            yield break;
        }

        if (target == null || target.GetCurrentState() == UnitState.Dead)
        {
            yield break;
        }

        // Create arrow at a slightly offset position to avoid clipping
        Vector3 spawnOffset = transform.up * 0.5f;
        GameObject arrowObj = Instantiate(arrowPrefab, transform.position + spawnOffset, Quaternion.identity);
        ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();

        if (arrow == null)
        {
            Debug.LogError("ArrowProjectile component missing from prefab!");
            Destroy(arrowObj);
            yield break;
        }

        Vector3 startPos = arrowObj.transform.position;
        float initialDistance = Vector3.Distance(startPos, target.transform.position);
        float duration = initialDistance / arrowSpeed;
        float elapsedTime = 0f;

        // Start arrow flight effects
        arrow.StartFlight();

        // Initial rotation to face target
        Vector3 direction = (target.transform.position - startPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        arrowObj.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Vector3 previousPosition = startPos;
        while (elapsedTime < duration && target != null && target.GetCurrentState() != UnitState.Dead)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Calculate the current target position
            Vector3 currentTargetPos = target.transform.position;
            
            // Calculate midpoint with arc
            Vector3 midPoint = Vector3.Lerp(startPos, currentTargetPos, 0.5f);
            midPoint.y += arrowArcHeight * Mathf.Sin(t * Mathf.PI);

            // Calculate the bezier curve position
            Vector3 idealPosition = Vector3.Lerp(
                Vector3.Lerp(startPos, midPoint, t),
                Vector3.Lerp(midPoint, currentTargetPos, t),
                t
            );

            // Current position is a blend between the ideal arc position and a direct line to the target
            Vector3 directPosition = Vector3.Lerp(arrow.transform.position, currentTargetPos, arrowHomingStrength * Time.deltaTime * arrowSpeed);
            arrow.transform.position = Vector3.Lerp(idealPosition, directPosition, t);

            // Update arrow rotation to face travel direction
            if (arrow.transform.position != previousPosition)
            {
                direction = (arrow.transform.position - previousPosition).normalized;
                angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrow.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            previousPosition = arrow.transform.position;
            arrow.UpdateArrowInFlight(t);

            // Check if we're close enough to hit
            if (Vector3.Distance(arrow.transform.position, currentTargetPos) < 0.5f)
            {
                break;
            }

            yield return null;
        }

        // Arrow hit
        if (arrow != null && target != null && target.GetCurrentState() != UnitState.Dead)
        {
            arrow.transform.position = target.transform.position;
            arrow.OnHit();
            ApplyDamage(target);
        }
        else if (arrow != null)
        {
            // If target died during flight, just destroy the arrow
            Destroy(arrow.gameObject);
        }
    }

    private IEnumerator PerformMageAttackSequence(BaseUnit target)
    {
        if (spellPrefab == null)
        {
            Debug.LogError("Spell prefab is missing!");
            yield break;
        }

        // Cast delay
        yield return new WaitForSeconds(spellCastDelay);

        // Create spell
        GameObject spellObj = Instantiate(spellPrefab, transform.position, Quaternion.identity);
        MagicProjectile spell = spellObj.GetComponent<MagicProjectile>();

        Vector3 startPos = transform.position;
        Vector3 targetPos = target.transform.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / spellSpeed;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (spell == null) break; // In case spell was destroyed

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            spell.transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            yield return null;
        }

        // Spell hit
        if (spell != null)
        {
            spell.OnSpellHit();
            ApplyDamage(target);
            Destroy(spellObj, 0.5f); // Longer delay to show spell hit effects
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

        // Return to original position
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

    private void SpawnMeleeEffect(Vector3 attackerPos, Vector3 targetPos)
    {
        if (meleeAttackEffectPrefab != null)
        {
            GameObject effectObj = Instantiate(meleeAttackEffectPrefab);
            MeleeAttackEffect effect = effectObj.GetComponent<MeleeAttackEffect>();
            if (effect != null)
            {
                effect.SetupEffect(attackerPos, targetPos);
            }
            else
            {
                Debug.LogError("MeleeAttackEffect component missing from prefab!");
            }
        }
    }

    private void ApplyDamage(BaseUnit target)
    {
        if (target != null && target.GetCurrentState() != UnitState.Dead)
        {
            float damage = unit.GetAttackDamage();
            target.TakeDamage(damage);
        }
    }
}