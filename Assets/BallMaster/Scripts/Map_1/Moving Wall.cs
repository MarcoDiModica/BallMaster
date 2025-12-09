using System.Collections;
using DG.Tweening;
using UnityEngine;

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

    [SerializeField]
    private NetworkManager networkManager;

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
            var replicationServer = FindFirstObjectByType<ReplicationManagerServer>();
            if (replicationServer != null)
            {
                replicationServer.RegisterObject(
                    networkObject.objectId,
                    ReplicatedObjectType.NetworkObject
                );
            }
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

        if (isHost)
        {
            var replicationServer = FindFirstObjectByType<ReplicationManagerServer>();
            if (replicationServer != null)
            {
                replicationServer.UnregisterObject(networkObject.objectId);
            }
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

            Tween bajar = transform
                .DOMove(posicionFinal, duracionBajada)
                .SetEase(Ease.InOutQuad)
                .OnUpdate(() => networkObject.isDirty = true);

            yield return bajar.WaitForCompletion();

            yield return new WaitForSeconds(tiempoEsperaSubida);

            Tween subir = transform
                .DOMove(posicionInicial, duracionSubida)
                .SetEase(Ease.InOutQuad)
                .OnUpdate(() => networkObject.isDirty = true);

            yield return subir.WaitForCompletion();
        }
    }
}
