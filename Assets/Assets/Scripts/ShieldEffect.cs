using UnityEngine;

public class ShieldEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem mainShieldParticles;
    [SerializeField] private ParticleSystem orbitalParticles;

    public void ActivateShield()
    {
        if (mainShieldParticles != null)
            mainShieldParticles.Play();
            
        if (orbitalParticles != null)
            orbitalParticles.Play();
    }

    public void DeactivateShield()
    {
        if (mainShieldParticles != null)
            mainShieldParticles.Stop();
            
        if (orbitalParticles != null)
            orbitalParticles.Stop();
    }

    private void OnDestroy()
    {
        DeactivateShield();
    }
}