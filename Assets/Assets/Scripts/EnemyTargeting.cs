using UnityEngine;
using System.Collections;

public class EnemyTargeting : MonoBehaviour
{
    private MovementSystem movementSystem;
    private BaseUnit unit;
    private bool isTargeting = false;
    private Transform currentTarget;
    private Vector3 lastTargetPosition;
    private float attackRange; // Added this field
    
    [Header("Targeting Settings")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float targetingRange = 10f;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private string targetTeamLayer = "TeamB";
    
    [Header("Combat Settings")]
    [SerializeField] private float positionVariance = 0.5f;

    private void Awake()
    {
        movementSystem = GetComponent<MovementSystem>();
        unit = GetComponent<BaseUnit>();
    }
    private void Start()
    {
        Debug.Log($"[{gameObject.name}] Starting EnemyTargeting initialization");
        
        // Check what components are actually on this object
        var components = GetComponents<BaseUnit>();
        foreach (var comp in components)
        {
            Debug.Log($"[{gameObject.name}] Found unit component: {comp.GetType().Name}");
        }

        if (movementSystem != null && unit != null)
        {
            // Get the attack range after unit stats are initialized
            attackRange = unit.GetAttackRange();
            
            // Debug log to show unit type and stats
            Debug.Log($"[{gameObject.name}] Unit Details:\n" +
                     $"Component Type: {unit.GetType().Name}\n" +
                     $"Unit Type Enum: {unit.GetUnitType()}\n" +
                     $"Attack Range: {unit.GetAttackRange()}\n" +
                     $"Attack Damage: {unit.GetAttackDamage()}\n" +
                     $"Move Speed: {unit.GetMoveSpeed()}");
            
            enemyLayer = LayerMask.GetMask(targetTeamLayer);
            StartTargeting();
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Missing components!\n" +
                          $"MovementSystem: {movementSystem != null}\n" +
                          $"Unit: {unit != null}");
        }
    }

    private void UpdateTargeting()
    {
        if (unit.GetCurrentState() == UnitState.Dead) return;

        if (currentTarget != null)
        {
            Vector3 currentPos = transform.position;
            Vector3 targetPos = currentTarget.position;
            float distanceToTarget = Vector2.Distance(currentPos, targetPos);

            if (distanceToTarget <= targetingRange && 
                currentTarget.gameObject.activeInHierarchy)
            {
                if (distanceToTarget <= unit.GetAttackRange())
                {
                    movementSystem.StopMovement();
                    FaceTarget(targetPos);
                    unit.UpdateState(UnitState.Attacking);
                }
                else
                {
                    Vector3 idealPosition = CalculateIdealPosition(currentPos, targetPos);
                    movementSystem.MoveTo(idealPosition);
                }
                return;
            }
            else
            {
                currentTarget = null;
                unit.UpdateState(UnitState.Idle);
            }
        }

        FindNewTarget();
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector2 direction = (targetPos - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
    }

    private Vector3 CalculateIdealPosition(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 directionToTarget = (targetPos - currentPos).normalized;
        float randomAngle = Random.Range(-30f, 30f);
        Vector3 randomizedDirection = Quaternion.Euler(0, 0, randomAngle) * directionToTarget;
        
        float targetDistance = unit.GetAttackRange() * 0.9f;
        Vector3 idealPos = targetPos - (randomizedDirection * targetDistance);
        
        idealPos += new Vector3(
            Random.Range(-positionVariance, positionVariance),
            Random.Range(-positionVariance, positionVariance),
            0
        );

        return idealPos;
    }

    private void FindNewTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetingRange, enemyLayer);
        
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            float distance = Vector2.Distance(transform.position, hit.transform.position);
            float score = 100f - distance;
            
            Collider2D[] nearbyFriendlies = Physics2D.OverlapCircleAll(hit.transform.position, unit.GetAttackRange() * 1.5f);
            foreach (var friendly in nearbyFriendlies)
            {
                if (friendly.gameObject.layer == gameObject.layer)
                {
                    score -= 20f;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = hit.transform;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            Vector3 idealPosition = CalculateIdealPosition(transform.position, bestTarget.position);
            movementSystem.MoveTo(idealPosition);
        }
    }

    public void StartTargeting()
    {
        if (!isTargeting)
        {
            isTargeting = true;
            StartCoroutine(TargetingRoutine());
        }
    }

    public void StopTargeting()
    {
        isTargeting = false;
        currentTarget = null;
        StopAllCoroutines();
    }

    private IEnumerator TargetingRoutine()
    {
        while (isTargeting && unit != null)
        {
            UpdateTargeting();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Targeting range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, targetingRange);

        // Attack range
        if (unit != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, unit.GetAttackRange());
        }

        // Line to current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}