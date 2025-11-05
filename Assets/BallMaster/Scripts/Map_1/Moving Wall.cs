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

    void Start()
    {
        posicionInicial = transform.position;
        networkObject = GetComponent<NetworkObject>();

        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            if (string.IsNullOrEmpty(networkObject.objectId))
            {
                networkObject.objectId = "MovingWall_" + GetInstanceID();
                Debug.LogWarning($"Asignado objectId temporal a MovingWall: {networkObject.objectId}");
            }
            StartCoroutine(BajarDespuesDeTiempoCoroutine());
        }
        else
        {
            enabled = false;
        }
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
                tiempoTranscurrido += Time.deltaTime;
                yield return null;
            }

            transform.position = posicionFinal;

            yield return new WaitForSeconds(tiempoEsperaSubida);

            tiempoTranscurrido = 0f;
            while (tiempoTranscurrido < duracionSubida)
            {
                transform.position = Vector3.Lerp(posicionFinal, posicionInicial, tiempoTranscurrido / duracionSubida);
                tiempoTranscurrido += Time.deltaTime;
                yield return null;
            }

            transform.position = posicionInicial;
        }
    }
}
