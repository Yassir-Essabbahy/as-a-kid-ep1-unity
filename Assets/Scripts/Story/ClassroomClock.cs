using System.Collections;
using UnityEngine;

public class ClassroomClock : MonoBehaviour
{
    [Header("Clock Hands")]
    public Transform hourHand;
    public Transform minuteHand;

    [Header("Settings")]
    public Vector3 rotationAxis = Vector3.forward;

    public void AdvanceTime(float hoursToAdvance, float duration, System.Action onComplete = null)
    {
        StartCoroutine(AdvanceTimeRoutine(hoursToAdvance, duration, onComplete));
    }

    private IEnumerator AdvanceTimeRoutine(float hoursToAdvance, float duration, System.Action onComplete)
    {
        float elapsed = 0f;
        float startMinuteAngle = minuteHand != null ? minuteHand.localEulerAngles.z : 0f;
        float startHourAngle = hourHand != null ? hourHand.localEulerAngles.z : 0f;

        float totalMinuteDegrees = hoursToAdvance * 360f;
        float totalHourDegrees = hoursToAdvance * 30f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (minuteHand != null)
            {
                minuteHand.localRotation = Quaternion.Euler(0, 0, startMinuteAngle - t * totalMinuteDegrees);
            }

            if (hourHand != null)
            {
                hourHand.localRotation = Quaternion.Euler(0, 0, startHourAngle - t * totalHourDegrees);
            }

            yield return null;
        }

        if (minuteHand != null)
        {
            minuteHand.localRotation = Quaternion.Euler(0, 0, startMinuteAngle - totalMinuteDegrees);
        }
        if (hourHand != null)
        {
            hourHand.localRotation = Quaternion.Euler(0, 0, startHourAngle - totalHourDegrees);
        }

        onComplete?.Invoke();
    }
}
