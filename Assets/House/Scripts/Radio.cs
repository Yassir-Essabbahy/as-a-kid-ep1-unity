using UnityEngine;
using System.Collections;

public class Radio : MonoBehaviour, IInteractable
{
    [Header("Songs")]
    public AudioClip[] songs;
    public AudioClip staticSound;

    [Header("Settings")]
    public float staticDuration = 1f;

    [Header("References")]
    public AudioSource audioSource;

    public string PromptMessage => "Turn On/Change Radio";

    public static Radio instance;
    private int currentSongIndex = 0;
    private bool isChanging = false;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    public void Interact()
    {
        if (isChanging) return;

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            return;
        }

        StartCoroutine(ChangeSong());
    }

    private IEnumerator ChangeSong()
    {
        if (audioSource == null) yield break;

        isChanging = true;

        if (staticSound != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(staticSound);
            yield return new WaitForSeconds(staticDuration);
        }

        if (songs != null && songs.Length > 0)
        {
            currentSongIndex = (currentSongIndex + 1) % songs.Length;

            if (songs[currentSongIndex] != null)
            {
                audioSource.clip = songs[currentSongIndex];
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        isChanging = false;
    }
}
