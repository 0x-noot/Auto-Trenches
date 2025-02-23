using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class ObjectPool : MonoBehaviourPunCallbacks
{
    public static ObjectPool Instance;

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    [SerializeField] private List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Transform> poolParents;
    private bool isInitialized = false;

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
        InitializePools();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BattleScene")
        {
            StartCoroutine(InitializePoolsDelayed());
        }
    }

    private System.Collections.IEnumerator InitializePoolsDelayed()
    {
        yield return null;
        InitializePools();
    }

    private void InitializePools()
    {
        if (isInitialized) return;

        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolParents = new Dictionary<string, Transform>();

        foreach (Pool pool in pools)
        {
            // Skip arrow and spell projectile pools since we're using direct instantiation
            if (pool.tag == GameConstants.ARROW_PREFAB_TAG || 
                pool.tag == GameConstants.SPELL_PREFAB_TAG) 
            {
                continue;
            }

            if (string.IsNullOrEmpty(pool.tag) || pool.prefab == null)
            {
                Debug.LogError($"ObjectPool: Invalid pool configuration for {pool.tag}");
                continue;
            }

            GameObject parentObj = new GameObject($"{pool.tag}Pool");
            parentObj.transform.SetParent(transform);
            poolParents[pool.tag] = parentObj.transform;

            Queue<GameObject> objectPool = new Queue<GameObject>();
            poolDictionary.Add(pool.tag, objectPool);

            // Only master client creates pool objects
            if (PhotonNetwork.IsMasterClient)
            {
                for (int i = 0; i < pool.size; i++)
                {
                    CreatePoolObject(pool.tag, pool.prefab, objectPool);
                }
            }
        }

        isInitialized = true;
    }

    private void CreatePoolObject(string tag, GameObject prefab, Queue<GameObject> pool)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // LOCAL INSTANTIATION INSTEAD OF NETWORK
        GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        if (obj != null)
        {
            obj.name = $"{tag}_{pool.Count}";
            obj.transform.SetParent(poolParents[tag]);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        // If it's a projectile tag, return null or throw an error as we're using direct instantiation
        if (tag == GameConstants.ARROW_PREFAB_TAG || tag == GameConstants.SPELL_PREFAB_TAG)
        {
            Debug.LogWarning($"ObjectPool: Projectiles should use direct instantiation, not pooling. Tag: {tag}");
            return null;
        }

        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogError($"ObjectPool: Pool with tag {tag} doesn't exist!");
            return null;
        }

        Queue<GameObject> pool = poolDictionary[tag];

        // If pool is empty, create a new object
        if (pool.Count == 0)
        {
            Pool poolConfig = pools.Find(p => p.tag == tag);
            if (poolConfig != null && poolConfig.prefab != null)
            {
                CreatePoolObject(tag, poolConfig.prefab, pool);
            }
            
            // If still empty, try direct instantiation
            if (pool.Count == 0)
            {
                Pool poolConfig2 = pools.Find(p => p.tag == tag);
                if (poolConfig2 != null && poolConfig2.prefab != null)
                {
                    GameObject newObj = Instantiate(poolConfig2.prefab, position, rotation);
                    return newObj;
                }
                return null;
            }
        }

        GameObject obj = pool.Dequeue();
        if (obj == null)
        {
            Debug.LogError($"ObjectPool: Null object in pool {tag}!");
            return null;
        }

        // Reset transform and parent
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        // IMPORTANT: Detach from pool parent while in use
        obj.transform.SetParent(null);

        // Standard activation
        obj.SetActive(true);

        IPooledObject pooledObj = obj.GetComponent<IPooledObject>();
        if (pooledObj != null)
        {
            pooledObj.OnObjectSpawn();
        }

        return obj;
    }

    public void ReturnToPool(string tag, GameObject obj)
    {
        // If it's a projectile tag, just destroy the object
        if (tag == GameConstants.ARROW_PREFAB_TAG || tag == GameConstants.SPELL_PREFAB_TAG)
        {
            Destroy(obj);
            return;
        }

        if (!poolDictionary.ContainsKey(tag) || obj == null)
        {
            if (obj == null)
                Debug.LogError($"ObjectPool: Attempting to return null object to pool {tag}!");
            else
                Debug.LogError($"ObjectPool: Pool {tag} doesn't exist!");
            return;
        }

        // Deactivate object first
        obj.SetActive(false);
        
        // IMPORTANT: Re-parent to pool parent when returned
        if (poolParents.TryGetValue(tag, out Transform poolParent))
        {
            obj.transform.SetParent(poolParent);
        }
        
        poolDictionary[tag].Enqueue(obj);
    }

    public void ClearAllPools()
    {
        if (!isInitialized) return;

        if (poolDictionary != null)
        {
            foreach (var pool in poolDictionary)
            {
                while (pool.Value.Count > 0)
                {
                    GameObject obj = pool.Value.Dequeue();
                    if (obj != null)
                    {
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

        isInitialized = false;
    }

    public void ResetPool(string tag)
    {
        // If it's a projectile tag, just return as we're using direct instantiation
        if (tag == GameConstants.ARROW_PREFAB_TAG || tag == GameConstants.SPELL_PREFAB_TAG)
        {
            return;
        }

        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogError($"ObjectPool: Cannot reset nonexistent pool {tag}!");
            return;
        }

        Queue<GameObject> pool = poolDictionary[tag];
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        if (poolParents.ContainsKey(tag) && poolParents[tag] != null)
        {
            Destroy(poolParents[tag].gameObject);
            
            // Recreate parent
            GameObject parentObj = new GameObject($"{tag}Pool");
            parentObj.transform.SetParent(transform);
            poolParents[tag] = parentObj.transform;
        }
    }
    
    // Helper method to ensure pools are initialized
    public void EnsurePoolsInitialized()
    {
        if (!isInitialized)
        {
            InitializePools();
        }
    }
}