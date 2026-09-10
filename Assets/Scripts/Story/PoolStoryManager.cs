using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PoolStoryManager : MonoBehaviour
{
    private static PoolStoryManager _instance;
    public static PoolStoryManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<PoolStoryManager>();
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Memory Items")]
    public PoolMemoryItem pantsItem;
    public PoolMemoryItem bagItem;
    public PoolMemoryItem shoesItem;
    public PoolMemoryItem towelItem;
    public PoolMemoryItem phoneItem;

    [Header("Pool Environment Roots")]
    public GameObject poolEnvironmentRoot;
    public Transform playerSpawnPoint;
    public PoolWaterTrigger waterTrigger;

    [Header("State")]
    public PoolMemoryItem CurrentlyCarriedItem { get; private set; }
    public int itemsThrownCount = 0;
    public bool isPoolSequenceComplete = false;

    [Header("UI & Dependencies")]
    public TextMeshProUGUI objectiveText;
    public ScreenFader screenFader;
    public FirstPersonController fpsController;

    private bool sequenceBusy = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (fpsController == null)
            fpsController = FindAnyObjectByType<FirstPersonController>();

        if (screenFader == null)
            screenFader = FindAnyObjectByType<ScreenFader>();

        if (objectiveText == null)
        {
            var metro = MetroStorySequenceController.Instance;
            if (metro != null) objectiveText = metro.objectiveText;
        }

        if (pantsItem == null)
        {
            var p = GameObject.Find("PoolMemory_Pants");
            if (p != null) pantsItem = p.GetComponent<PoolMemoryItem>();
        }
        if (bagItem == null)
        {
            var b = GameObject.Find("PoolMemory_Bag");
            if (b != null) bagItem = b.GetComponent<PoolMemoryItem>();
        }
        if (shoesItem == null)
        {
            var s = GameObject.Find("PoolMemory_Shoes");
            if (s != null) shoesItem = s.GetComponent<PoolMemoryItem>();
        }
        if (towelItem == null)
        {
            var t = GameObject.Find("PoolMemory_Towel");
            if (t != null) towelItem = t.GetComponent<PoolMemoryItem>();
        }
        if (phoneItem == null)
        {
            var ph = GameObject.Find("PoolMemory_Phone");
            if (ph != null) phoneItem = ph.GetComponent<PoolMemoryItem>();
        }
    }

    public void StartPoolSequence()
    {
        ResolveReferences();
        if (poolEnvironmentRoot != null) poolEnvironmentRoot.SetActive(true);

        if (fpsController != null && playerSpawnPoint != null)
        {
            fpsController.transform.position = playerSpawnPoint.position;
            fpsController.transform.rotation = playerSpawnPoint.rotation;
            fpsController.SnapViewRotation(playerSpawnPoint.rotation);
            fpsController.SetControlLocked(false);
        }

        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "Explore the pool area. Find the memories left behind.";
        }

        StartCoroutine(PlayPoolIntro());
    }

    private IEnumerator PlayPoolIntro()
    {
        yield return new WaitForSeconds(1.0f);
        string intro = MetroStorySequenceController.GetLoc("pool_intro_01");
        var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
        if (diag != null && Application.isPlaying)
        {
            yield return StartCoroutine(diag.ShowDialogue(
                new string[] { intro },
                hasChoice: false,
                dialogueColor: new Color(0.9f, 0.95f, 1f),
                dialogueFont: null
            ));
        }
    }

    public void OnItemPickedUp(PoolMemoryItem item)
    {
        CurrentlyCarriedItem = item;
        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = $"Take {item.displayName} to the edge of the pool.";
        }
    }

    public void ThrowCurrentItemIntoPool(Vector3 poolCenter)
    {
        if (CurrentlyCarriedItem == null || sequenceBusy) return;

        var item = CurrentlyCarriedItem;
        CurrentlyCarriedItem = null;
        itemsThrownCount++;

        item.ThrowIntoPool(poolCenter);
        StartCoroutine(HandleItemThrownRoutine(item));
    }

    private IEnumerator HandleItemThrownRoutine(PoolMemoryItem item)
    {
        sequenceBusy = true;

        yield return new WaitForSeconds(1.0f);

        string[] throwLines = null;
        switch (itemsThrownCount)
        {
            case 1:
                throwLines = new string[] {
                    MetroStorySequenceController.GetLoc("pool_throw_01"),
                    MetroStorySequenceController.GetLoc("pool_throw_02"),
                    MetroStorySequenceController.GetLoc("pool_throw_03")
                };
                break;
            case 2:
                throwLines = new string[] {
                    MetroStorySequenceController.GetLoc("pool_throw_04")
                };
                break;
            case 3:
                throwLines = new string[] {
                    MetroStorySequenceController.GetLoc("pool_throw_05")
                };
                break;
            case 4:
                throwLines = new string[] {
                    MetroStorySequenceController.GetLoc("pool_throw_06")
                };
                break;
            case 5:
                throwLines = new string[] {
                    MetroStorySequenceController.GetLoc("pool_throw_final_01")
                };
                break;
        }

        if (throwLines != null && throwLines.Length > 0)
        {
            var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
            if (diag != null && Application.isPlaying)
            {
                yield return StartCoroutine(diag.ShowDialogue(
                    throwLines,
                    hasChoice: false,
                    dialogueColor: new Color(0.9f, 0.95f, 1f),
                    dialogueFont: null
                ));
            }
        }

        if (itemsThrownCount < 5)
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                if (itemsThrownCount == 4)
                    objectiveText.text = "Find his brother's phone.";
                else
                    objectiveText.text = $"Find another memory object ({itemsThrownCount}/5 completed).";
            }
            sequenceBusy = false;
        }
        else
        {
            // Final item thrown!
            isPoolSequenceComplete = true;
            if (objectiveText != null) objectiveText.gameObject.SetActive(false);

            if (fpsController != null) fpsController.SetControlLocked(true);

            // Silence for emotional impact
            yield return new WaitForSeconds(2.5f);

            // Transition to Classroom ending
            TriggerClassroomTransition();
        }
    }

    public void TriggerClassroomTransition()
    {
        Debug.Log("[PoolStoryManager] All 5 memories thrown into pool. Transitioning to Classroom Scene...");
        if (screenFader != null)
        {
            screenFader.FadeToBlack(2.0f, () => {
                ClassroomEndingController.IsEndingActive = true;
                SceneManager.LoadScene("S1");
            });
        }
        else
        {
            ClassroomEndingController.IsEndingActive = true;
            SceneManager.LoadScene("S1");
        }
    }
}
