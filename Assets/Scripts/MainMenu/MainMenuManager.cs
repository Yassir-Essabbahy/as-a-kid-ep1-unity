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

    [SerializeField] private GameObject Vcam_01;
    [SerializeField] private GameObject Vcam_02;
    [SerializeField] private GameObject Vcam_03;
    [SerializeField] private GameObject Vcam_04;




    #region MainMenuButtons
    public void EpisodesButton()
    {
        if (CanInteract == false) return;
        StartCoroutine(EpisodesButton_CO());
    }

    private IEnumerator EpisodesButton_CO()
    {
        Vcam_01.SetActive(false);
        Vcam_02.SetActive(true);
        
        CanInteract = false;
        Episodes_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        Episodes_GO.SetActive(true);
        mainMenu_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }


        public void OptionsButton()
    {
        if (CanInteract == false) return;
        StartCoroutine(OptionsButton_CO());
    }

    private IEnumerator OptionsButton_CO()
    {
        Vcam_01.SetActive(false);
        Vcam_03.SetActive(true);
        CanInteract = false;
        Options_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        Options_GO.SetActive(true);
        mainMenu_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }

        public void CreditsButton()
    {
        if (CanInteract == false) return;
        StartCoroutine(CreditsButton_CO());
    }

    private IEnumerator CreditsButton_CO()
    {
        Vcam_01.SetActive(false);
        Vcam_04.SetActive(true);
        CanInteract = false;
        Credits_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        Credits_GO.SetActive(true);
        mainMenu_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }

    public void ExitGame()
    {
        Application.Quit();
    }
    #endregion



    #region EpisodesMenu

    public void BackButton_Episodes()
    {
        if (CanInteract == false) return;
        StartCoroutine(BackButton_Episodes_CO());
    }

    private IEnumerator BackButton_Episodes_CO()
    {
        Vcam_02.SetActive(false);
        Vcam_01.SetActive(true);
        CanInteract = false;
        Episodes_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        mainMenu_GO.SetActive(true);
        Episodes_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }

    #endregion



    #region OptionsMenu

    public void BackButton_Options()
    {
        if (CanInteract == false) return;
        StartCoroutine(BackButton_Options_CO());
    }

    private IEnumerator BackButton_Options_CO()
    {
        Vcam_03.SetActive(false);
        Vcam_01.SetActive(true);
        CanInteract = false;
        Options_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        mainMenu_GO.SetActive(true);
        Options_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }

    #endregion



    #region CreditsMenu

    public void BackButton_Credits()
    {
        if (CanInteract == false) return;
        StartCoroutine(BackButton_Credits_CO());
    }

    private IEnumerator BackButton_Credits_CO()
    {
        Vcam_04.SetActive(false);
        Vcam_01.SetActive(true);
        CanInteract = false;
        Credits_Anim.SetInteger("C", 1);
        yield return new WaitForSeconds(0.4f);
        mainMenu_GO.SetActive(true);
        Credits_GO.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        CanInteract = true;
    }

    #endregion
}