using UnityEngine;

public class FindableItem : MonoBehaviour
{
    public TeddyFindQuest questGiver; // assign the object holding TeddyFindQuest
    public float interactDistance = 3f;
    private bool collected = false;

    void Update()
    {
        if (collected) return;
        if (questGiver == null || !questGiver.questActive) return;

        float dist = Vector3.Distance(transform.position, Camera.main.transform.position);
        if (dist <= interactDistance && Input.GetKeyDown(KeyCode.E))
        {
            Collect();
        }
    }

    void Collect()
    {
        collected = true;
        gameObject.SetActive(false); // or play a pickup animation/sound first
        questGiver.CompleteQuest();
    }
}