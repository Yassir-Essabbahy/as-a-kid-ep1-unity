using UnityEngine;

public class MenuButtonUI : MonoBehaviour
{


    public void ActiveUI(GameObject CubeUI_GO)
    {
        if (CubeUI_GO.activeSelf == true) return;
        CubeUI_GO.SetActive(true);
    }

    public void DeactiveUI(GameObject CubeUI_GO)
    {
        if (CubeUI_GO.activeSelf == false) return;
        CubeUI_GO.SetActive(false);
    }
}
