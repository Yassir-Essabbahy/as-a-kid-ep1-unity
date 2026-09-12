using UnityEngine;

namespace MoroccanBeach
{
    [RequireComponent(typeof(Renderer))]
    public class PSXWaterSurface : MonoBehaviour
    {
        [Header("Wave Motion")]
        [Tooltip("Horizontal UV scrolling speed")]
        public Vector2 uvScrollSpeed = new Vector2(0.04f, 0.08f);

        [Tooltip("Vertical wave bobbing amplitude")]
        public float waveHeight = 0.08f;

        [Tooltip("Vertical wave frequency")]
        public float waveSpeed = 1.2f;

        private Renderer rend;
        private Material mat;
        private Vector3 initialPosition;
        private Vector2 uvOffset;

        private void Start()
        {
            rend = GetComponent<Renderer>();
            if (rend != null)
            {
                mat = rend.material;
            }
            initialPosition = transform.position;
        }

        private void Update()
        {
            // 1. Texture UV scrolling for retro wave roll
            if (mat != null)
            {
                uvOffset += uvScrollSpeed * Time.deltaTime;
                mat.mainTextureOffset = uvOffset;
            }

            // 2. Subtle physical vertical bobbing for shore tides
            float yOffset = Mathf.Sin(Time.time * waveSpeed) * waveHeight;
            transform.position = new Vector3(initialPosition.x, initialPosition.y + yOffset, initialPosition.z);
        }

        private void OnDestroy()
        {
            if (mat != null && Application.isPlaying)
            {
                Destroy(mat);
            }
        }
    }
}
