using UnityEngine;
using Photon.Pun;

public class ShieldEffect : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private ParticleSystem mainShieldParticles;
    [SerializeField] private ParticleSystem orbitalParticles;
    private bool isShieldActive = false;

    private void Awake()
    {
        // Optional validation
        if (mainShieldParticles == null)
            Debug.LogWarning("Main shield particles reference is missing!");
        if (orbitalParticles == null)
            Debug.LogWarning("Orbital particles reference is missing!");
    }

    public void ActivateShield()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCActivateShield", RpcTarget.All);
    }

    [PunRPC]
    private void RPCActivateShield()
    {
        isShieldActive = true;
        
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
        isShieldActive = false;
        
        if (mainShieldParticles != null)
        {
            mainShieldParticles.Stop();
        }
            
        if (orbitalParticles != null)
        {
            orbitalParticles.Stop();
        }
    }

    private void OnDestroy()
    {
        if (photonView.IsMine)
        {
            DeactivateShield();
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isShieldActive);
        }
        else
        {
            bool newShieldState = (bool)stream.ReceiveNext();
            if (newShieldState != isShieldActive)
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