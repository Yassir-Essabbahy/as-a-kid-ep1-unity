using UnityEngine;
using System.Collections;

[System.Serializable]
public class LocationOption
{
    public string locationName;       // shown on button, e.g. "l7anout", "dar jiran"
    public Transform spawnPoint;      // where to teleport the player
}

public class LocationChooser : MonoBehaviour, IInteractable
{
    [Header("Destinations")]
    public LocationOption[] locations;

    [Header("References (optional - defaults to tag/main camera)")]
    public Transform player;          // drag your player here
    public CharacterController characterController; // drag player's CharacterController

    [Header("Audio")]
    public AudioClip doorOpenSound;
    public AudioSource audioSource;   // can be on this object or shared

    [Header("Timing")]
    public float fadeOutDuration = 0.5f;
    public float blackScreenHold = 0.3f;
    public float fadeInDuration = 0.5f;

    public string PromptMessage => "Choose Destination";

    public void Interact()
    {
        ShowChoices();
    }

    /// <summary>
    /// Called by Interactable.onInteract or Interact()
    /// </summary>
    public void ShowChoices()
    {
        if (locations == null || locations.Length == 0) return;

        // Build choice names array
        string[] names = new string[locations.Length];
        for (int i = 0; i < locations.Length; i++)
        {
            names[i] = locations[i].locationName;
        }

        // Show choice UI — UIManager handles button creation
        if (UIManager.instance != null)
        {
            UIManager.instance.ShowChoicePanel(names, OnLocationChosen);
        }
    }

    private void OnLocationChosen(int index)
    {
        if (UIManager.instance != null)
            UIManager.instance.HideChoicePanel();

        if (locations != null && index >= 0 && index < locations.Length)
        {
            StartCoroutine(TravelRoutine(locations[index].spawnPoint));
        }
    }

    private IEnumerator TravelRoutine(Transform destination)
    {
        // Fade to black
        if (UIManager.instance != null)
            yield return UIManager.instance.FadeOut(fadeOutDuration);

        // Play door open sound during black screen
        if (doorOpenSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(doorOpenSound);
        }

        // Hold black screen briefly
        yield return new WaitForSeconds(blackScreenHold);

        // Resolve player Transform
        Transform targetPlayer = player;
        if (targetPlayer == null)
        {
            var pGo = GameObject.FindWithTag("Player");
            if (pGo != null) targetPlayer = pGo.transform;
            else if (Camera.main != null) targetPlayer = Camera.main.transform;
        }

        // Resolve CharacterController
        CharacterController cc = characterController;
        if (cc == null && targetPlayer != null)
            cc = targetPlayer.GetComponent<CharacterController>();

        // Disable CharacterController to allow position change
        if (cc != null)
            cc.enabled = false;

        if (targetPlayer != null && destination != null)
        {
            targetPlayer.position = destination.position;
            targetPlayer.rotation = destination.rotation;
        }

        if (cc != null)
            cc.enabled = true;

        // Fade back in
        if (UIManager.instance != null)
            yield return UIManager.instance.FadeIn(fadeInDuration);
    }  
}
