using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class StandardNetworkTransform : MonoBehaviour
{
    private NetworkObject networkObject;
    private NetworkManager networkManager;
    private bool isClient = false;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        networkManager = FindFirstObjectByType<NetworkManager>();
    }

    void Start()
    {
        if (networkManager != null && networkManager.isHost)
        {
            enabled = false;
            return;
        }
        isClient = true;
    }

    void Update()
    {
        if (!isClient || networkObject == null)
            return;

        var (pos, rot, valid) = networkObject.InterpolateState();
        if (valid)
        {
            transform.position = pos;
            transform.rotation = rot;
        }
    }
}
