using UnityEngine;

[RequireComponent(typeof(Light))]
public class DiscoColorCycle : MonoBehaviour
{
    [SerializeField] private float colorCycleSpeed = 0.5f; // hue shift per second
    [SerializeField] [Range(0f, 1f)] private float saturation = 1f;
    [SerializeField] [Range(0f, 1f)] private float brightness = 1f;

    private Light discoLight;
    private float currentHue;

    private void Awake()
    {
        discoLight = GetComponent<Light>();
    }

    private void Update()
    {
        currentHue += colorCycleSpeed * Time.deltaTime;
        if (currentHue >= 1f) currentHue -= 1f;

        discoLight.color = Color.HSVToRGB(currentHue, saturation, brightness);
    }
}