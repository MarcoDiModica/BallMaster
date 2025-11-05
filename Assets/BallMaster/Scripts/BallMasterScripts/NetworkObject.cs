using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    public string objectId;
    public bool useInterpolation = true;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    public float interpolationSpeed = 15f;
    private PlayerController playerController;
    private bool checkedForPlayer = false;
    private Ball ball;

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
        if (!checkedForPlayer)
        {
            playerController = GetComponent<PlayerController>();
            ball = GetComponent<Ball>();
            
            if (ball != null)
                useInterpolation = false;
            
            checkedForPlayer = true;
        }

        if (!useInterpolation)
            return;

        if (playerController != null && playerController.IsLocalPlayer())
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