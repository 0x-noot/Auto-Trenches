using UnityEngine;
using System.Collections;

public class EnemyTargeting : MonoBehaviour
{
    private MovementSystem movementSystem;
    private CombatSystem combatSystem;
    private BaseUnit unit;
    private bool isTargeting = false;
    private Transform currentTarget;
    private BaseUnit currentTargetUnit;
    private Vector3 lastTargetPosition;
    private float attackRange;
    
    [Header("Targeting Settings")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float targetingRange = 10f;
    [SerializeField] private float updateInterval = 0.1f;  // Reduced from 0.2f to 0.1f
    [SerializeField] private string targetTeamLayer = "TeamB";
    
    [Header("Combat Settings")]
    [SerializeField] private float positionVariance = 0.5f;

    private void Awake()
    {
        movementSystem = GetComponent<MovementSystem>();
        unit = GetComponent<BaseUnit>();
        combatSystem = GetComponent<CombatSystem>();
        
        if (combatSystem == null)
        {
            Debug.LogError($"[{gameObject.name}] Missing CombatSystem component!");
        }
    }

    private void Start()
    {
        if (movementSystem != null && unit != null)
        {
            attackRange = unit.GetAttackRange();
            enemyLayer = LayerMask.GetMask(targetTeamLayer);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                Debug.Log($"[{gameObject.name}] Subscribed to GameState changes");
            }
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        Debug.Log($"[{gameObject.name}] Received game state change: {newState}");
        switch (newState)
        {
            case GameState.BattleActive:
                Debug.Log($"[{gameObject.name}] Starting targeting system");
                StartTargeting();
                break;
            case GameState.BattleEnd:
            case GameState.GameOver:
                Debug.Log($"[{gameObject.name}] Stopping targeting system");
                StopTargeting();
                break;
        }
    }

    private void UpdateTargeting()
    {
        // Skip if unit is dead or we're not actively targeting
        if (!isTargeting || unit.GetCurrentState() == UnitState.Dead) return;

        // If we don't have a target, find one
        if (currentTarget == null)
        {
            FindNewTarget();
            return;
        }

        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentTarget.position;
        float distanceToTarget = Vector2.Distance(currentPos, targetPos);

        // Check if current target is still valid
        if (distanceToTarget <= targetingRange && currentTarget.gameObject.activeInHierarchy)
        {
            // In attack range
            if (distanceToTarget <= unit.GetAttackRange())
            {
                movementSystem.StopMovement();
                FaceTarget(targetPos);
                unit.UpdateState(UnitState.Attacking);
                
                // Get or update target unit reference
                if (currentTargetUnit == null || currentTargetUnit.gameObject != currentTarget.gameObject)
                {
                    currentTargetUnit = currentTarget.GetComponent<BaseUnit>();
                }
                
                // Attack if possible
                if (currentTargetUnit != null && currentTargetUnit.GetCurrentState() != UnitState.Dead)
                {
                    combatSystem.ExecuteAttack(currentTargetUnit);
                }
                else
                {
                    // Target is dead or invalid, find new target
                    currentTarget = null;
                    currentTargetUnit = null;
                    FindNewTarget();
                }
            }
            // Move towards target
            else
            {
                Vector3 idealPosition = CalculateIdealPosition(currentPos, targetPos);
                movementSystem.MoveTo(idealPosition);
            }
        }
        // Target out of range or inactive
        else
        {
            currentTarget = null;
            currentTargetUnit = null;
            unit.UpdateState(UnitState.Idle);
            FindNewTarget();
        }
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
        if (!isTargeting) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetingRange, enemyLayer);
        
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // Check if potential target is alive
            BaseUnit targetUnit = hit.GetComponent<BaseUnit>();
            if (targetUnit == null || targetUnit.GetCurrentState() == UnitState.Dead) continue;

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
            currentTargetUnit = bestTarget.GetComponent<BaseUnit>();
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
            Debug.Log($"[{gameObject.name}] Started targeting routine");
        }
    }

    public void StopTargeting()
    {
        isTargeting = false;
        currentTarget = null;
        currentTargetUnit = null;
        StopAllCoroutines();
        Debug.Log($"[{gameObject.name}] Stopped targeting routine");
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
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, targetingRange);

        if (unit != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, unit.GetAttackRange());
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}