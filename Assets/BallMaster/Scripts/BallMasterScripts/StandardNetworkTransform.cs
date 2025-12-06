using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class StandardNetworkTransform : MonoBehaviour
{
    [Header("Interpolation")]
    public float interpolationSpeed = 15f;
    
    private NetworkObject networkObject;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool initialStateReceived = false;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void OnEnable()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated += OnStateUpdated;
        }
    }

    void OnDisable()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated -= OnStateUpdated;
        }
    }

    void OnStateUpdated(Vector3 pos, Quaternion rot)
    {
        targetPosition = pos;
        targetRotation = rot;
        
        if (!initialStateReceived)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            initialStateReceived = true;
        }
    }

    void Start()
    {
        NetworkManager nm = FindFirstObjectByType<NetworkManager>();
        if (nm != null && nm.isHost)
        {
            enabled = false;
        }
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
        }
        else
        {
            transform.position = targetPosition;
        }

        if (Quaternion.Angle(transform.rotation, targetRotation) > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }
}
