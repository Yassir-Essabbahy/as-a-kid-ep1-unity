using System.Collections;
using UnityEngine;

public class MapCollapseSequence : MonoBehaviour
{
    [Header("Trigger Source")]
    public NpcConversation triggerAfterConversation;

    [Header("Shake")]
    public Transform shakeTarget;
    public float shakeDuration = 5f;
    public float shakeIntensity = 0.08f;
    public float shakeSpeed = 30f;

    [Header("Collapsing Map Pieces")]
    public GameObject[] mapPieces;
    public float fallSpeed = 8f;
    public float fallDistance = 30f;
    public float staggerDelay = 0.25f;
    public float randomDelayVariance = 0.15f;
    public float maxTiltAngle = 15f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip rumbleSound;
    public AudioClip[] impactSounds;
    public float rumbleFadeOutDuration = 1.5f;

    void OnEnable()
    {
        if (triggerAfterConversation != null)
        {
            triggerAfterConversation.OnConversationFinished += StartCollapse;
        }
    }

    void OnDisable()
    {
        if (triggerAfterConversation != null)
        {
            triggerAfterConversation.OnConversationFinished -= StartCollapse;
        }
    }

    void StartCollapse()
    {
        StartCoroutine(RunCollapse());
    }

    IEnumerator RunCollapse()
    {
        float rumbleBaseVolume = audioSource != null ? audioSource.volume : 1f;

        if (audioSource != null && rumbleSound != null)
        {
            audioSource.clip = rumbleSound;
            audioSource.volume = rumbleBaseVolume;
            audioSource.loop = true;
            audioSource.Play();
        }

        StartCoroutine(ShakeCamera(shakeDuration));
        StartCoroutine(DropAllPieces());

        yield return new WaitForSeconds(shakeDuration);

        if (audioSource != null)
        {
            yield return StartCoroutine(FadeOutAudio(audioSource, rumbleFadeOutDuration, rumbleBaseVolume));
        }
    }

    IEnumerator FadeOutAudio(AudioSource source, float duration, float startVolume)
    {
        if (source == null || !source.isPlaying)
            yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        source.volume = 0f;
        source.loop = false;
        source.Stop();
        source.volume = startVolume; // reset for next use
    }

    IEnumerator DropAllPieces()
    {
        foreach (GameObject piece in mapPieces)
        {
            StartCoroutine(DropPiece(piece));

            float delay = staggerDelay + Random.Range(-randomDelayVariance, randomDelayVariance);
            yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
        }
    }

    IEnumerator DropPiece(GameObject piece)
    {
        if (piece == null) yield break;

        if (audioSource != null && impactSounds != null && impactSounds.Length > 0)
        {
            AudioClip impact = impactSounds[Random.Range(0, impactSounds.Length)];
            audioSource.PlayOneShot(impact);
        }

        Vector3 startPos = piece.transform.position;
        Vector3 targetPos = startPos + Vector3.down * fallDistance;

        Quaternion startRot = piece.transform.rotation;
        Vector3 randomTilt = new Vector3(
            Random.Range(-maxTiltAngle, maxTiltAngle),
            Random.Range(-maxTiltAngle, maxTiltAngle),
            Random.Range(-maxTiltAngle, maxTiltAngle)
        );
        Quaternion targetRot = startRot * Quaternion.Euler(randomTilt);

        float elapsed = 0f;
        float duration = Mathf.Sqrt(2f * fallDistance / fallSpeed); // gravity-like fall duration

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float easedT = t * t; // ease-in, accelerates like gravity

            piece.transform.position = Vector3.Lerp(startPos, targetPos, easedT);
            piece.transform.rotation = Quaternion.Slerp(startRot, targetRot, easedT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        piece.transform.position = targetPos;
        piece.transform.rotation = targetRot;
        piece.SetActive(false);
    }

    IEnumerator ShakeCamera(float duration)
    {
        if (shakeTarget == null) yield break;

        Vector3 originalPos = shakeTarget.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = (Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) - 0.5f) * shakeIntensity;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * shakeSpeed) - 0.5f) * shakeIntensity;

            shakeTarget.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeTarget.localPosition = originalPos;
    }
}