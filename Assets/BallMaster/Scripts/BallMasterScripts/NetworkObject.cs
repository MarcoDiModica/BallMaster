using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    public string objectId;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    public float interpolationSpeed = 15f;

    void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        targetPosition = pos;
        targetRotation = rot;
    }

    void Update()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            return;
        }

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
