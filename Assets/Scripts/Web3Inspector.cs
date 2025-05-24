using UnityEngine;
using Solana.Unity.SDK;

public class WebInspector : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log($"[WebInspector] Awake - Web3.Instance: {Web3.Instance != null}");
    }
    
    private void Start()
    {
        Debug.Log($"[WebInspector] Start - Web3.Instance: {Web3.Instance != null}");
        if (Web3.Instance != null)
        {
            Debug.Log($"[WebInspector] Web3.Wallet: {Web3.Wallet != null}");
            if (Web3.Wallet != null)
            {
                Debug.Log($"[WebInspector] Web3.Wallet.Account: {Web3.Wallet.Account != null}");
                if (Web3.Wallet.Account != null)
                {
                    Debug.Log($"[WebInspector] Web3.Wallet.Account.PublicKey: {Web3.Wallet.Account.PublicKey}");
                }
            }
        }
        
        // Invoke periodic checking
        InvokeRepeating("CheckWeb3State", 5f, 30f);
    }
    
    private void CheckWeb3State()
    {
        Debug.Log($"[WebInspector] Periodic check - Web3.Instance: {Web3.Instance != null}");
        if (Web3.Instance != null)
        {
            Debug.Log($"[WebInspector] Web3.Wallet: {Web3.Wallet != null}");
            if (Web3.Wallet != null)
            {
                Debug.Log($"[WebInspector] Web3.Wallet.Account: {Web3.Wallet.Account != null}");
                if (Web3.Wallet.Account != null)
                {
                    Debug.Log($"[WebInspector] Web3.Wallet.Account.PublicKey: {Web3.Wallet.Account.PublicKey}");
                }
            }
        }
    }
    
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log($"[WebInspector] Scene loaded: {scene.name}");
        CheckWeb3State();
    }
    
    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}