using UnityEngine;
using System.Collections;
using Photon.Pun;

public class EnemyTargeting : MonoBehaviourPunCallbacks, IPunObservable
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
    [SerializeField] private float targetingRange = 100f;
    [SerializeField] private float updateInterval = 0.1f;
    
    [Header("Combat Settings")]
    [SerializeField] private float positionVariance = 0.5f;

    // Network optimization
    private float lastTargetUpdateTime = 0f;
    private const float TARGET_UPDATE_INTERVAL = 0.5f; // Max 2 target changes per second
    private float lastPositionSyncTime = 0f;
    private const float POSITION_SYNC_INTERVAL = 0.1f; // 10 position updates per second max

    private void Awake()
    {
        movementSystem = GetComponent<MovementSystem>();
        unit = GetComponent<BaseUnit>();
        combatSystem = GetComponent<CombatSystem>();
        
        if (combatSystem == null)
        {
            Debug.LogError($"Missing CombatSystem component!");
        }
    }

    private void Start()
    {
        if (movementSystem != null && unit != null)
        {
            attackRange = unit.GetAttackRange();
            
            string targetTeamLayer = unit.GetTeamId() == "TeamA" ? "TeamB" : "TeamA";
            enemyLayer = LayerMask.GetMask(targetTeamLayer);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
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
        switch (newState)
        {
            case GameState.BattleActive:
                if (photonView.IsMine)
                {
                    StartTargeting();
                }
                break;
            case GameState.BattleEnd:
            case GameState.GameOver:
                if (photonView.IsMine)
                {
                    StopTargeting();
                }
                break;
        }
    }

    private void UpdateTargeting()
    {
        if (!isTargeting || !photonView.IsMine || unit.GetCurrentState() == UnitState.Dead) return;

        if (currentTarget == null)
        {
            FindNewTarget();
            return;
        }

        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentTarget.position;
        float distanceToTarget = Vector2.Distance(currentPos, targetPos);

        if (distanceToTarget <= targetingRange && currentTarget.gameObject.activeInHierarchy)
        {
            if (distanceToTarget <= unit.GetAttackRange())
            {
                movementSystem.StopMovement();
                
                // Only face if we need to
                if (Vector3.Distance(targetPos, lastTargetPosition) > 0.5f)
                {
                    photonView.RPC("RPCFaceTarget", RpcTarget.All, targetPos);
                    lastTargetPosition = targetPos;
                }
                
                unit.UpdateState(UnitState.Attacking);
                
                if (currentTargetUnit == null || currentTargetUnit.gameObject != currentTarget.gameObject)
                {
                    currentTargetUnit = currentTarget.GetComponent<BaseUnit>();
                }
                
                if (currentTargetUnit != null && currentTargetUnit.GetCurrentState() != UnitState.Dead)
                {
                    combatSystem.ExecuteAttack(currentTargetUnit);
                }
                else
                {
                    SetCurrentTarget(null);
                    FindNewTarget();
                }
            }
            else
            {
                Vector3 idealPosition = CalculateIdealPosition(currentPos, targetPos);
                movementSystem.MoveTo(idealPosition);
            }
        }
        else
        {
            SetCurrentTarget(null);
            unit.UpdateState(UnitState.Idle);
            FindNewTarget();
        }
    }

    [PunRPC]
    private void RPCFaceTarget(Vector3 targetPos)
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
        
        // Reduce variance to avoid too much random movement
        float variance = positionVariance * 0.5f;
        idealPos += new Vector3(
            Random.Range(-variance, variance),
            Random.Range(-variance, variance),
            0
        );

        return idealPos;
    }

    private void FindNewTarget()
    {
        if (!isTargeting || !photonView.IsMine) return;
        
        // Throttle target updates to reduce network traffic
        if (Time.time - lastTargetUpdateTime < TARGET_UPDATE_INTERVAL)
            return;
            
        lastTargetUpdateTime = Time.time;
        
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetingRange, enemyLayer);
        
        Transform bestTarget = null;
        float bestScore = float.MinValue;
        int bestTargetViewID = -1;

        int maxEnemiesToCheck = Mathf.Min(hits.Length, 5); // Limit the number of enemies to check
        for (int i = 0; i < maxEnemiesToCheck; i++)
        {
            var hit = hits[i];
            if (hit.gameObject == gameObject) continue;

            BaseUnit targetUnit = hit.GetComponent<BaseUnit>();
            PhotonView targetView = hit.GetComponent<PhotonView>();
            
            if (targetUnit == null || targetView == null || targetUnit.GetCurrentState() == UnitState.Dead) continue;

            float distance = Vector2.Distance(transform.position, hit.transform.position);
            float score = 100f - distance;
            
            // Limit friendly checks to reduce physics operations - max 3 checks
            int maxFriendlyChecks = 3;
            Collider2D[] nearbyFriendlies = Physics2D.OverlapCircleAll(hit.transform.position, unit.GetAttackRange(), 1 << gameObject.layer);
            for (int j = 0; j < Mathf.Min(nearbyFriendlies.Length, maxFriendlyChecks); j++)
            {
                score -= 10f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = hit.transform;
                bestTargetViewID = targetView.ViewID;
            }
        }

        if (bestTarget != null)
        {
            // Use buffered RPC to ensure consistency if connection issues occur
            photonView.RPC("RPCSetTarget", RpcTarget.AllBuffered, bestTargetViewID);
            Vector3 idealPosition = CalculateIdealPosition(transform.position, bestTarget.position);
            movementSystem.MoveTo(idealPosition);
        }
    }

    [PunRPC]
    private void RPCSetTarget(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            SetCurrentTarget(targetView.transform);
            currentTargetUnit = targetView.GetComponent<BaseUnit>();
            lastTargetPosition = targetView.transform.position;
        }
    }

    private void SetCurrentTarget(Transform target)
    {
        currentTarget = target;
        currentTargetUnit = target?.GetComponent<BaseUnit>();
    }

    public void StartTargeting()
    {
        if (!isTargeting && photonView.IsMine)
        {
            photonView.RPC("RPCStartTargeting", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPCStartTargeting()
    {
        if (!isTargeting)
        {
            isTargeting = true;
            StartCoroutine(TargetingRoutine());
        }
    }

    public void StopTargeting()
    {
        if (isTargeting && photonView.IsMine)
        {
            photonView.RPC("RPCStopTargeting", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPCStopTargeting()
    {
        isTargeting = false;
        SetCurrentTarget(null);
        StopAllCoroutines();
    }

    private IEnumerator TargetingRoutine()
    {
        float waitTime = updateInterval;
        while (isTargeting && unit != null)
        {
            if (photonView.IsMine)
            {
                UpdateTargeting();
                
                // Dynamic update interval based on distance to target and combat state
                if (currentTarget != null)
                {
                    float distance = Vector3.Distance(transform.position, currentTarget.position);
                    
                    // Update more frequently when closer to target or in combat
                    if (unit.GetCurrentState() == UnitState.Attacking)
                    {
                        waitTime = 0.05f; // Fast updates when attacking
                    }
                    else
                    {
                        // Distant targets need less frequent updates
                        waitTime = Mathf.Lerp(0.2f, 0.05f, Mathf.Clamp01(1f - (distance / targetingRange)));
                    }
                }
                else
                {
                    waitTime = updateInterval;
                }
            }
            yield return new WaitForSeconds(waitTime);
        }
    }

    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isTargeting);
            
            // Only send ViewID rather than full transform
            int targetViewID = -1;
            if (currentTarget != null)
            {
                PhotonView targetView = currentTarget.GetComponent<PhotonView>();
                if (targetView != null)
                    targetViewID = targetView.ViewID;
            }
            stream.SendNext(targetViewID);
        }
        else
        {
            isTargeting = (bool)stream.ReceiveNext();
            int targetViewID = (int)stream.ReceiveNext();
            
            // Only update target if it changed
            int currentTargetID = currentTarget?.GetComponent<PhotonView>()?.ViewID ?? -1;
            if (targetViewID != currentTargetID)
            {
                if (targetViewID != -1)
                {
                    PhotonView targetView = PhotonView.Find(targetViewID);
                    if (targetView != null)
                    {
                        currentTarget = targetView.transform;
                        currentTargetUnit = targetView.GetComponent<BaseUnit>();
                        lastTargetPosition = targetView.transform.position;
                    }
                }
                else
                {
                    currentTarget = null;
                    currentTargetUnit = null;
                }
            }
        }
    }
}