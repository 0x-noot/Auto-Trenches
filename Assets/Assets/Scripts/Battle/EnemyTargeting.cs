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
                photonView.RPC("RPCFaceTarget", RpcTarget.All, targetPos);
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
        
        idealPos += new Vector3(
            Random.Range(-positionVariance, positionVariance),
            Random.Range(-positionVariance, positionVariance),
            0
        );

        return idealPos;
    }

    private void FindNewTarget()
    {
        if (!isTargeting || !photonView.IsMine) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetingRange, enemyLayer);
        
        Transform bestTarget = null;
        float bestScore = float.MinValue;
        int bestTargetViewID = -1;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            BaseUnit targetUnit = hit.GetComponent<BaseUnit>();
            PhotonView targetView = hit.GetComponent<PhotonView>();
            
            if (targetUnit == null || targetView == null || targetUnit.GetCurrentState() == UnitState.Dead) continue;

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
                bestTargetViewID = targetView.ViewID;
            }
        }

        if (bestTarget != null)
        {
            photonView.RPC("RPCSetTarget", RpcTarget.All, bestTargetViewID);
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
        while (isTargeting && unit != null)
        {
            if (photonView.IsMine)
            {
                UpdateTargeting();
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(isTargeting);
            // Send target's ViewID if it exists, -1 if not
            stream.SendNext(currentTarget != null ? currentTarget.GetComponent<PhotonView>()?.ViewID ?? -1 : -1);
        }
        else
        {
            // Network player, receive data
            this.isTargeting = (bool)stream.ReceiveNext();
            int targetViewID = (int)stream.ReceiveNext();
            
            // Update target based on ViewID
            if (targetViewID != -1)
            {
                PhotonView targetView = PhotonView.Find(targetViewID);
                if (targetView != null)
                {
                    currentTarget = targetView.transform;
                    currentTargetUnit = targetView.GetComponent<BaseUnit>();
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