using UnityEngine;
using System.Collections;

[RequireComponent(typeof(NetworkObject))]
public class FallingBuilding : MonoBehaviour
{
    public float timeBeforeFalling = 5f;
    public float fallSpeed = 5f;
    public float fallDuration = 3f;
    public float respawnTime = 30f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private NetworkObject networkObject;

    [SerializeField] private NetworkManager networkManager;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        networkObject = GetComponent<NetworkObject>();

        if (networkManager != null && networkManager.isHost)
        {
            if (string.IsNullOrEmpty(networkObject.objectId))
            {
                networkObject.objectId = "FallingBuilding_" + GetInstanceID();
            }
            StartCoroutine(FallCycle());
        }
    }

    IEnumerator FallCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(timeBeforeFalling);

            float elapsed = 0f;

            while (elapsed < fallDuration)
            {
                transform.position += Vector3.down * fallSpeed * Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(respawnTime);

            transform.position = initialPosition;
            transform.rotation = initialRotation;
        }
    }
}
