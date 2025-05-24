using UnityEngine;
using Solana.Unity.SDK;

public class Web3Persistence : MonoBehaviour
{
    private static Web3Persistence instance;
    private Web3 web3Instance;

    private void Awake()
    {
        // Check if instance already exists
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Get reference to Web3
            web3Instance = GetComponent<Web3>();
            
            if (web3Instance == null)
            {
                Debug.LogError("Web3Persistence: No Web3 component found on this object!");
            }
            else
            {
                Debug.Log("Web3Persistence: Web3 instance preserved");
            }
        }
        else if (instance != this)
        {
            // If another instance exists, find its Web3
            Web3 otherWeb3 = GetComponent<Web3>();
            
            // If this object has Web3 but we already have an instance, destroy this one
            if (otherWeb3 != null)
            {
                Debug.Log("Web3Persistence: Duplicate Web3 instance found, destroying");
                Destroy(gameObject);
            }
        }
    }
}