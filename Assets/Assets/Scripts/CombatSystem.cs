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
    [SerializeField] private float meleeAttackRecoil = 0.3f; // How far unit moves back after attack
    [SerializeField] private float meleeAttackLunge = 0.5f;  // How far unit moves forward during attack
    
    private void Awake()
    {
        unit = GetComponent<BaseUnit>();
        if (!TryGetComponent<EnemyTargeting>(out var targeting))
        {
            Debug.LogError($"[{gameObject.name}] CombatSystem requires EnemyTargeting component!");
        }
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
        if (unit.GetUnitType() == UnitType.Tank || unit.GetUnitType() == UnitType.Fighter)
        {
            StartCoroutine(PerformMeleeAttackSequence(target));
        }
    }

    private IEnumerator PerformMeleeAttackSequence(BaseUnit target)
    {
        Vector3 originalPosition = transform.position;
        Vector3 targetPosition = target.transform.position;
        Vector3 attackDirection = (targetPosition - originalPosition).normalized;

        Debug.Log($"[{gameObject.name}] Starting melee attack sequence. Original pos: {originalPosition}, Target pos: {targetPosition}");

        // Quick lunge forward
        float elapsedTime = 0f;
        float lungeDuration = attackAnimationDuration * 0.3f;
        Vector3 lungePosition = originalPosition + (attackDirection * meleeAttackLunge);
        
        while (elapsedTime < lungeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / lungeDuration;
            transform.position = Vector3.Lerp(originalPosition, lungePosition, t);
            yield return null;
        }

        // Spawn attack effect
        if (meleeAttackEffectPrefab != null)
        {
            Debug.Log($"[{gameObject.name}] Spawning attack effect");
            GameObject effectObj = Instantiate(meleeAttackEffectPrefab);
            MeleeAttackEffect effect = effectObj.GetComponent<MeleeAttackEffect>();
            if (effect != null)
            {
                Debug.Log($"[{gameObject.name}] Setting up effect between {transform.position} and {targetPosition}");
                effect.SetupEffect(transform.position, targetPosition);
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] MeleeAttackEffect component missing from prefab!");
            }
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] No meleeAttackEffectPrefab assigned!");
        }

        // Apply damage
        if (target != null && target.GetCurrentState() != UnitState.Dead)
        {
            float damage = CalculateDamage(target);
            target.TakeDamage(damage);
            Debug.Log($"{gameObject.name} dealt {damage} damage to {target.gameObject.name}");
        }

        // Recoil movement
        elapsedTime = 0f;
        float recoilDuration = attackAnimationDuration * 0.4f;
        Vector3 recoilPosition = transform.position - (attackDirection * meleeAttackRecoil);
        
        while (elapsedTime < recoilDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / recoilDuration;
            transform.position = Vector3.Lerp(lungePosition, recoilPosition, t);
            yield return null;
        }

        // Return to original position
        elapsedTime = 0f;
        float returnDuration = attackAnimationDuration * 0.3f;
        
        while (elapsedTime < returnDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / returnDuration;
            transform.position = Vector3.Lerp(recoilPosition, originalPosition, t);
            yield return null;
        }

        transform.position = originalPosition;
        Debug.Log($"[{gameObject.name}] Completed melee attack sequence");
    }

    private float CalculateDamage(BaseUnit target)
    {
        // Basic damage calculation
        float baseDamage = unit.GetAttackDamage();
        
        // We can add more complex calculations here later:
        // - Critical hits
        // - Damage types
        // - Armor/resistance
        // - Special abilities
        
        return baseDamage;
    }
}