using TMPro;
using UnityEngine;

public class fpsTMP : MonoBehaviour
{
    public TextMeshProUGUI fpsText;

    private float time = 0.0f;
    private int frameCount = 0;

    void LateUpdate()
    {
        time += Time.deltaTime;
        frameCount++;

        if (time >= 1.0f)
        {
            float fps = frameCount / time;
            fpsText.text = $"{fps:F2}";

            time = 0.0f;
            frameCount = 0;
        }
    }
}
