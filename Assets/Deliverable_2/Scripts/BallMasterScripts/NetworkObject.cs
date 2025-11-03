using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    public string objectId;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }
}
