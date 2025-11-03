using UnityEngine;

public class PlayerControllerUDP : MonoBehaviour
{
    private Lab2_UDPClient client;

    void Start()
    {
        client = Lab2_UDPClient.Instance;

        if (Lab2_UDPClient.Instance != null)
        {
            this.enabled = false;
        }
    }

    void Update()
    {
        if (client == null) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        PlayerInputData inputData = new PlayerInputData
        {
            horizontal = h,
            vertical = v,
        };

        // client.SendPlayerInput(inputData);  alguna función nueva se necesita hacer en UPDClient
    }
}
