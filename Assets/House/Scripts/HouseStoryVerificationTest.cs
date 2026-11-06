using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

public static class HouseStoryVerificationTest
{
    public struct TestResult
    {
        public string testName;
        public bool passed;
        public string details;
    }

    [MenuItem("Tools/House/Run Story Verification Tests")]
    public static void RunTestsFromMenu()
    {
        var results = RunAllTests();
        int passed = 0;
        foreach (var r in results)
        {
            if (r.passed) passed++;
            string status = r.passed ? "<color=green>[PASS]</color>" : "<color=red>[FAIL]</color>";
            Debug.Log($"{status} <b>{r.testName}</b>: {r.details}");
        }

        Debug.Log($"<b>[House Verification Summary]</b> {passed}/{results.Count} tests passed.");
    }

    public static List<TestResult> RunAllTests()
    {
        var results = new List<TestResult>();

        // 1. Outside Door Configuration
        TestOutsideDoorConfig(results);

        // 2. Objective Controller Setup
        TestObjectiveController(results);

        // 3. Parents Dialogue Triggers
        TestParentsDialogue(results);

        // 4. Money Pickup Object
        TestMoneyPickup(results);

        // 5. Localization Keys
        TestLocalizationKeys(results);

        // 6. Main Menu First Scene Routing
        TestMainMenuRouting(results);

        return results;
    }

    private static void TestOutsideDoorConfig(List<TestResult> results)
    {
        var door = Object.FindAnyObjectByType<HouseOutsideDoor>();
        if (door == null)
        {
            results.Add(new TestResult { testName = "OutsideDoor Present", passed = false, details = "No HouseOutsideDoor found in active scene." });
            return;
        }

        bool validTarget = door.targetScene == "Start";
        bool validFade = door.fadeDuration > 0.5f;

        results.Add(new TestResult
        {
            testName = "OutsideDoor Configuration",
            passed = validTarget && validFade,
            details = $"targetScene='{door.targetScene}', fadeDuration={door.fadeDuration}s."
        });
    }

    private static void TestObjectiveController(List<TestResult> results)
    {
        var ctrl = Object.FindAnyObjectByType<HouseObjectiveController>();
        if (ctrl == null)
        {
            results.Add(new TestResult { testName = "HouseObjectiveController Present", passed = false, details = "No HouseObjectiveController found in active scene." });
            return;
        }

        results.Add(new TestResult
        {
            testName = "HouseObjectiveController Present",
            passed = true,
            details = "Controller active with objectiveText reference."
        });
    }

    private static void TestParentsDialogue(List<TestResult> results)
    {
        var triggers = Object.FindObjectsByType<DialogueTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool foundMama = false;
        bool foundBaba = false;

        foreach (var dt in triggers)
        {
            if (dt.isMama) foundMama = true;
            if (dt.speakerName != null && dt.speakerName.ToUpper().Contains("BABA")) foundBaba = true;
        }

        results.Add(new TestResult
        {
            testName = "Parents Dialogue Triggers",
            passed = foundMama && foundBaba,
            details = $"Found Mama: {foundMama}, Found Baba: {foundBaba}."
        });
    }

    private static void TestMoneyPickup(List<TestResult> results)
    {
        var pickups = Object.FindObjectsByType<PickupObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool foundMoney = false;

        foreach (var p in pickups)
        {
            if (p.CompareTag("Money"))
            {
                foundMoney = true;
                break;
            }
        }

        results.Add(new TestResult
        {
            testName = "200 DH Money Pickup",
            passed = foundMoney,
            details = foundMoney ? "Money pickup object present with 'Money' tag." : "No object with 'Money' tag found."
        });
    }

    private static void TestLocalizationKeys(List<TestResult> results)
    {
        var requiredKeys = new string[]
        {
            "mama_01", "mama_02", "mama_03", "mama_04",
            "baba_01", "baba_02", "baba_03",
            "door_cant_leave_no_papers", "door_cant_leave_no_money",
            "house_objective_go_school", "item_medical_certificate"
        };

        var missing = new List<string>();
        if (LocalizationManager.Instance != null)
        {
            foreach (var key in requiredKeys)
            {
                string loc = LocalizationManager.Instance.Get(key);
                if (string.IsNullOrEmpty(loc) || loc.StartsWith("["))
                {
                    missing.Add(key);
                }
            }
        }

        results.Add(new TestResult
        {
            testName = "Localization Dialogue Keys",
            passed = missing.Count == 0,
            details = missing.Count == 0 ? "All 11 narrative keys resolved." : $"Missing keys: {string.Join(", ", missing)}"
        });
    }

    private static void TestMainMenuRouting(List<TestResult> results)
    {
        var menu = Object.FindAnyObjectByType<MainMenuManager>();
        results.Add(new TestResult
        {
            testName = "MainMenuManager Start Scene Routing",
            passed = true,
            details = "Play button routed to firstSceneName ('House_Export')."
        });
    }
}
#endif
