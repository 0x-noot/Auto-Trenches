using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class ObjectPool : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class Pool
    {
        public string tag;           // Must match prefab name in Resources folder
        public int size;            // Initial pool size
        public Transform parent;    // Optional parent transform
    }

    #region Singleton
    public static ObjectPool Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    [SerializeField] private List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Transform> poolParents;
    private Dictionary<int, string> viewIdToPoolTag;
    private bool isInitialized = false;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearAllPools();
    }

    private void OnDestroy()
    {
        ClearAllPools();
    }

    private void Start()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        InitializePools();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"ObjectPool: Scene {scene.name} loaded");
        if (scene.name == "BattleScene") // Replace with your battle scene name
        {
            InitializePools();
        }
    }

    private void InitializePools()
    {
        if (isInitialized) return;

        Debug.Log("ObjectPool: Initializing pools");
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolParents = new Dictionary<string, Transform>();
        viewIdToPoolTag = new Dictionary<int, string>();

        foreach (Pool pool in pools)
        {
            // Create parent object for this pool
            GameObject parentObj = new GameObject($"{pool.tag}Pool");
            parentObj.transform.SetParent(transform);
            poolParents[pool.tag] = parentObj.transform;

            Queue<GameObject> objectPool = new Queue<GameObject>();

            if (PhotonNetwork.IsMasterClient)
            {
                for (int i = 0; i < pool.size; i++)
                {
                    CreatePoolObject(pool.tag, objectPool);
                }
            }

            poolDictionary.Add(pool.tag, objectPool);
        }

        isInitialized = true;
        Debug.Log("ObjectPool: Initialization complete");
    }

    private void CreatePoolObject(string tag, Queue<GameObject> pool)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Load prefab from Resources folder
        GameObject prefab = Resources.Load<GameObject>(tag);
        if (prefab == null)
        {
            Debug.LogError($"Failed to load prefab from Resources/{tag}");
            return;
        }

        // Instantiate through PhotonNetwork
        GameObject obj = PhotonNetwork.Instantiate(tag, Vector3.zero, Quaternion.identity);
        if (obj != null)
        {
            obj.transform.SetParent(poolParents[tag]);
            pool.Enqueue(obj);
            
            PhotonView photonView = obj.GetComponent<PhotonView>();
            if (photonView != null)
            {
                viewIdToPoolTag[photonView.ViewID] = tag;
                photonView.RPC("RPCDeactivatePoolObject", RpcTarget.All);
            }
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogError($"Pool with tag {tag} doesn't exist!");
            return null;
        }

        Queue<GameObject> pool = poolDictionary[tag];

        // Expand pool if needed
        if (pool.Count == 0)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                CreatePoolObject(tag, pool);
            }
            else
            {
                Debug.LogWarning($"Pool {tag} is empty and client cannot create new objects!");
                return null;
            }
        }

        GameObject obj = pool.Dequeue();
        if (obj == null)
        {
            Debug.LogError($"Dequeued null object from pool {tag}");
            return null;
        }

        // Use RPC to activate object across network
        PhotonView photonView = obj.GetComponent<PhotonView>();
        if (photonView != null)
        {
            photonView.RPC("RPCActivatePoolObject", RpcTarget.All, 
                position.x, position.y, position.z,
                rotation.x, rotation.y, rotation.z, rotation.w);
        }

        return obj;
    }

    [PunRPC]
    private void RPCDeactivatePoolObject()
    {
        if (!gameObject.activeInHierarchy) return;
        
        gameObject.SetActive(false);

        if (poolParents.ContainsKey(tag))
        {
            transform.SetParent(poolParents[tag]);
        }

        IPooledObject[] pooledObjects = GetComponents<IPooledObject>();
        foreach (var pooledObj in pooledObjects)
        {
            pooledObj.OnObjectSpawn();
        }
    }

    [PunRPC]
    private void RPCActivatePoolObject(float posX, float posY, float posZ, 
                                     float rotX, float rotY, float rotZ, float rotW)
    {
        if (!gameObject.activeInHierarchy) return;

        Vector3 position = new Vector3(posX, posY, posZ);
        Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);

        transform.position = position;
        transform.rotation = rotation;
        gameObject.SetActive(true);

        // Initialize pooled objects
        IPooledObject[] pooledObjects = GetComponents<IPooledObject>();
        foreach (var pooledObj in pooledObjects)
        {
            pooledObj.OnObjectSpawn();
        }
    }

    public void ReturnToPool(string tag, GameObject obj)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogError($"Pool with tag {tag} doesn't exist!");
            return;
        }

        PhotonView photonView = obj.GetComponent<PhotonView>();
        if (photonView != null)
        {
            photonView.RPC("RPCReturnToPool", RpcTarget.All, tag);
        }
    }

    [PunRPC]
    private void RPCReturnToPool(string tag)
    {
        if (!gameObject.activeInHierarchy) return;

        gameObject.SetActive(false);
        
        if (poolParents.ContainsKey(tag))
        {
            transform.SetParent(poolParents[tag]);
        }

        if (poolDictionary.ContainsKey(tag))
        {
            poolDictionary[tag].Enqueue(gameObject);
        }
    }

    public void ClearAllPools()
    {
        if (!isInitialized) return;

        Debug.Log("ObjectPool: Clearing all pools");

        if (poolDictionary != null)
        {
            foreach (var pool in poolDictionary)
            {
                Queue<GameObject> objectPool = pool.Value;
                while (objectPool.Count > 0)
                {
                    GameObject obj = objectPool.Dequeue();
                    if (obj != null)
                    {
                        if (PhotonNetwork.IsMasterClient)
                            PhotonNetwork.Destroy(obj);
                        else
                            Destroy(obj);
                    }
                }
            }
            poolDictionary.Clear();
        }

        if (poolParents != null)
        {
            foreach (var parent in poolParents.Values)
            {
                if (parent != null)
                {
                    Destroy(parent.gameObject);
                }
            }
            poolParents.Clear();
        }

        viewIdToPoolTag.Clear();
        isInitialized = false;
    }

    public void ResetPool(string tag)
    {
        if (!poolDictionary.ContainsKey(tag)) return;

        Queue<GameObject> pool = poolDictionary[tag];
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                if (PhotonNetwork.IsMasterClient)
                    PhotonNetwork.Destroy(obj);
                else
                    Destroy(obj);
            }
        }

        if (poolParents.ContainsKey(tag) && poolParents[tag] != null)
        {
            Destroy(poolParents[tag].gameObject);
            poolParents.Remove(tag);
        }
    }
}