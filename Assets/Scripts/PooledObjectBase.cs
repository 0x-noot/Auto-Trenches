using UnityEngine;
using Photon.Pun;

public abstract class PooledObjectBase : MonoBehaviourPunCallbacks, IPooledObject
{
    protected bool isActive = false;
    protected Transform originalParent;

    protected virtual void Awake()
    {
        originalParent = transform.parent;
    }

    [PunRPC]
    protected virtual void RPCSyncTransform(float posX, float posY, float posZ, 
                                          float rotX, float rotY, float rotZ, float rotW)
    {
        // This RPC only used for position synchronization by non-owners
        Vector3 position = new Vector3(posX, posY, posZ);
        Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);

        transform.position = position;
        transform.rotation = rotation;
    }

    // This must be implemented by derived classes
    public abstract void OnObjectSpawn();

    protected virtual void OnDisable()
    {
        isActive = false;
    }

    public bool IsActive()
    {
        return isActive;
    }

    protected virtual void OnDestroy()
    {
        // Cleanup if needed
        isActive = false;
        originalParent = null;
    }
}