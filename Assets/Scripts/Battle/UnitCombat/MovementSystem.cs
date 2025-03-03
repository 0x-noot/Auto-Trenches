using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;

public class MovementSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    private BaseUnit unit;
    private Grid grid;
    private PathfindingSystem pathfinding;
    private bool isMoving = false;
    private bool isEnabled = false;
    private List<Vector3> currentPath = new List<Vector3>();
    private Vector3 currentTargetPosition;
    private float pathRecalculationTimer = 0f;
    
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float stoppingDistance = 0.1f;
    [SerializeField] private float pathRecalculationInterval = 0.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask unitLayer;
    
    // Network optimization
    private Vector3 syncedPosition;
    private Quaternion syncedRotation;
    private bool isSyncPositionDirty = false;
    private float lastSyncTime = 0f;
    private const float SYNC_INTERVAL = 0.1f; // 10 updates per second max
    private float syncLerpSpeed = 10f;

    private void Awake()
    {
        unit = GetComponent<BaseUnit>();
        grid = Object.FindFirstObjectByType<Grid>();
        pathfinding = new PathfindingSystem(grid, obstacleLayer, unitLayer);
        
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    public void SetMoveSpeed(float newSpeed)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCSetMoveSpeed", RpcTarget.All, newSpeed);
    }

    [PunRPC]
    private void RPCSetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
        Debug.Log($"Movement speed updated to: {moveSpeed}");
    }

    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.BattleActive:
                isEnabled = true;
                break;
            case GameState.BattleEnd:
            case GameState.GameOver:
                isEnabled = false;
                StopMovement();
                break;
            default:
                isEnabled = false;
                break;
        }
    }

    private void Update()
    {
        if (!isEnabled) return;
        
        if (photonView.IsMine)
        {
            // Owner controls movement
            if (isMoving)
            {
                pathRecalculationTimer += Time.deltaTime;
                if (pathRecalculationTimer >= pathRecalculationInterval)
                {
                    pathRecalculationTimer = 0f;
                    RecalculatePath();
                }
            }
            
            // Mark position for sync at intervals
            if (Time.time - lastSyncTime > SYNC_INTERVAL)
            {
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
                isSyncPositionDirty = true;
                lastSyncTime = Time.time;
            }
        }
        else
        {
            // Non-owner smoothly interpolates to synced position
            if (isMoving && Vector3.Distance(transform.position, syncedPosition) > 0.1f)
            {
                transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * syncLerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, syncedRotation, Time.deltaTime * syncLerpSpeed);
            }
        }
    }

    public bool MoveTo(Vector3 destination)
    {
        if (!isEnabled || !photonView.IsMine) return false;
        
        photonView.RPC("RPCMoveTo", RpcTarget.All, destination);
        return true;
    }

    [PunRPC]
    private void RPCMoveTo(Vector3 destination)
    {
        currentTargetPosition = destination;
        CalculateAndFollowPath();
    }

    public void UpdateTargetPosition(Vector3 newPosition)
    {
        if (!photonView.IsMine) return;
        
        if (Vector3.Distance(currentTargetPosition, newPosition) > stoppingDistance)
        {
            photonView.RPC("RPCUpdateTargetPosition", RpcTarget.All, newPosition);
        }
    }

    [PunRPC]
    private void RPCUpdateTargetPosition(Vector3 newPosition)
    {
        currentTargetPosition = newPosition;
    }

    private bool CalculateAndFollowPath()
    {
        if (unit.GetCurrentState() == UnitState.Dead)
            return false;

        Vector3Int targetCell = grid.WorldToCell(currentTargetPosition);
        List<Vector3Int> path = pathfinding.FindPath(
            grid.WorldToCell(transform.position), 
            targetCell
        );
        
        if (path == null || path.Count == 0)
            return false;

        currentPath = path.Select(p => grid.GetCellCenterWorld(p)).ToList();
        
        if (!isMoving)
        {
            StartCoroutine(FollowPathCoroutine());
        }
        
        return true;
    }

    private void RecalculatePath()
    {
        if (Vector3.Distance(transform.position, currentTargetPosition) <= stoppingDistance)
            return;

        CalculateAndFollowPath();
    }

    private IEnumerator FollowPathCoroutine()
    {
        isMoving = true;
        unit.UpdateState(UnitState.Moving);
        
        while (currentPath.Count > 0)
        {
            Vector3 currentWaypoint = currentPath[0];
            
            while (Vector3.Distance(transform.position, currentWaypoint) > stoppingDistance)
            {
                if (unit.GetCurrentState() == UnitState.Dead)
                {
                    StopMovement();
                    yield break;
                }

                Vector3 direction = (currentWaypoint - transform.position).normalized;
                
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
                
                float step = moveSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, step);
                
                // Mark position as dirty for sync, but throttle updates
                if (Time.time - lastSyncTime > SYNC_INTERVAL && photonView.IsMine)
                {
                    syncedPosition = transform.position;
                    syncedRotation = transform.rotation;
                    isSyncPositionDirty = true;
                    lastSyncTime = Time.time;
                }
                
                yield return null;
            }

            if (currentPath.Count > 0)
                currentPath.RemoveAt(0);
            
            if (Vector3.Distance(transform.position, currentTargetPosition) <= stoppingDistance)
            {
                break;
            }
        }
        
        isMoving = false;
        unit.UpdateState(UnitState.Idle);
    }

    public void StopMovement()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCStopMovement", RpcTarget.All);
    }

    [PunRPC]
    private void RPCStopMovement()
    {
        if (isMoving)
        {
            StopAllCoroutines();
            currentPath.Clear();
            isMoving = false;
            unit.UpdateState(UnitState.Idle);
            
            // Final position sync when stopping
            if (photonView.IsMine)
            {
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
                isSyncPositionDirty = true;
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send basic state always
            stream.SendNext(isMoving);
            stream.SendNext(isEnabled);
            
            // Only send position when dirty
            stream.SendNext(isSyncPositionDirty);
            if (isSyncPositionDirty)
            {
                stream.SendNext(syncedPosition);
                stream.SendNext(syncedRotation);
                stream.SendNext(currentTargetPosition);
                isSyncPositionDirty = false;
            }
        }
        else
        {
            isMoving = (bool)stream.ReceiveNext();
            isEnabled = (bool)stream.ReceiveNext();
            
            bool positionUpdated = (bool)stream.ReceiveNext();
            if (positionUpdated)
            {
                syncedPosition = (Vector3)stream.ReceiveNext();
                syncedRotation = (Quaternion)stream.ReceiveNext();
                currentTargetPosition = (Vector3)stream.ReceiveNext();
                
                // Non-owners will interpolate to this position in Update()
            }
        }
    }
}