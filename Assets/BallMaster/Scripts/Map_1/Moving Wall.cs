using UnityEngine;

public class MovingWall : MonoBehaviour
{
    public float tiempoEspera = 30f; // tiempo en segundos antes de bajar
    public float distanciaBajada = 2f; // unidades que baja en Y
    public float duracionBajada = 2f; // duración del movimiento descendente en segundos
    public float tiempoEsperaSubida = 60f; // tiempo en segundos antes de volver a subir
    public float duracionSubida = 2f; // duración del movimiento ascendente en segundos

    private Vector3 posicionInicial;

    void Start()
    {
        posicionInicial = transform.position;
        StartCoroutine(BajarDespuesDeTiempoCoroutine());
    }

    System.Collections.IEnumerator BajarDespuesDeTiempoCoroutine()
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

        Debug.Log($"{gameObject.name} ha bajado {distanciaBajada} unidades después de {tiempoEspera} segundos.");

        // Espera antes de volver a la posición inicial
        yield return new WaitForSeconds(tiempoEsperaSubida);

        // Inicia la subida progresiva
        tiempoTranscurrido = 0f;
        while (tiempoTranscurrido < duracionSubida)
        {
            transform.position = Vector3.Lerp(posicionFinal, posicionInicial, tiempoTranscurrido / duracionSubida);
            tiempoTranscurrido += Time.deltaTime;
            yield return null;
        }

        transform.position = posicionInicial;

        Debug.Log($"{gameObject.name} ha vuelto a la posición inicial después de {tiempoEsperaSubida} segundos.");
    }
}
