using UnityEngine;

public class NetworkPlayer : MonoBehaviour
{
    public float moveSpeed = 5f;
    public bool isLocalPlayer = false;

    private NetworkObject networkObject;

    void Start()
    {
        networkObject = GetComponent<NetworkObject>();
        
        if (networkObject == null)
        {
            networkObject = gameObject.AddComponent<NetworkObject>();
            networkObject.objectId = gameObject.name;
        }
    }

    void Update()
    {
        if (!isLocalPlayer)
            return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (NetworkManager.Instance.isHost)
        {
            MovePlayer(h, v);
        }
        else
        {
            NetworkManager.Instance.SendInput(h, v);
        }
    }

    void MovePlayer(float h, float v)
    {
        Vector3 movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
        transform.position += movement;
    }
}