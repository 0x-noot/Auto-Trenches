using UnityEngine;
using Photon.Pun;

// Add this to the same GameObject as ScoreDisplayUI
public class WebGLSafeUI : MonoBehaviourPunCallbacks
{
    private ScoreDisplayUI scoreUI;
    
    private void Awake()
    {
        // Immediately disable ScoreDisplayUI to prevent initialization errors
        scoreUI = GetComponent<ScoreDisplayUI>();
        if (scoreUI != null)
        {
            scoreUI.enabled = false;
        }
        
        // Only activate in WebGL
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            Destroy(this);
        }
    }
    
    private void Start()
    {
        // Use Invoke to delay ScoreDisplayUI activation in WebGL builds
        Invoke("SafeActivate", 3.0f);
    }
    
    private void SafeActivate()
    {
        if (scoreUI != null)
        {
            // Re-enable the ScoreDisplayUI after delay
            scoreUI.enabled = true;
            Debug.Log("Safely activated ScoreDisplayUI");
        }
    }
    
    // Make sure PhotonNetwork is properly initialized before re-enabling
    public override void OnJoinedRoom()
    {
        // If we're already in a room when scene loads, reset the timer
        CancelInvoke("SafeActivate");
        Invoke("SafeActivate", 2.0f);
    }
}