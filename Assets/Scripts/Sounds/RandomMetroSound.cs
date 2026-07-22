using UnityEngine;

public class RandomMetroSound : MonoBehaviour
{

    public AudioSource audioSource;
    public AudioClip[] metroClips;

    public float minDelay = 15f;
    public float maxDelay = 30f;

    private int currentIndex = 0;

    void Start()
    {
        NextSound();
    }

    void TryPlayNextSound()
    {
        if (audioSource.isPlaying)
        {
            Invoke(nameof(TryPlayNextSound), 0.5f);
            return;
        }
        PlayRandomClip();
    }


    void NextSound()
    {
        float delay = Random.Range(minDelay, maxDelay);
        Invoke(nameof(TryPlayNextSound), delay);
    }
    void PlayRandomClip()
    {

        audioSource.PlayOneShot(metroClips[currentIndex]);
        currentIndex = (currentIndex + 1) % metroClips.Length;
        
        NextSound();
    }
}
