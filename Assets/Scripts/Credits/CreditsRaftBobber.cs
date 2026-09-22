using UnityEngine;

namespace Credits
{
    /// <summary>
    /// Animates the raft drifting into open water during Shot 4 with gentle wave bobbing and roll.
    /// </summary>
    public class CreditsRaftBobber : MonoBehaviour
    {
        [Header("Drift")]
        public Vector3 driftDirection = Vector3.forward;
        public float driftSpeed = 0.75f;

        [Header("Wave Bobbing")]
        public float waveFrequency = 1.5f;
        public float waveAmplitude = 0.05f;
        public float rollFrequency = 1.1f;
        public float rollAmplitude = 1.8f;

        private Vector3 _basePos;
        private Quaternion _baseRot;
        private float _timeAccumulator;

        private void Start()
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
        }

        private void Update()
        {
            _timeAccumulator += Time.deltaTime;

            // Forward drift
            _basePos += driftDirection.normalized * (driftSpeed * Time.deltaTime);

            // Vertical bobbing
            float verticalOffset = Mathf.Sin(_timeAccumulator * waveFrequency) * waveAmplitude;
            transform.position = _basePos + new Vector3(0f, verticalOffset, 0f);

            // Gentle rolling
            float roll = Mathf.Sin(_timeAccumulator * rollFrequency) * rollAmplitude;
            float pitch = Mathf.Cos(_timeAccumulator * waveFrequency * 0.8f) * (rollAmplitude * 0.5f);
            transform.rotation = _baseRot * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
