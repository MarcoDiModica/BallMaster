using UnityEngine;
using System.Collections;

[RequireComponent(typeof(NetworkObject))]
public class MovingWall : MonoBehaviour
{
    public float tiempoEspera = 30f;
    public float distanciaBajada = 2f; 
    public float duracionBajada = 2f; 
    public float tiempoEsperaSubida = 60f; 
    public float duracionSubida = 2f; 

    private Vector3 posicionInicial;
    private NetworkObject networkObject;
    private NetworkObjectManager networkObjectManager;
    private bool isHost = false;

    [SerializeField] private NetworkManager networkManager;

    void Start()
    {
        posicionInicial = transform.position;
        networkObject = GetComponent<NetworkObject>();

        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        networkObjectManager = FindFirstObjectByType<NetworkObjectManager>();
        isHost = networkManager != null && networkManager.isHost;

        if (string.IsNullOrEmpty(networkObject.objectId))
        {
            networkObject.objectId = "MovingWall_" + gameObject.name;
        }
        
        if (networkObjectManager != null)
        {
            networkObjectManager.RegisterNetworkObject(networkObject);
        }

        if (isHost)
        {
            StartCoroutine(BajarDespuesDeTiempoCoroutine());
        }
        else
        {
            networkObject.OnStateUpdated += OnNetworkStateUpdated;
        }
    }

    void OnDestroy()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated -= OnNetworkStateUpdated;
        }
    }

    void OnNetworkStateUpdated(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    System.Collections.IEnumerator BajarDespuesDeTiempoCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(tiempoEspera);

            Vector3 posicionFinal = posicionInicial + Vector3.down * distanciaBajada;
            float tiempoTranscurrido = 0f;

            while (tiempoTranscurrido < duracionBajada)
            {
                transform.position = Vector3.Lerp(posicionInicial, posicionFinal, tiempoTranscurrido / duracionBajada);
                networkObject.isDirty = true;
                tiempoTranscurrido += Time.deltaTime;
                yield return null;
            }

            transform.position = posicionFinal;
            networkObject.isDirty = true;

            yield return new WaitForSeconds(tiempoEsperaSubida);

            tiempoTranscurrido = 0f;
            while (tiempoTranscurrido < duracionSubida)
            {
                transform.position = Vector3.Lerp(posicionFinal, posicionInicial, tiempoTranscurrido / duracionSubida);
                networkObject.isDirty = true;
                tiempoTranscurrido += Time.deltaTime;
                yield return null;
            }

            transform.position = posicionInicial;
            networkObject.isDirty = true;
        }
    }
}
