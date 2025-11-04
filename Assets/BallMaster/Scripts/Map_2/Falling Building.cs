using UnityEngine;
using System.Collections;

public class FallingBuilding : MonoBehaviour
{

    public float tiempoEspera = 30f; // tiempo en segundos antes de bajar
    public float distanciaBajada = 2f; // unidades que baja en Y
    public float duracionBajada = 2f; // duración del movimiento descendente en segundos
    public Vector3 velocidadRotacion = new Vector3(0f, 20f, 0f); // velocidad de rotación en grados por segundo

    private Vector3 posicionInicial;
    private NetworkObject networkObject;

    void Start()
    {
        posicionInicial = transform.position;
        networkObject = GetComponent<NetworkObject>();
        StartCoroutine(BajarYRotarCoroutine());

        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            if (string.IsNullOrEmpty(networkObject.objectId))
            {
                networkObject.objectId = "MovingWall_" + GetInstanceID();
                Debug.LogWarning($"Asignado objectId temporal a MovingWall: {networkObject.objectId}. Es mejor asignarlo en el Inspector.");
            }
            StartCoroutine(BajarYRotarCoroutine());
        }
        else
        {
            enabled = false;
        }
    }

    System.Collections.IEnumerator BajarYRotarCoroutine()
    {
        yield return new WaitForSeconds(tiempoEspera);

        Vector3 posicionFinal = posicionInicial + Vector3.down * distanciaBajada;
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracionBajada)
        {
            // Interpolación de posición
            transform.position = Vector3.Lerp(posicionInicial, posicionFinal, tiempoTranscurrido / duracionBajada);

            // Aplicar rotación
            transform.Rotate(velocidadRotacion * Time.deltaTime);

            tiempoTranscurrido += Time.deltaTime;
            yield return null;
        }

        transform.position = posicionFinal;

        Debug.Log($"{gameObject.name} ha bajado {distanciaBajada} unidades después de {tiempoEspera} segundos.");
    }
}
