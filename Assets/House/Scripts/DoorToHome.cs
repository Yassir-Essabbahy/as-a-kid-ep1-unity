using UnityEngine;
using System.Collections;

public class DoorToHome : MonoBehaviour, IInteractable
{
    [Header("Optional References (defaults to trigger or Player tag)")]
    public CharacterController characterController;
    public Transform player;
    public Transform spawnPoint;

    [Header("Settings")]
    public float fadeDuration = 0.8f;
    public AudioClip doorSound;
    public AudioSource audioSource;

    public string PromptMessage => LocalizationManager.Resolve("prompt_enter_door", "Enter Door");

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (spawnPoint == null)
        {
            var homeSpawn = GameObject.Find("HomeSpawnPoint");
            if (homeSpawn != null) spawnPoint = homeSpawn.transform;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(TeleportRoutine(other.transform, other.GetComponent<CharacterController>()));
        }
    }

    public void Interact()
    {
        StartCoroutine(TeleportRoutine(null, null));
    }

    public IEnumerator TeleportRoutine(Transform triggerPlayer = null, CharacterController triggerCC = null)
    {
        bool faderDone = false;
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeToBlack(fadeDuration, () => faderDone = true);
            while (!faderDone) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration * 0.5f);
        }

        if (doorSound != null && audioSource != null)
            audioSource.PlayOneShot(doorSound);

        Transform targetPlayer = player != null ? player : triggerPlayer;
        if (targetPlayer == null)
        {
            var pGo = GameObject.FindWithTag("Player");
            if (pGo != null) targetPlayer = pGo.transform;
            else if (Camera.main != null) targetPlayer = Camera.main.transform.root;
        }

        CharacterController cc = characterController != null ? characterController : triggerCC;
        if (cc == null && targetPlayer != null)
            cc = targetPlayer.GetComponent<CharacterController>();

        if (cc != null) cc.enabled = false;
        if (targetPlayer != null && spawnPoint != null)
        {
            targetPlayer.position = spawnPoint.position;
            targetPlayer.rotation = spawnPoint.rotation;
        }
        if (cc != null) cc.enabled = true;

        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeFromBlack(fadeDuration);
        }
    }
}
