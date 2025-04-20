using UnityEngine;
using Photon.Pun;

public class BarbarianAnimator : MonoBehaviourPunCallbacks
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    
    void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    public void SetMoving(bool isMoving)
    {
        animator.SetBool("isMoving", isMoving);
    }
    
    public void SetAttacking(bool isAttacking)
    {
        animator.SetBool("isAttacking", isAttacking);
    }
    
    public void SetDirection(int direction)
    {
        animator.SetInteger("direction", direction);
        // No need to flip sprite since they're already properly oriented
    }
    
    // Helper method to determine direction from a movement vector
    public void SetDirectionFromVector(Vector2 direction)
    {
        if (direction.magnitude < 0.1f) return;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Convert angle to direction index
        // Up = 0, Down = 1, Right = 2, Left = 3
        if (angle > 45 && angle < 135) // Up
            SetDirection(0);
        else if (angle < -45 && angle > -135) // Down
            SetDirection(1);
        else if ((angle >= -45 && angle <= 45)) // Right
            SetDirection(2);
        else // Left
            SetDirection(3);
    }
}