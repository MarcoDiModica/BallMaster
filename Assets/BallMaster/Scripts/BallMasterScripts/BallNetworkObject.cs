using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BallNetworkObject : MonoBehaviour
{
    private NetworkObject netObj;
    public float lifeTime = 5.0f;

    void Awake()
    {
        netObj = GetComponent<NetworkObject>();
    }

    void Start()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            Destroy(gameObject, lifeTime);
        }
        else
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(netObj.objectId))
        {
            NetworkObjectManager.Instance.UnregisterNetworkObject(netObj.objectId);
        }
    }

}
