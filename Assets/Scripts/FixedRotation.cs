using UnityEngine;
using Photon.Pun;

public class FixedRotation : MonoBehaviourPunCallbacks
{
    [SerializeField] private Vector3 offset = new Vector3(0, 0.75f, 0);
    
    private void LateUpdate()
    {
        if (transform.parent != null)
        {
            // Keep position above parent with the specified offset
            transform.position = transform.parent.position + offset;
            
            // Reset rotation to zero
            transform.rotation = Quaternion.identity;
        }
    }
}