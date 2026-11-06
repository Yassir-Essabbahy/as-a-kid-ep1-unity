using UnityEngine;

public class NPCLookAt : MonoBehaviour
{
    [Header("Look Target (defaults to Camera.main if null)")]
    public Transform target;
    public float speed = 3f;

    private float weight = 0f;
    private float targetWeight = 0f;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
    }

    public void StartLooking() => targetWeight = 1f;
    public void StopLooking() => targetWeight = 0f;

    void Update()
    {
        weight = Mathf.Lerp(weight, targetWeight, Time.deltaTime * speed);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        Transform lookTarget = target;
        if (lookTarget == null && Camera.main != null)
        {
            lookTarget = Camera.main.transform;
        }

        if (lookTarget != null)
        {
            anim.SetLookAtWeight(weight);
            anim.SetLookAtPosition(lookTarget.position);
        }
    }
}
