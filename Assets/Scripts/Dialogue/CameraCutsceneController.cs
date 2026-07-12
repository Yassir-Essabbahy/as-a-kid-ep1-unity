using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Generic "move the camera to look at X" tool. Reusable for the book,
/// the teacher, jumpscares, any future look-at moment. Two modes:
/// MoveTo = smooth eased drift (dozing off, drifting attention)
/// CutTo  = instant hard cut (getting caught, jump moments)
///
/// Assign this to a plain cutscene Camera's Transform, not the FPS rig's -
/// the player rig should simply not be active during the cutscene.
/// </summary>
public class CameraCutsceneController : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public void MoveTo(Transform target, float duration, Action onComplete = null)
    {
        StartCoroutine(MoveRoutine(target, duration, onComplete));
    }

    public void CutTo(Transform target)
    {
        playerCamera.SetPositionAndRotation(target.position, target.rotation);
    }

    private IEnumerator MoveRoutine(Transform target, float duration, Action onComplete)
    {
        Vector3 startPos = playerCamera.position;
        Quaternion startRot = playerCamera.rotation;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float eval = easeCurve.Evaluate(t / duration);
            playerCamera.position = Vector3.Lerp(startPos, target.position, eval);
            playerCamera.rotation = Quaternion.Slerp(startRot, target.rotation, eval);
            yield return null;
        }

        playerCamera.SetPositionAndRotation(target.position, target.rotation);
        onComplete?.Invoke();
    }
}