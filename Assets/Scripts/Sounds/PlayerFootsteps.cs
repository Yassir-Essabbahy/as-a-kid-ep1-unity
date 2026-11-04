using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(FirstPersonController))]
public class PlayerFootsteps : MonoBehaviour
{
    public enum SurfaceType
    {
        Concrete = 0,
        Sand = 1,
        Auto = 2,
        Metro = 3
    }

    [Header("Surface Settings")]
    [Tooltip("Surface mode: Auto detects based on ground/scene, or force Concrete / Sand / Metro.")]
    public SurfaceType surfaceType = SurfaceType.Auto;
    public LayerMask groundCheckLayers = ~0;
    public float groundRaycastDistance = 2.0f;

    [Header("Audio Clips")]
    public AudioClip[] metroClips;
    public AudioClip[] concreteClips;
    public AudioClip[] sandClips;

    [Header("Step Timing")]
    [Tooltip("Time between footsteps while walking.")]
    public float walkStepInterval = 0.50f;
    [Tooltip("Time between footsteps while sprinting.")]
    public float sprintStepInterval = 0.30f;
    [Tooltip("Time between footsteps while crouching.")]
    public float crouchStepInterval = 0.65f;
    [Tooltip("Minimum horizontal velocity to trigger footstep sounds.")]
    public float minVelocityThreshold = 0.25f;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float walkVolume = 0.65f;
    [Range(0f, 1f)] public float sprintVolume = 0.85f;
    [Range(0f, 1f)] public float crouchVolume = 0.40f;
    [Range(0f, 1f)] public float landVolume = 0.75f;
    public float minPitch = 0.94f;
    public float maxPitch = 1.06f;

    [Header("Audio Source")]
    public AudioSource audioSource;

    private FirstPersonController fpc;
    private Rigidbody rb;
    private float stepTimer = 0f;
    private bool wasGrounded = true;
    private int lastPlayedIndex = -1;

    private void Awake()
    {
        fpc = GetComponent<FirstPersonController>();
        if (fpc != null)
        {
            rb = fpc.ControllerRigidbody != null ? fpc.ControllerRigidbody : GetComponent<Rigidbody>();
        }
        else
        {
            rb = GetComponent<Rigidbody>();
        }

        if (audioSource == null)
        {
            // Check for existing footstep audio source or create a dedicated one
            var existingSources = GetComponents<AudioSource>();
            foreach (var src in existingSources)
            {
                if (src.clip == null && !src.loop)
                {
                    audioSource = src;
                    break;
                }
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D stereo for player's own footsteps

        stepTimer = walkStepInterval * 0.5f;
        wasGrounded = true;
    }

    private void Update()
    {
        if (fpc == null) return;

        bool isGrounded = fpc.IsGrounded;
        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector2 horizontalVel = new Vector2(vel.x, vel.z);
        float speed = horizontalVel.magnitude;

        // Landing detection
        if (!wasGrounded && isGrounded && vel.y <= -1.5f && fpc.playerCanMove)
        {
            PlayLanding();
            stepTimer = GetCurrentStepInterval() * 0.75f;
        }

        wasGrounded = isGrounded;

        // Do not play movement footsteps if player cannot move or is in air or standing still
        if (!fpc.playerCanMove || !isGrounded || !fpc.IsWalking || speed < minVelocityThreshold)
        {
            // Reset timer so footsteps resume cleanly upon walking
            if (stepTimer <= 0f)
            {
                stepTimer = GetCurrentStepInterval() * 0.5f;
            }
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = GetCurrentStepInterval();
        }
    }

    private float GetCurrentStepInterval()
    {
        if (fpc == null) return walkStepInterval;
        if (fpc.IsSprinting) return sprintStepInterval;
        if (fpc.IsCrouched) return crouchStepInterval;
        return walkStepInterval;
    }

    private float GetCurrentVolume()
    {
        if (fpc == null) return walkVolume;
        if (fpc.IsSprinting) return sprintVolume;
        if (fpc.IsCrouched) return crouchVolume;
        return walkVolume;
    }

    public SurfaceType GetCurrentSurface()
    {
        if (surfaceType != SurfaceType.Auto)
            return surfaceType;

        // Raycast down to check surface under feet
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundCheckLayers, QueryTriggerInteraction.Ignore))
        {
            string colName = hit.collider.gameObject.name.ToLowerInvariant();
            string tagName = hit.collider.tag.ToLowerInvariant();

            if (colName.Contains("sand") || colName.Contains("beach") || colName.Contains("dirt") || tagName.Contains("sand"))
            {
                return SurfaceType.Sand;
            }

            if (colName.Contains("metro") || colName.Contains("train") || colName.Contains("station") || tagName.Contains("metro"))
            {
                return SurfaceType.Metro;
            }

            if (colName.Contains("concrete") || colName.Contains("cement") || colName.Contains("tile") ||
                colName.Contains("floor") || colName.Contains("ground") ||
                colName.Contains("packinterior") || colName.Contains("column") || colName.Contains("border"))
            {
                string scName = SceneManager.GetActiveScene().name.ToLowerInvariant();
                if (scName.Contains("gameplay") || scName.Contains("metro"))
                {
                    return SurfaceType.Metro;
                }
                return SurfaceType.Concrete;
            }
        }

        // Fallback to active scene name
        string sceneName = SceneManager.GetActiveScene().name.ToLowerInvariant();
        if (sceneName.Contains("beach") || sceneName.Contains("sand"))
        {
            return SurfaceType.Sand;
        }

        if (sceneName.Contains("gameplay") || sceneName.Contains("metro"))
        {
            return SurfaceType.Metro;
        }

        return SurfaceType.Concrete;
    }

