using UnityEngine;

public class AudioInit : MonoBehaviour
{
    [Header("Audio Prefab")]
    [SerializeField] private GameObject audioManagerPrefab;
    
    private void Awake()
    {
        // Check if AudioManager already exists
        if (AudioManager.Instance == null && audioManagerPrefab != null)
        {
            // Instantiate it
            Instantiate(audioManagerPrefab);
            Debug.Log("AudioManager created");
        }
        else
        {
            Debug.Log("AudioManager already exists");
        }
    }
}