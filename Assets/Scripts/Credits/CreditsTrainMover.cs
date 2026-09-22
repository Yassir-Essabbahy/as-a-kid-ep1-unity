using System;
using UnityEngine;

namespace Credits
{
    /// <summary>
    /// Moves the train across the camera during Shot 1 (Metro Platform) to serve as a physical wipe cut.
    /// </summary>
    public class CreditsTrainMover : MonoBehaviour
    {
        [Header("Movement")]
        public Vector3 startPosition = new Vector3(0f, 0f, -40f);
        public Vector3 endPosition = new Vector3(0f, 0f, 60f);
        public float speed = 22f;
        [Range(0f, 1f)]
        public float wipeTriggerProgress = 0.48f; // Progress along path (0 to 1) where train body occludes lens

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip railwayAudio;

        public event Action OnWipePointReached;

        private bool _isMoving = false;
        private bool _wipeTriggered = false;

        public void Initialize(Vector3 start, Vector3 end, float moveSpeed, float triggerProgress = 0.48f)
        {
            startPosition = start;
            endPosition = end;
            speed = moveSpeed;
            wipeTriggerProgress = triggerProgress;
            transform.position = startPosition;
        }

        public void StartMoving()
        {
            _isMoving = true;
            _wipeTriggered = false;

            if (audioSource != null && railwayAudio != null)
            {
                audioSource.clip = railwayAudio;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void Update()
        {
            if (!_isMoving) return;

            transform.position = Vector3.MoveTowards(transform.position, endPosition, speed * Time.deltaTime);

            float totalDist = Vector3.Distance(startPosition, endPosition);
            if (totalDist > 0.001f)
            {
                float traveled = Vector3.Distance(startPosition, transform.position);
                float progress = traveled / totalDist;

                // Check if train body has occluded the camera lens
                if (!_wipeTriggered && progress >= wipeTriggerProgress)
                {
                    _wipeTriggered = true;
                    OnWipePointReached?.Invoke();
                }
            }

            if (Vector3.Distance(transform.position, endPosition) < 0.05f)
            {
                _isMoving = false;
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }
        }
    }
}
