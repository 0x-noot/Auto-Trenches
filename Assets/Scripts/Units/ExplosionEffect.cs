using UnityEngine;
using Photon.Pun;

public class ExplosionEffect : MonoBehaviourPunCallbacks
{
    [SerializeField] private ParticleSystem particleSystem;
    [SerializeField] private float duration = 1f;
    
    private void Awake()
    {
        Debug.Log($"ExplosionEffect Awake: {gameObject.name}");
        if (particleSystem == null)
            particleSystem = GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        Debug.Log($"ExplosionEffect Start: {gameObject.name}");
        if (particleSystem != null)
        {
            particleSystem.Play();
            Debug.Log("Playing particle system");
        }
        else
        {
            Debug.LogError("ParticleSystem is null!");
        }

        Destroy(gameObject, duration);
    }
}