using System.Collections;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{

    public bool CanInteract = true;

    [SerializeField] private Animator mainMenu_Anim;
    [SerializeField] private Animator Episodes_Anim;
    [SerializeField] private Animator Options_Anim;
    [SerializeField] private Animator Credits_Anim;

    [SerializeField] private GameObject mainMenu_GO;
    [SerializeField] private GameObject Episodes_GO;
    [SerializeField] private GameObject Options_GO;
    [SerializeField] private GameObject Credits_GO;

    public void EpisodesButton()
    {
        if (CanInteract == false) return;
        StartCoroutine(EpisodesButton_CO());
    }

    private IEnumerator EpisodesButton_CO()
    {
        
        CanInteract = false;
        Episodes_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        Episodes_GO.SetActive(true);
        mainMenu_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }

}
