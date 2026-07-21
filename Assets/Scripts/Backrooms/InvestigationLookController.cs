using UnityEngine;
using Unity.Cinemachine;

public class InvestigationLookController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera lookDonkey;
    [SerializeField] private CinemachineCamera lookPaper;

    private bool lookingAtPaper = false;

    private const int ActivePriority = 20;
    private const int InactivePriority = 10;

    private void Start()
    {
        SetLookingAtPaper(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetLookingAtPaper(!lookingAtPaper);
        }
    }

    private void SetLookingAtPaper(bool paper)
    {
        lookingAtPaper = paper;
        lookDonkey.Priority = paper ? InactivePriority : ActivePriority;
        lookPaper.Priority = paper ? ActivePriority : InactivePriority;
    }
}