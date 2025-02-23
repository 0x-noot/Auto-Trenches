using UnityEngine;
using Photon.Pun;

public class ShieldEffect : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private ParticleSystem mainShieldParticles;
    [SerializeField] private ParticleSystem orbitalParticles;

    [Header("Shield Settings")]
    [SerializeField] private float shieldRadius = 1f;
    [SerializeField] private Color shieldColor = new Color(0, 0.8f, 1f, 0.5f);
    [SerializeField] private float orbitalSpeed = 2f;

    private bool isActive = false;

    private void Awake()
    {
        // Validate and get references
        if (mainShieldParticles == null)
        {
            mainShieldParticles = GetComponent<ParticleSystem>();
            if (mainShieldParticles == null)
                Debug.LogWarning("Main shield particles reference is missing!");
        }
        
        if (orbitalParticles == null)
        {
            var orbitalObj = transform.Find("OrbitalParticles");
            if (orbitalObj != null)
                orbitalParticles = orbitalObj.GetComponent<ParticleSystem>();
            else
                CreateOrbitalParticles();
        }

        SetupParticleSystems();
    }

    private void OnEnable()
    {
        // Reset particles
        if (mainShieldParticles != null)
        {
            mainShieldParticles.Stop();
            mainShieldParticles.Clear();
        }
            
        if (orbitalParticles != null)
        {
            orbitalParticles.Stop();
            orbitalParticles.Clear();
        }

        isActive = false;
    }

    private void CreateOrbitalParticles()
    {
        var orbitalObj = new GameObject("OrbitalParticles");
        orbitalObj.transform.SetParent(transform);
        orbitalObj.transform.localPosition = Vector3.zero;
        
        orbitalParticles = orbitalObj.AddComponent<ParticleSystem>();
        
        // Setup orbital particles
        var main = orbitalParticles.main;
        main.startColor = shieldColor;
        main.startLifetime = 1f;
        main.startSpeed = orbitalSpeed;
        main.startSize = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        
        var emission = orbitalParticles.emission;
        emission.rateOverTime = 20;
        
        var shape = orbitalParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = shieldRadius;
    }

    private void SetupParticleSystems()
    {
        if (mainShieldParticles != null)
        {
            var main = mainShieldParticles.main;
            main.startColor = shieldColor;
            main.startSize = shieldRadius * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        if (orbitalParticles != null)
        {
            var main = orbitalParticles.main;
            main.startColor = shieldColor;
            main.startSpeed = orbitalSpeed;
            
            var shape = orbitalParticles.shape;
            shape.radius = shieldRadius;
        }
    }

    public void ActivateShield()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCActivateShield", RpcTarget.All);
    }

    [PunRPC]
    private void RPCActivateShield()
    {
        isActive = true;
        
        if (mainShieldParticles != null)
        {
            mainShieldParticles.Play();
        }
            
        if (orbitalParticles != null)
        {
            orbitalParticles.Play();
        }
    }

    public void DeactivateShield()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCDeactivateShield", RpcTarget.All);
    }

    [PunRPC]
    private void RPCDeactivateShield()
    {
        isActive = false;
        
        if (mainShieldParticles != null)
        {
            mainShieldParticles.Stop();
        }
            
        if (orbitalParticles != null)
        {
            orbitalParticles.Stop();
        }
    }

    private void OnDisable()
    {
        if (mainShieldParticles != null)
            mainShieldParticles.Stop();
        if (orbitalParticles != null)
            orbitalParticles.Stop();
            
        isActive = false;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isActive);
        }
        else
        {
            bool newShieldState = (bool)stream.ReceiveNext();
            if (newShieldState != isActive)
            {
                if (newShieldState)
                {
                    RPCActivateShield();
                }
                else
                {
                    RPCDeactivateShield();
                }
            }
        }
    }
}