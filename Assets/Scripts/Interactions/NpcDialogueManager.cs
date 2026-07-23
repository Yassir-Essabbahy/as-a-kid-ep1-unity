using System.Collections;
using TMPro;
using UnityEngine;

public class NpcDialogueManager : MonoBehaviour
{
    public static NpcDialogueManager Instance;

    public GameObject talkPanel;   // The main dialogue box
    public GameObject choicePack;  // The container for buttons
    public TextMeshProUGUI dialogueText;
    public int lastChoiceIndex;

    private bool choiceMade = false;

    void Awake()
    {
        Instance = this;
        talkPanel.SetActive(false);
        choicePack.SetActive(false);
    }

    public IEnumerator ShowDialogue(string[] lines, bool hasChoice)
    {
        talkPanel.SetActive(true);

        foreach (string line in lines)
        {
            yield return StartCoroutine(TypeLine(line));
            yield return WaitForInput();
        }

        if (hasChoice)
        {
            yield return StartCoroutine(HandleChoices());
        }

        talkPanel.SetActive(false);
    }

    IEnumerator TypeLine(string line)
    {
        dialogueText.text = "";
        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(0.04f);
        }
    }

    IEnumerator WaitForInput()
    {
        while (!Input.GetMouseButtonDown(0)) yield return null;
    }

    IEnumerator HandleChoices()
    {
        choicePack.SetActive(true);
        choiceMade = false;

        while (!choiceMade)
        {
            yield return null;
        }

        choicePack.SetActive(false);
    }

    public void MakeChoice()
    {
        choiceMade = true;
        Debug.Log("Choice Selected!");
    }

    public void MakeChoice(int index)
    {
        lastChoiceIndex = index;
        choiceMade = true;
    }
}