using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Metro Sound System:
/// - Continuous Moroccan ONCF metro station background ambiance with smooth looping.
/// - Periodic and pre-arrival platform PA announcements (Oncf1, Oncf2).
/// - Synchronized audio cueing with incoming trains.
/// </summary>
public class MetroSoundSystem : MonoBehaviour
{
    public static MetroSoundSystem Instance { get; private set; }

    [Header("Station Ambiance")]
    [Tooltip("Looping metro station ambient sound (e.g. OncfAmbiance.MP3)")]
    public AudioClip stationAmbiance;
    [Range(0f, 1f)] public float ambianceVolume = 0.35f;
    public AudioSource ambianceSource;

    [Header("Station Announcements (PA System)")]
    [Tooltip("Moroccan ONCF station announcement chimes and lines (Oncf1, Oncf2).")]
    public AudioClip[] stationAnnouncements;
    [Range(0f, 1f)] public float announcementVolume = 0.65f;
    public AudioSource announcementSource;

    [Header("Announcement Scheduling")]
    [Tooltip("If true, automatically plays station announcements periodically.")]
    public bool enablePeriodicAnnouncements = true;
    public float minAnnouncementInterval = 75f;
    public float maxAnnouncementInterval = 130f;
    [Tooltip("If true, triggers an announcement when a train is approaching the station.")]
    public bool announceOnTrainApproach = true;

    private int lastAnnouncementIndex = -1;
    private Coroutine periodicRoutine;
    private float lastAnnouncementTime = -999f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        PrewarmAudioClips();
        SetupAudioSources();
    }

    private void Start()
    {
        StartAmbiance();

        if (enablePeriodicAnnouncements)
        {
            periodicRoutine = StartCoroutine(PeriodicAnnouncementRoutine());
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void PrewarmAudioClips()
    {
        if (stationAmbiance != null && stationAmbiance.loadState == AudioDataLoadState.Unloaded)
        {
            stationAmbiance.LoadAudioData();
        }

        if (stationAnnouncements != null)
        {
            foreach (var clip in stationAnnouncements)
            {
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }
            }
        }
    }

    private void SetupAudioSources()
    {
        // 1. Ambiance Source
        if (ambianceSource == null)
        {
            // Look for existing AmbianceAudioSource in scene or under player
            var existing = GameObject.Find("AmbianceAudioSource");
            if (existing != null)
            {
                ambianceSource = existing.GetComponent<AudioSource>();
            }

            if (ambianceSource == null)
            {
                var ambObj = new GameObject("MetroAmbianceSource");
                ambObj.transform.SetParent(transform, false);
                ambianceSource = ambObj.AddComponent<AudioSource>();
            }
        }

        ambianceSource.playOnAwake = false;
        ambianceSource.loop = true;
        ambianceSource.spatialBlend = 0f; // 2D immersive background ambiance
        ambianceSource.volume = ambianceVolume;
        ambianceSource.priority = 64; // High priority so ambient never cuts out

        // 2. Announcement Source
        if (announcementSource == null)
        {
            var paObj = new GameObject("MetroPASource");
            paObj.transform.SetParent(transform, false);
            announcementSource = paObj.AddComponent<AudioSource>();
        }

        announcementSource.playOnAwake = false;
        announcementSource.loop = false;
        announcementSource.spatialBlend = 0.15f; // Slight spatial spread for wide station acoustic feel
        announcementSource.volume = announcementVolume;
        announcementSource.priority = 32; // Higher priority for clear voice lines
    }

    public void StartAmbiance()
    {
        if (ambianceSource == null) return;

        if (stationAmbiance != null)
        {
            ambianceSource.clip = stationAmbiance;
        }

        if (ambianceSource.clip != null)
        {
            ambianceSource.loop = true;
            ambianceSource.volume = ambianceVolume;
            if (!ambianceSource.isPlaying)
            {
                ambianceSource.Play();
            }
        }
    }

    public void PlayAnnouncement(int index = -1)
    {
        if (stationAnnouncements == null || stationAnnouncements.Length == 0) return;
        if (announcementSource == null) return;

        // Prevent announcements back-to-back within 15 seconds
        if (Time.time - lastAnnouncementTime < 15f) return;

        int selected = index;
        if (selected < 0 || selected >= stationAnnouncements.Length)
        {
            if (stationAnnouncements.Length == 1)
            {
                selected = 0;
            }
            else
            {
                selected = Random.Range(0, stationAnnouncements.Length);
                if (selected == lastAnnouncementIndex)
                {
                    selected = (selected + 1) % stationAnnouncements.Length;
                }
            }
        }

        lastAnnouncementIndex = selected;
        AudioClip clip = stationAnnouncements[selected];
        if (clip != null)
        {
            lastAnnouncementTime = Time.time;
            announcementSource.pitch = Random.Range(0.98f, 1.02f);
            announcementSource.PlayOneShot(clip, announcementVolume);
        }
    }

    /// <summary>
    /// Invoked when a train is approaching the station to sound platform announcements.
    /// </summary>
    public void OnTrainApproaching()
    {
        if (!announceOnTrainApproach) return;

        PlayAnnouncement();
    }

    private IEnumerator PeriodicAnnouncementRoutine()
    {
        // Initial delay before first periodic announcement
        yield return new WaitForSeconds(Random.Range(30f, 60f));

        while (enablePeriodicAnnouncements)
        {
            float wait = Random.Range(minAnnouncementInterval, maxAnnouncementInterval);
            yield return new WaitForSeconds(wait);

            // Only play if not recently played
            if (Time.time - lastAnnouncementTime >= 30f)
            {
                PlayAnnouncement();
            }
        }
    }

    [ContextMenu("Play Announcement Now")]
    public void PlayTestAnnouncement()
    {
        lastAnnouncementTime = -999f;
        PlayAnnouncement();
    }
}
