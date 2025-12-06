using UnityEngine;
using System;

public class PlayerNetworkComponent : MonoBehaviour
{
    [Header("Network Settings")]
    public float interpolationSpeed = 15f;
    private bool isLocalPlayer = false;

    public event Action OnTransformModified;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    
    private NetworkObject networkObject;
    private PlayerController playerController;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        playerController = GetComponent<PlayerController>();
        
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void OnEnable()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated += UpdateNetworkState;
        }
    }

    void OnDisable()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated -= UpdateNetworkState;
        }
    }

    public void Initialize(bool local)
    {
        isLocalPlayer = local;
        
        if (!isLocalPlayer)
        {
            if (playerController != null) 
            {
                playerController.enabled = false;
            }
            
            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null)
            {
                input.enabled = false;
            }
        }
        else
        {
            if (playerController != null)
            {
                playerController.enabled = true;
                
                if (playerController.cameraTransform != null)
                {
                    playerController.cameraTransform.gameObject.SetActive(true);
                    
                    foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    {
                        if (cam.transform != playerController.cameraTransform)
                            cam.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public bool IsLocalPlayer => isLocalPlayer;

    private void UpdateNetworkState(Vector3 pos, Quaternion rot)
    {
        if (isLocalPlayer) return; 

        targetPosition = pos;
        targetRotation = rot;
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            if (transform.hasChanged)
            {
                if (networkObject != null)
                {
                    networkObject.MarkDirty();
                }
                OnTransformModified?.Invoke();
                transform.hasChanged = false;
            }
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
