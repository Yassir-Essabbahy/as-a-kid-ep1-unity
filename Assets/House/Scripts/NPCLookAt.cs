using UnityEngine;

public class NPCLookAt : MonoBehaviour
{
    [Header("Look Target (defaults to active Camera if null)")]
    public Transform target;
    public float speed = 3.5f;

    [Header("IK Weights")]
    [Range(0f, 1f)] public float bodyWeight = 0.15f;
    [Range(0f, 1f)] public float headWeight = 0.85f;
    [Range(0f, 1f)] public float eyesWeight = 0.6f;
    [Range(0f, 1f)] public float clampWeight = 0.5f;

    private float weight = 0f;
    private float targetWeight = 0f;
    private Animator anim;

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        GetTarget();
    }

    public void StartLooking() => targetWeight = 1f;
    public void StopLooking() => targetWeight = 0f;
    public void SetTarget(Transform newTarget) => target = newTarget;

    public Transform GetTarget()
    {
        if (target != null && target.gameObject.activeInHierarchy) return target;

        if (Camera.main != null && Camera.main.gameObject.activeInHierarchy)
        {
            target = Camera.main.transform;
            return target;
        }

        var cam = FindAnyObjectByType<Camera>();
        if (cam != null && cam.gameObject.activeInHierarchy)
        {
            target = cam.transform;
            return target;
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            return target;
        }

        return null;
    }

    private void Update()
    {
        weight = Mathf.Lerp(weight, targetWeight, Time.deltaTime * speed);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        Transform lookTarget = GetTarget();
        if (lookTarget != null && weight > 0.001f)
        {
            anim.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clampWeight);
            anim.SetLookAtPosition(lookTarget.position);
        }
        else
        {
            anim.SetLookAtWeight(0f);
        }
    }
}
