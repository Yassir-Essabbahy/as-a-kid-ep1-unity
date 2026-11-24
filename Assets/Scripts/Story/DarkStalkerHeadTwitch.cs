using UnityEngine;

/// <summary>
/// Post-animation LateUpdate override for the Loop 2 stalker entity.
/// Keeps the character's white eyes locked unblinkingly onto the player camera
/// while adding unnatural neck roll and erratic dread head-twitching.
/// </summary>
public class DarkStalkerHeadTwitch : MonoBehaviour
{
    public Transform lookTarget;
    public float neckRollDegrees = 0f;
    public float twitchAmplitude = 0.5f;
    public float twitchFrequency = 18f;
    [Range(0f, 1f)] public float lookWeight = 0.92f;
    public bool lockOnTarget = true;

    private Animator _animator;
    private Transform _headBone;
    private float _currentRoll;

    public Transform HeadBone => _headBone;

    public Vector3 EyeWorldPosition
    {
        get
        {
            if (_headBone != null)
            {
                return _headBone.position + Vector3.up * 0.15f;
            }
            return transform.position + Vector3.up * 1.95f;
        }
    }

    private void Awake()
    {
        ResolveHeadBone();
    }

    private void ResolveHeadBone()
    {
        if (_headBone != null) return;
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
        if (_animator != null && _animator.isHuman)
        {
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    private void LateUpdate()
    {
        ResolveHeadBone();
        if (!lockOnTarget || _headBone == null || lookTarget == null) return;

        _currentRoll = Mathf.Lerp(_currentRoll, neckRollDegrees, Time.deltaTime * 10f);

        float t = Time.time * twitchFrequency;
        float noiseA = (Mathf.PerlinNoise(t, 1.37f) - 0.5f) * 2f;
        float noiseB = (Mathf.PerlinNoise(t * 1.43f, 7.91f) - 0.5f) * 2f;
        float noiseC = (Mathf.PerlinNoise(t * 0.87f, 19.23f) - 0.5f) * 2f;
        float spasmGate = Mathf.Sin(Time.time * 5.7f) > 0.94f ? 1.8f : 1.0f;

        float rollJitter = noiseA * twitchAmplitude * spasmGate;
        float yawJitter = noiseB * (twitchAmplitude * 0.65f) * spasmGate;
        float pitchJitter = noiseC * (twitchAmplitude * 0.45f) * spasmGate;

        Vector3 rawDir = lookTarget.position - EyeWorldPosition;
        if (rawDir.sqrMagnitude < 0.0001f) return;
        rawDir.Normalize();

        Vector3 bodyFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (bodyFwd.sqrMagnitude < 0.001f) bodyFwd = transform.forward;
        bodyFwd.Normalize();

        Vector3 flatDir = Vector3.ProjectOnPlane(rawDir, Vector3.up);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            flatDir = Vector3.RotateTowards(bodyFwd, flatDir.normalized, 55f * Mathf.Deg2Rad, 0f);
        }
        else
        {
            flatDir = bodyFwd;
        }

        float vertY = Mathf.Clamp(rawDir.y, -0.58f, 0.58f);
        Vector3 toTarget = (flatDir * Mathf.Sqrt(Mathf.Max(0.01f, 1f - vertY * vertY)) + Vector3.up * vertY).normalized;

        // Verified Mixamo-in-Blender Humanoid head bone orientation: face points out along -head.right (+90Y from +Z).
        Quaternion targetWorldRot = Quaternion.AngleAxis(_currentRoll + rollJitter, toTarget)
            * Quaternion.LookRotation(toTarget, Vector3.up)
            * Quaternion.Euler(pitchJitter, 90f + yawJitter, 0f);

        _headBone.rotation = Quaternion.Slerp(_headBone.rotation, targetWorldRot, Mathf.Clamp01(lookWeight));
    }
}
