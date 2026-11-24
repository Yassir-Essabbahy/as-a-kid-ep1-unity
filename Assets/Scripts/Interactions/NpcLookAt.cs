using UnityEngine;

public class NpcLookAt : MonoBehaviour
{
    public Animator animator;
    public bool IKActive = false;
    public Transform LookAtObj = null;
    public float LookWeight = 0f;

    [Header("Look-At Tuning")]
    public float lookSpeed = 3.5f;
    [Range(0f, 1f)] public float bodyWeight = 0.25f;
    [Range(0f, 1f)] public float headWeight = 1f;
    [Range(0f, 1f)] public float eyesWeight = 0.85f;
    [Range(0f, 1f)] public float clampWeight = 0.5f;

    private Transform _headBone;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        LookWeight = IKActive ? 1f : 0f;
        ResolveLookTargetIfMissing();
        CacheHeadBoneIfNeeded();
    }

    private void ResolveLookTargetIfMissing()
    {
        if (LookAtObj != null)
            return;

        if (FirstPersonController.ActiveInstance != null && FirstPersonController.ActiveInstance.playerCamera != null)
        {
            LookAtObj = FirstPersonController.ActiveInstance.playerCamera.transform;
            return;
        }

        if (Camera.main != null)
        {
            LookAtObj = Camera.main.transform;
        }
    }

    private void CacheHeadBoneIfNeeded()
    {
        if (_headBone != null)
            return;

        if (animator != null && animator.isHuman && animator.runtimeAnimatorController != null)
            return;

        var transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t == transform)
                continue;

            if (string.Equals(t.name, "mixamorig:Head", System.StringComparison.OrdinalIgnoreCase) ||
                t.name.EndsWith("Head", System.StringComparison.OrdinalIgnoreCase))
            {
                _headBone = t;
                return;
            }
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        ResolveLookTargetIfMissing();
        if (!animator || LookAtObj == null) return;

        LookWeight = Mathf.Lerp(LookWeight, IKActive ? 1f : 0f, Time.unscaledDeltaTime * lookSpeed);
        if (LookWeight <= 0.001f && !IKActive)
        {
            LookWeight = 0f;
            animator.SetLookAtWeight(0f);
            return;
        }

        animator.SetLookAtWeight(LookWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(LookAtObj.position);
    }

    private void LateUpdate()
    {
        if (animator != null && animator.isHuman && animator.runtimeAnimatorController != null)
            return;

        if (_headBone == null)
            CacheHeadBoneIfNeeded();

        ResolveLookTargetIfMissing();
        if (_headBone == null || LookAtObj == null)
            return;

        LookWeight = Mathf.Lerp(LookWeight, IKActive ? 1f : 0f, Time.unscaledDeltaTime * lookSpeed);
        if (LookWeight <= 0.001f)
        {
            if (!IKActive) LookWeight = 0f;
            return;
        }

        Vector3 toTarget = LookAtObj.position - _headBone.position;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);
        float yaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -60f, 60f);

        float horiz = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
        float pitch = Mathf.Clamp(-Mathf.Atan2(localDir.y, Mathf.Max(0.0001f, horiz)) * Mathf.Rad2Deg, -30f, 30f);

        Vector3 clampedLocalDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        Vector3 clampedWorldDir = transform.TransformDirection(clampedLocalDir);
        Quaternion targetWorldRot = Quaternion.LookRotation(clampedWorldDir, transform.up);

        _headBone.rotation = Quaternion.Slerp(_headBone.rotation, targetWorldRot, LookWeight);
    }
}