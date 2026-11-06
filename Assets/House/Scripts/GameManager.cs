using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int phase = 0;
    public bool hasMoney = false;
    public ObjectiveUI objectiveUI;

void Awake()
{
    instance = this;

    // في الـ editor فقط: reset مرة واحدة
    #if UNITY_EDITOR
    if (!PlayerPrefs.HasKey("gameStarted"))
    {
        PlayerPrefs.SetInt("phase", 0);
        PlayerPrefs.SetInt("gameStarted", 1);
    }
    #else
    // في الـ build: دايما ابدا من 0
    PlayerPrefs.SetInt("phase", 0);
    #endif

    phase = PlayerPrefs.GetInt("phase", 0);
    SetPhase(phase);
}
void OnApplicationQuit()
{
    PlayerPrefs.DeleteKey("gameStarted");
}

    public void SetPhase(int newPhase)
    {
        phase = newPhase;
        PlayerPrefs.SetInt("phase", phase);

        switch (phase)
        {
            case 0: SetupPhase0(); break;
            case 1: SetupPhase1(); break;
            case 2: SetupPhase2(); break;
        }
    }

    void SetupPhase0()
    {
        if (objectiveUI != null)
            objectiveUI.SetObjective("tklm");
    }

    void SetupPhase1()
    {
        if (objectiveUI != null)
            objectiveUI.SetObjective("sir l7anot");
    }

    void SetupPhase2()
    {
        if (objectiveUI != null)
            objectiveUI.SetObjective("rj3 ldar");
    }

    public void OnMamaTalked()
    {
        if (phase != 0) return;
        SetPhase(1);
    }

    public void OnStoreComplete()
    {
        SetPhase(2);
    }
}