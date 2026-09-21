using UnityEngine;

public class NpcLookAt : MonoBehaviour
{
    public Animator animator;
    public bool IKActive = false;
    public Transform LookAtObj = null;
    public float LookWeight = 0f;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!animator || LookAtObj == null) return;

        LookWeight = Mathf.Lerp(LookWeight, IKActive ? 1f : 0f, Time.deltaTime * 2f);

        animator.SetLookAtWeight(LookWeight);
        animator.SetLookAtPosition(LookAtObj.position);
    }
}