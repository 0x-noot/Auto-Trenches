using UnityEngine;
using Photon.Pun;

public class BarbarianAnimator : MonoBehaviourPunCallbacks, IPunObservable
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private int currentDirection = 1; // Default to down
    private bool isMoving = false;
    private bool isAttacking = false;
    
    void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    public void SetMoving(bool moving)
    {
        isMoving = moving;
        if (animator != null)
        {
            animator.SetBool("isMoving", moving);
        }
    }
    
    public void SetAttacking(bool attacking)
    {
        isAttacking = attacking;
        if (animator != null)
        {
            animator.SetBool("isAttacking", attacking);
        }
    }
    
    public void SetDirection(int direction)
    {
        currentDirection = direction;
        if (animator != null)
        {
            animator.SetInteger("direction", direction);
        }
    }
    
    public void SetDirectionFromVector(Vector2 direction)
    {
        if (direction.magnitude < 0.1f) return;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        int newDirection;
        if (angle > 45 && angle < 135) // Up
            newDirection = 0;
        else if (angle < -45 && angle > -135) // Down
            newDirection = 1;
        else if ((angle >= -45 && angle <= 45)) // Right
            newDirection = 2;
        else // Left
            newDirection = 3;
            
        SetDirection(newDirection);
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send animation state
            stream.SendNext(currentDirection);
            stream.SendNext(isMoving);
            stream.SendNext(isAttacking);
        }
        else
        {
            // Receive animation state
            int receivedDirection = (int)stream.ReceiveNext();
            bool receivedMoving = (bool)stream.ReceiveNext();
            bool receivedAttacking = (bool)stream.ReceiveNext();
            
            // Only apply if we're not the owner
            if (!photonView.IsMine)
            {
                SetDirection(receivedDirection);
                SetMoving(receivedMoving);
                SetAttacking(receivedAttacking);
            }
        }
    }
}