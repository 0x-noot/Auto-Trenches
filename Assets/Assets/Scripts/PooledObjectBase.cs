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
    protected virtual void RPCActivatePoolObject(float posX, float posY, float posZ, 
                                               float rotX, float rotY, float rotZ, float rotW)
    {
        if (!gameObject.activeInHierarchy) return;

        Vector3 position = new Vector3(posX, posY, posZ);
        Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);

        transform.position = position;
        transform.rotation = rotation;
        gameObject.SetActive(true);
        isActive = true;

        OnObjectSpawn();
    }

    [PunRPC]
    protected virtual void RPCDeactivatePoolObject()
    {
        if (!gameObject.activeInHierarchy) return;

        isActive = false;
        gameObject.SetActive(false);

        // Return to original parent if it exists
        if (originalParent != null)
        {
            transform.SetParent(originalParent);
        }
    }

    [PunRPC]
    protected virtual void RPCReturnToPool(string tag)
    {
        if (!gameObject.activeInHierarchy) return;

        isActive = false;
        gameObject.SetActive(false);

        // Return to original parent if it exists
        if (originalParent != null)
        {
            transform.SetParent(originalParent);
        }

        // Notify object pool if needed
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool(tag, gameObject);
        }
    }

    public abstract void OnObjectSpawn();

    protected virtual void OnDisable()
    {
        isActive = false;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (photonView != null && !photonView.ObservedComponents.Contains(this))
        {
            photonView.ObservedComponents.Add(this);
        }
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