    public void PlayFootstep()
    {
        AudioClip[] clips = GetClipsForSurface(GetCurrentSurface());
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = SelectRandomClip(clips);
        if (clip == null || audioSource == null) return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip, GetCurrentVolume());
    }

    public void PlayLanding()
    {
        AudioClip[] clips = GetClipsForSurface(GetCurrentSurface());
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = SelectRandomClip(clips);
        if (clip == null || audioSource == null) return;

        audioSource.pitch = Random.Range(minPitch * 0.95f, maxPitch * 0.95f);
        audioSource.PlayOneShot(clip, landVolume);
    }

    private AudioClip[] GetClipsForSurface(SurfaceType surface)
    {
        switch (surface)
        {
            case SurfaceType.Metro:
                if (metroClips != null && metroClips.Length > 0) return metroClips;
                if (concreteClips != null && concreteClips.Length > 0) return concreteClips;
                return sandClips;

            case SurfaceType.Sand:
                if (sandClips != null && sandClips.Length > 0) return sandClips;
                if (concreteClips != null && concreteClips.Length > 0) return concreteClips;
                return metroClips;

            case SurfaceType.Concrete:
            default:
                if (concreteClips != null && concreteClips.Length > 0) return concreteClips;
                if (metroClips != null && metroClips.Length > 0) return metroClips;
                return sandClips;
        }
    }

    private AudioClip SelectRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        int index = Random.Range(0, clips.Length);
        if (index == lastPlayedIndex)
        {
            index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        }
        lastPlayedIndex = index;
        return clips[index];
    }

#if UNITY_EDITOR
    private void Reset()
    {
        LoadClipsFromAssets();
    }

    [ContextMenu("Reload Footstep Clips")]
    public void LoadClipsFromAssets()
    {
        var foundMetro = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Assets/Episode 1/Sounds/Footsteps/Metro" });
        if (foundMetro != null && foundMetro.Length > 0)
        {
            var list = new List<AudioClip>();
            foreach (var guid in foundMetro)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) list.Add(clip);
            }
            metroClips = list.ToArray();
        }

        var foundConcrete = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Assets/Episode 1/Sounds/Footsteps/Concrete" });
        if (foundConcrete != null && foundConcrete.Length > 0)
        {
            var list = new List<AudioClip>();
            foreach (var guid in foundConcrete)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) list.Add(clip);
            }
            concreteClips = list.ToArray();
        }

        var foundSand = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Assets/Episode 1/Sounds/Footsteps/Sand" });
        if (foundSand != null && foundSand.Length > 0)
        {
            var list = new List<AudioClip>();
            foreach (var guid in foundSand)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) list.Add(clip);
            }
            sandClips = list.ToArray();
        }
    }
#endif
}
