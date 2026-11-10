using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class DialogueWorkflowEditorWindow : EditorWindow
{
    private const string CsvAssetPath = "Assets/Resources/Localization/dialogue.csv";

    public enum WorkflowStage
    {
        AllWorkflow = 0,
        Scene1_House = 1,
        Scene2_ClassroomIntro = 2,
        Scene3_MetroFloor1AndElevator = 3,
        Scene4_MetroFloor3AndDarkRoom = 4,
        Scene5_MoroccanBeach = 5,
        Scene6_ClassroomEnding = 6,
        OtherOrPrompts = 7
    }

    [Serializable]
    private class DialogueRow
    {
        public string key = "";
        public string en = "";
        public string fr = "";
        public string ar = "";
        public WorkflowStage stage;
        public string subSection = "";
        public int workflowOrder = 9999;
    }

    private readonly List<DialogueRow> _rows = new List<DialogueRow>();
    private Vector2 _scrollPos;
    private WorkflowStage _selectedStage = WorkflowStage.AllWorkflow;
    private string _searchQuery = "";
    private bool _filterMissingOnly = false;
    private string _speakerFilter = "All Speakers";
    private bool _isDirty = false;
    private string _statusMessage = "";
    private double _statusMessageTime = 0;

    // New key creation fields
    private bool _showAddKeyPanel = false;
    private string _newKey = "";
    private string _newEn = "";
    private string _newFr = "";
    private string _newAr = "";

    private static readonly string[] StageTabLabels = new string[]
    {
        "All Workflow (Chronological)",
        "1. House (Morning)",
        "2. Classroom Intro",
        "3. Metro Floor 1 & Elevator",
        "4. Metro Floor 3 & Dark Room",
        "5. Moroccan Beach",
        "6. Classroom Ending",
        "Other / Prompts"
    };

    private static readonly string[] SpeakerOptions = new string[]
    {
        "All Speakers",
        "Child",
        "Teddy ({TEDDY_NAME})",
        "Mama / Mother",
        "Baba / Father",
        "Teacher",
        "Old Traveler / Neighbor",
        "Shop Owner",
        "Bully",
        "Prompts / Objectives / UI"
    };

    // Exact chronological playthrough order and subsection labels
    private static readonly (string prefixOrKey, bool exact, WorkflowStage stage, string subSection, int baseOrder)[] WorkflowRules =
        new (string, bool, WorkflowStage, string, int)[]
    {
        // --- SCENE 1: HOUSE_EXPORT ---
        ("mama_doorway_wake", true, WorkflowStage.Scene1_House, "1.1 Bedroom Wake-Up Cinematic (Mama at Doorway)", 100),
        ("house_objective_wash_face", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 110),
        ("prompt_wash_face", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 111),
        ("house_need_wash_face", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 112),
        ("house_face_washed", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 113),
        ("house_objective_get_dressed", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 114),
        ("house_need_clothes_and_bag", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 115),
        ("house_clothes_worn", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 116),
        ("house_backpack_taken", true, WorkflowStage.Scene1_House, "1.2 Bathroom & Getting Ready Objectives", 117),
        ("mama_remind_wash_face", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 120),
        ("mama_remind_get_ready", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 121),
        ("house_objective_talk_mom", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 122),
        ("house_need_talk_mom", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 123),
        ("mama_house_envelope", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 124),
        ("mama_house_reminder", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 125),
        ("item_doctor_certificate", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 126),
        ("item_medical_certificate", true, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 127),
        ("mama_0", false, WorkflowStage.Scene1_House, "1.3 Talking to Mama in Kitchen", 128),
        ("baba_house_advice", true, WorkflowStage.Scene1_House, "1.4 Talking to Baba in Living Room", 130),
        ("baba_house_reminder", true, WorkflowStage.Scene1_House, "1.4 Talking to Baba in Living Room", 131),
        ("baba_", false, WorkflowStage.Scene1_House, "1.4 Talking to Baba in Living Room", 132),
        ("house_objective_go_school", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 140),
        ("door_cant_leave_", false, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 141),
        ("fridge_comment", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 142),
        ("cabinet_comment", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 143),
        ("door_locked", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 144),
        ("drawer_locked", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 145),
        ("window_locked", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 146),
        ("cupboard_locked", true, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 147),
        ("house_objective_", false, WorkflowStage.Scene1_House, "1.5 House Exploration & Leaving for School", 148),

        // --- SCENE 2: START (CLASSROOM INTRO) ---
        ("classroom_intro_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.1 Teacher Morning Lecture", 200),
        ("classroom_intro_02", true, WorkflowStage.Scene2_ClassroomIntro, "2.1 Teacher Morning Lecture", 201),
        ("classroom_intro_03", true, WorkflowStage.Scene2_ClassroomIntro, "2.1 Teacher Morning Lecture", 202),
        ("teacher_2_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.1 Teacher Morning Lecture", 203),
        ("desk_book_locked_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.2 Student Desk Item Thoughts", 210),
        ("desk_pen_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.2 Student Desk Item Thoughts", 211),
        ("desk_doodle_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.2 Student Desk Item Thoughts", 212),
        ("desk_carving_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.2 Student Desk Item Thoughts", 213),
        ("classroom_intro_04", true, WorkflowStage.Scene2_ClassroomIntro, "2.3 Opening Book to Page 54 & Transition", 220),
        ("player_intro_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.3 Opening Book to Page 54 & Transition", 221),
        ("player_intro_02", true, WorkflowStage.Scene2_ClassroomIntro, "2.3 Opening Book to Page 54 & Transition", 222),
        ("lookback_book_01", true, WorkflowStage.Scene2_ClassroomIntro, "2.3 Opening Book to Page 54 & Transition", 223),
        ("lookback_book_02", true, WorkflowStage.Scene2_ClassroomIntro, "2.3 Opening Book to Page 54 & Transition", 224),

        // --- SCENE 3: STORY (METRO FLOOR 1 & ELEVATOR) ---
        ("teddy_greet_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.1 First Meeting Teddy on Floor 1", 300),
        ("teddy_metro_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.1 First Meeting Teddy on Floor 1", 301),
        ("teddy_metro_02", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.1 First Meeting Teddy on Floor 1", 302),
        ("neighbor_0", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.2 Floor 1 NPCs (Neighbor & Shop Owner)", 310),
        ("shop_0", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.2 Floor 1 NPCs (Neighbor & Shop Owner)", 315),
        ("vending_machine_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.3 Floor 1 Station Clues & Ticket Booth", 320),
        ("intercom_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.3 Floor 1 Station Clues & Ticket Booth", 321),
        ("ticket_booth_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.3 Floor 1 Station Clues & Ticket Booth", 322),
        ("transit_pass_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.3 Floor 1 Station Clues & Ticket Booth", 325),
        ("teddy_fuse_hint_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.4 Fuse Box Puzzle & Old Traveler Advice", 330),
        ("neighbor_fuse_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.4 Fuse Box Puzzle & Old Traveler Advice", 332),
        ("begger_metro_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.4 Fuse Box Puzzle & Old Traveler Advice", 335),
        ("teddy_metro_03", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 340),
        ("teddy_metro_04", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 341),
        ("objective_search_trash", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 342),
        ("trash_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 343),
        ("teddy_trash_empty_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 345),
        ("ticket_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 346),
        ("teddy_metro_05", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 348),
        ("teddy_metro_06", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.5 Power Restored & Trash Ticket Search", 349),
        ("elevator_door_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.6 Boarding Elevator & Void Train False-Stop", 350),
        ("elevator_btn_", false, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.6 Boarding Elevator & Void Train False-Stop", 352),
        ("elevator_train_reaction_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.6 Boarding Elevator & Void Train False-Stop", 355),
        ("elevator_train_reaction_02", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.6 Boarding Elevator & Void Train False-Stop", 356),
        ("elevator_train_reaction_03", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.6 Boarding Elevator & Void Train False-Stop", 357),
        ("teddy_reaction_train_01", true, WorkflowStage.Scene3_MetroFloor1AndElevator, "3.6 Boarding Elevator & Void Train False-Stop", 358),

        // --- SCENE 4: STORY (METRO FLOOR 3, INTERROGATION & DARK ROOM) ---
        ("departure_board_01", true, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.1 Floor 3 Arrival & Clues", 400),
        ("teddy_lost_piece_01", true, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.1 Floor 3 Arrival & Clues", 401),
        ("teddy_floor3_hint_", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.1 Floor 3 Arrival & Clues", 402),
        ("bully_floor3_", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.2 Bully Confrontation on Floor 3", 410),
        ("bully_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.2 Bully Confrontation on Floor 3", 412),
        ("teddy_met_people_01", true, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.3 Teddy Turn ('Wake Up')", 420),
        ("turn_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.3 Teddy Turn ('Wake Up')", 421),
        ("cinematic_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.4 Behind the Door — Interrogation Cinematic", 430),
        ("scream_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.4 Behind the Door — Interrogation Cinematic", 435),
        ("episode_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.5 Child Panic Episode", 440),
        ("final_teddy_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.5 Child Panic Episode", 445),
        ("darkroom_0", false, WorkflowStage.Scene4_MetroFloor3AndDarkRoom, "4.6 Dark Room Reveal (Transition to Beach)", 450),

        // --- SCENE 5: MOROCCAN BEACH ---
        ("beach_intro_0", false, WorkflowStage.Scene5_MoroccanBeach, "5.1 Beach Intro (Teddy & Child by the Ocean)", 500),
        ("beach_ending_ready", true, WorkflowStage.Scene5_MoroccanBeach, "5.2 Raft Completed & Crossing Choice", 510),
        ("beach_choice_prompt", true, WorkflowStage.Scene5_MoroccanBeach, "5.2 Raft Completed & Crossing Choice", 511),
        ("beach_choice_leave", true, WorkflowStage.Scene5_MoroccanBeach, "5.2 Raft Completed & Crossing Choice", 512),
        ("beach_choice_send", true, WorkflowStage.Scene5_MoroccanBeach, "5.2 Raft Completed & Crossing Choice", 513),
        ("beach_teddy_take", true, WorkflowStage.Scene5_MoroccanBeach, "5.3 Teddy Boat Interaction Prompts", 520),
        ("beach_teddy_put", true, WorkflowStage.Scene5_MoroccanBeach, "5.3 Teddy Boat Interaction Prompts", 521),
        ("beach_teddy_pickup", true, WorkflowStage.Scene5_MoroccanBeach, "5.3 Teddy Boat Interaction Prompts", 522),
        ("beach_teddy_place", true, WorkflowStage.Scene5_MoroccanBeach, "5.3 Teddy Boat Interaction Prompts", 523),
        ("beach_farewell_path_a", true, WorkflowStage.Scene5_MoroccanBeach, "5.4 Ending Farewells (Path A: Leave / Path B: Send)", 530),
        ("beach_farewell_path_b", true, WorkflowStage.Scene5_MoroccanBeach, "5.4 Ending Farewells (Path A: Leave / Path B: Send)", 531),
        ("beach_teddy_farewell", true, WorkflowStage.Scene5_MoroccanBeach, "5.4 Ending Farewells (Path A: Leave / Path B: Send)", 532),

        // --- SCENE 6: ENDING (CLASSROOM RETURN) ---
        ("teacher_ending_01", true, WorkflowStage.Scene6_ClassroomEnding, "6.1 Classroom Awakening Ending", 600),
        ("teacher_ending_02", true, WorkflowStage.Scene6_ClassroomEnding, "6.1 Classroom Awakening Ending", 601),
    };

    [MenuItem("Tools/Dialogue & Translation Manager", false, 10)]
    [MenuItem("Window/Dialogue & Translation Manager", false, 100)]
    public static void OpenWindow()
    {
        var window = GetWindow<DialogueWorkflowEditorWindow>("Dialogue Workflow");
        window.minSize = new Vector2(920f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadFromCsv();
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusMessageTime = EditorApplication.timeSinceStartup;
        Repaint();
    }

    public void LoadFromCsv()
    {
        _rows.Clear();
        _isDirty = false;

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), CsvAssetPath);
        if (!File.Exists(fullPath))
        {
            SetStatus($"CSV file not found at {CsvAssetPath}");
            return;
        }

        string csvText = File.ReadAllText(fullPath, Encoding.UTF8);
        using (StringReader reader = new StringReader(csvText))
        {
            string headerLine = reader.ReadLine();
            if (headerLine == null) return;

            int lineIndex = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] fields = ParseCsvLine(line);
                if (fields.Length == 0) continue;

                string key = fields[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                var row = new DialogueRow
                {
                    key = key,
                    en = fields.Length > 1 ? fields[1].Trim() : "",
                    fr = fields.Length > 2 ? fields[2].Trim() : "",
                    ar = fields.Length > 3 ? fields[3].Trim() : ""
                };

                ClassifyRow(row, lineIndex);
                _rows.Add(row);
                lineIndex++;
            }
        }

        _rows.Sort((a, b) =>
        {
            int cmp = a.workflowOrder.CompareTo(b.workflowOrder);
            if (cmp != 0) return cmp;
            return string.Compare(a.key, b.key, StringComparison.Ordinal);
        });

        SetStatus($"Loaded {_rows.Count} dialogue keys across 6 workflow stages.");
    }

    public void SaveToCsv(bool sortChronologically = false)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), CsvAssetPath);

        IEnumerable<DialogueRow> rowsToWrite = _rows;
        if (sortChronologically)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                ClassifyRow(_rows[i], i);
            }
            rowsToWrite = _rows.OrderBy(r => r.workflowOrder).ThenBy(r => r.key, StringComparer.Ordinal).ToList();
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("key,en,fr,ar");

        foreach (var row in rowsToWrite)
        {
            if (string.IsNullOrWhiteSpace(row.key)) continue;
            sb.Append(EscapeCsvField(row.key.Trim())).Append(',');
            sb.Append(EscapeCsvField(row.en ?? "")).Append(',');
            sb.Append(EscapeCsvField(row.fr ?? "")).Append(',');
            sb.Append(EscapeCsvField(row.ar ?? ""));
            sb.AppendLine();
        }

        File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(CsvAssetPath, ImportAssetOptions.ForceUpdate);

        if (Application.isPlaying && LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.Reload();
        }

        _isDirty = false;
        SetStatus($"Saved {_rows.Count} dialogue & translation entries to {CsvAssetPath}.");
    }

    private static void ClassifyRow(DialogueRow row, int fallbackIndex)
    {
        for (int i = 0; i < WorkflowRules.Length; i++)
        {
            var rule = WorkflowRules[i];
            bool match = rule.exact
                ? string.Equals(row.key, rule.prefixOrKey, StringComparison.OrdinalIgnoreCase)
                : row.key.StartsWith(rule.prefixOrKey, StringComparison.OrdinalIgnoreCase);

            if (match)
            {
                row.stage = rule.stage;
                row.subSection = rule.subSection;
                row.workflowOrder = rule.baseOrder * 100 + ExtractTrailingNumber(row.key, fallbackIndex);
                return;
            }
        }

        row.stage = WorkflowStage.OtherOrPrompts;
        row.subSection = "7.1 General / Misc Keys";
        row.workflowOrder = 900000 + fallbackIndex;
    }

    private static int ExtractTrailingNumber(string key, int fallback)
    {
        int i = key.Length - 1;
        while (i >= 0 && char.IsDigit(key[i])) i--;
        if (i < key.Length - 1 && int.TryParse(key.Substring(i + 1), out int num))
        {
            return num;
        }
        return fallback % 99;
    }

    private void OnGUI()
    {
        DrawHeaderBar();
        DrawStageTabs();
        DrawFilterBar();

        if (_showAddKeyPanel)
        {
            DrawAddKeyPanel();
        }

        DrawDialogueList();
        DrawFooterBar();
    }

    private void DrawHeaderBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Playthrough Dialogue & Translation Workflow Manager", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (_isDirty)
        {
            GUIStyle dirtyStyle = new GUIStyle(EditorStyles.miniLabel);
            dirtyStyle.normal.textColor = new Color(1f, 0.65f, 0.2f);
            GUILayout.Label("● Unsaved Changes", dirtyStyle);
        }

        if (GUILayout.Button("Reload from CSV", GUILayout.Width(125f), GUILayout.Height(24f)))
        {
            if (!_isDirty || EditorUtility.DisplayDialog("Reload CSV?", "Discard unsaved changes and reload from dialogue.csv?", "Reload", "Cancel"))
            {
                LoadFromCsv();
                GUI.FocusControl(null);
            }
        }

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = _isDirty ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.75f, 0.9f, 0.75f);
        if (GUILayout.Button("Save All to CSV", GUILayout.Width(140f), GUILayout.Height(24f)))
        {
            GUI.FocusControl(null);
            SaveToCsv(sortChronologically: true);
        }
        GUI.backgroundColor = prevBg;

        if (GUILayout.Button("Ping CSV Asset", GUILayout.Width(110f), GUILayout.Height(24f)))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvAssetPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Workflow Order: 1. House (Morning)  →  2. Classroom Intro  →  3. Metro Floor 1 & Elevator  →  4. Metro Floor 3 & Dark Room  →  5. Moroccan Beach  →  6. Classroom Ending   |   Placeholders: {TEDDY_NAME}",
            EditorStyles.wordWrappedMiniLabel
        );

        EditorGUILayout.EndVertical();
    }

    private void DrawStageTabs()
    {
        EditorGUILayout.Space(2f);
        int currentTab = (int)_selectedStage;
        int newTab = GUILayout.SelectionGrid(currentTab, StageTabLabels, 4, GUILayout.Height(46f));
        if (newTab != currentTab)
        {
            _selectedStage = (WorkflowStage)newTab;
            _scrollPos = Vector2.zero;
            GUI.FocusControl(null);
        }
        EditorGUILayout.Space(2f);
    }

    private void DrawFilterBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Search:", GUILayout.Width(50f));
        string nextSearch = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField, GUILayout.MinWidth(180f));
        if (nextSearch != _searchQuery)
        {
            _searchQuery = nextSearch;
        }
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45f)))
        {
            _searchQuery = "";
            GUI.FocusControl(null);
        }

        GUILayout.Space(12f);
        GUILayout.Label("Speaker:", GUILayout.Width(55f));
        int currentSpeakerIdx = Mathf.Max(0, Array.IndexOf(SpeakerOptions, _speakerFilter));
        int nextSpeakerIdx = EditorGUILayout.Popup(currentSpeakerIdx, SpeakerOptions, EditorStyles.toolbarPopup, GUILayout.Width(165f));
        _speakerFilter = SpeakerOptions[nextSpeakerIdx];

        GUILayout.Space(12f);
        _filterMissingOnly = GUILayout.Toggle(_filterMissingOnly, "Missing Translations Only", EditorStyles.toolbarButton, GUILayout.Width(165f));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(_showAddKeyPanel ? "Hide + Add Key" : "+ Add New Dialogue Key", EditorStyles.toolbarButton, GUILayout.Width(150f)))
        {
            _showAddKeyPanel = !_showAddKeyPanel;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAddKeyPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Add New Dialogue / Translation Key", EditorStyles.boldLabel);

        _newKey = EditorGUILayout.TextField("Key ID (e.g. beach_intro_07)", _newKey);
        _newEn = EditorGUILayout.TextField("English (en)", _newEn);
        _newFr = EditorGUILayout.TextField("French (fr)", _newFr);
        _newAr = EditorGUILayout.TextField("Arabic (ar)", _newAr);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
        {
            _showAddKeyPanel = false;
        }
        if (GUILayout.Button("Add Entry", GUILayout.Width(110f)))
        {
            string trimmedKey = (_newKey ?? "").Trim();
            if (string.IsNullOrEmpty(trimmedKey))
            {
                EditorUtility.DisplayDialog("Invalid Key", "Please enter a non-empty localization key.", "OK");
            }
            else if (_rows.Any(r => string.Equals(r.key, trimmedKey, StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("Duplicate Key", $"Key '{trimmedKey}' already exists!", "OK");
            }
            else
            {
                var newRow = new DialogueRow
                {
                    key = trimmedKey,
                    en = _newEn ?? "",
                    fr = _newFr ?? "",
                    ar = _newAr ?? ""
                };
                ClassifyRow(newRow, _rows.Count);
                _rows.Add(newRow);
                _isDirty = true;
                _newKey = "";
                _newEn = "";
                _newFr = "";
                _newAr = "";
                _showAddKeyPanel = false;
                SetStatus($"Added key '{trimmedKey}'. Remember to click 'Save All to CSV'.");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawDialogueList()
    {
        var filtered = GetFilteredRows();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Showing {filtered.Count} of {_rows.Count} entries", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        string lastSubSection = null;
        int rowDeleteIdx = -1;

        for (int i = 0; i < filtered.Count; i++)
        {
            DialogueRow row = filtered[i];

            if (row.subSection != lastSubSection)
            {
                lastSubSection = row.subSection;
                EditorGUILayout.Space(6f);
                DrawSubSectionBanner(lastSubSection);
            }

            if (DrawDialogueCard(row))
            {
                rowDeleteIdx = _rows.IndexOf(row);
            }
        }

        if (rowDeleteIdx >= 0 && rowDeleteIdx < _rows.Count)
        {
            string deletedKey = _rows[rowDeleteIdx].key;
            _rows.RemoveAt(rowDeleteIdx);
            _isDirty = true;
            SetStatus($"Removed key '{deletedKey}'. Click 'Save All to CSV' to persist.");
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSubSectionBanner(string title)
    {
        GUIStyle bannerStyle = new GUIStyle(EditorStyles.toolbarButton);
        bannerStyle.alignment = TextAnchor.MiddleLeft;
        bannerStyle.fontStyle = FontStyle.Bold;
        bannerStyle.fontSize = 12;

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.38f, 0.55f);
        GUILayout.Box("  ▸ " + title, bannerStyle, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
        GUI.backgroundColor = prev;
    }

    private bool DrawDialogueCard(DialogueRow row)
    {
        bool deleteClicked = false;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Card top row: Key + detected Speaker badge + Missing warning + Delete button
        EditorGUILayout.BeginHorizontal();

        string speaker = DetectSpeakerLabel(row);
        Color badgeColor = GetSpeakerBadgeColor(speaker);
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = badgeColor;
        GUILayout.Label(speaker, EditorStyles.miniButton, GUILayout.Width(135f));
        GUI.backgroundColor = prevBg;

        EditorGUILayout.SelectableLabel(row.key, EditorStyles.boldLabel, GUILayout.Height(18f), GUILayout.MinWidth(200f));

        bool missingAny = string.IsNullOrWhiteSpace(row.en) || string.IsNullOrWhiteSpace(row.fr) || string.IsNullOrWhiteSpace(row.ar);
        if (missingAny)
        {
            GUIStyle warnStyle = new GUIStyle(EditorStyles.miniLabel);
            warnStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
            GUILayout.Label("⚠ Missing translation", warnStyle, GUILayout.Width(130f));
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Copy Key", EditorStyles.miniButton, GUILayout.Width(68f)))
        {
            EditorGUIUtility.systemCopyBuffer = row.key;
            SetStatus($"Copied key '{row.key}' to clipboard.");
        }

        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24f)))
        {
            if (EditorUtility.DisplayDialog("Delete Key?", $"Delete localization key '{row.key}'?", "Delete", "Cancel"))
            {
                deleteClicked = true;
            }
        }

        EditorGUILayout.EndHorizontal();

        // 3 Language TextAreas side-by-side or stacked cleanly
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("EN (English)", EditorStyles.miniBoldLabel, GUILayout.Width(95f));
        string newEn = EditorGUILayout.TextArea(row.en, EditorStyles.textArea, GUILayout.MinHeight(36f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("FR (Français)", EditorStyles.miniBoldLabel, GUILayout.Width(95f));
        string newFr = EditorGUILayout.TextArea(row.fr, EditorStyles.textArea, GUILayout.MinHeight(36f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("AR (العربية)", EditorStyles.miniBoldLabel, GUILayout.Width(95f));
        string newAr = EditorGUILayout.TextArea(row.ar, EditorStyles.textArea, GUILayout.MinHeight(36f));
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            row.en = newEn;
            row.fr = newFr;
            row.ar = newAr;
            _isDirty = true;
        }

        EditorGUILayout.EndVertical();
        return deleteClicked;
    }

    private void DrawFooterBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (!string.IsNullOrEmpty(_statusMessage) && EditorApplication.timeSinceStartup - _statusMessageTime < 15.0)
        {
            GUILayout.Label(_statusMessage, EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label($"CSV Path: {CsvAssetPath}", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Live Switch: EN", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                LocalizationManager.Instance?.SetLanguage("en");
            }
            if (GUILayout.Button("Live Switch: FR", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                LocalizationManager.Instance?.SetLanguage("fr");
            }
            if (GUILayout.Button("Live Switch: AR", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                LocalizationManager.Instance?.SetLanguage("ar");
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private List<DialogueRow> GetFilteredRows()
    {
        var result = new List<DialogueRow>(_rows.Count);
        string q = (_searchQuery ?? "").Trim().ToLowerInvariant();

        foreach (var row in _rows)
        {
            if (_selectedStage != WorkflowStage.AllWorkflow && row.stage != _selectedStage)
                continue;

            if (_filterMissingOnly)
            {
                bool missing = string.IsNullOrWhiteSpace(row.en) ||
                               string.IsNullOrWhiteSpace(row.fr) ||
                               string.IsNullOrWhiteSpace(row.ar);
                if (!missing) continue;
            }

            if (_speakerFilter != "All Speakers")
            {
                string detected = DetectSpeakerLabel(row);
                if (!MatchesSpeakerFilter(detected, _speakerFilter))
                    continue;
            }

            if (!string.IsNullOrEmpty(q))
            {
                bool match = (row.key != null && row.key.ToLowerInvariant().Contains(q)) ||
                             (row.en != null && row.en.ToLowerInvariant().Contains(q)) ||
                             (row.fr != null && row.fr.ToLowerInvariant().Contains(q)) ||
                             (row.ar != null && row.ar.ToLowerInvariant().Contains(q)) ||
                             (row.subSection != null && row.subSection.ToLowerInvariant().Contains(q));
                if (!match) continue;
            }

            result.Add(row);
        }

        return result;
    }

    private static string DetectSpeakerLabel(DialogueRow row)
    {
        string en = row.en ?? "";
        string key = row.key ?? "";

        if (en.StartsWith("{TEDDY_NAME}:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("teddy_", StringComparison.OrdinalIgnoreCase))
            return "Teddy ({TEDDY_NAME})";
        if (en.StartsWith("Child:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("player_intro", StringComparison.OrdinalIgnoreCase) || key.StartsWith("lookback_book", StringComparison.OrdinalIgnoreCase) || key.StartsWith("desk_", StringComparison.OrdinalIgnoreCase))
            return "Child";
        if (en.StartsWith("Mama:", StringComparison.OrdinalIgnoreCase) || en.StartsWith("Mother:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("mama_", StringComparison.OrdinalIgnoreCase))
            return "Mama / Mother";
        if (en.StartsWith("Baba:", StringComparison.OrdinalIgnoreCase) || en.StartsWith("Father:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("baba_", StringComparison.OrdinalIgnoreCase))
            return "Baba / Father";
        if (en.StartsWith("Teacher:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("classroom_intro", StringComparison.OrdinalIgnoreCase) || key.StartsWith("teacher_", StringComparison.OrdinalIgnoreCase))
            return "Teacher";
        if (en.StartsWith("Old Traveler:", StringComparison.OrdinalIgnoreCase) || en.StartsWith("Neighbor:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("neighbor_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("begger_", StringComparison.OrdinalIgnoreCase))
            return "Old Traveler / Neighbor";
        if (en.StartsWith("Shop Owner:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("shop_", StringComparison.OrdinalIgnoreCase))
            return "Shop Owner";
        if (en.StartsWith("Bully:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("bully_", StringComparison.OrdinalIgnoreCase))
            return "Bully";

        return "Prompts / Objectives / UI";
    }

    private static bool MatchesSpeakerFilter(string detected, string filter)
    {
        return string.Equals(detected, filter, StringComparison.OrdinalIgnoreCase);
    }

    private static Color GetSpeakerBadgeColor(string speaker)
    {
        switch (speaker)
        {
            case "Teddy ({TEDDY_NAME})": return new Color(1.0f, 0.72f, 0.35f);
            case "Child": return new Color(0.55f, 0.82f, 1.0f);
            case "Mama / Mother": return new Color(0.82f, 0.72f, 0.99f);
            case "Baba / Father": return new Color(0.65f, 0.85f, 0.72f);
            case "Teacher": return new Color(0.95f, 0.88f, 0.55f);
            case "Old Traveler / Neighbor": return new Color(0.65f, 0.90f, 0.85f);
            case "Shop Owner": return new Color(0.85f, 0.78f, 0.65f);
            case "Bully": return new Color(0.95f, 0.55f, 0.55f);
            default: return new Color(0.75f, 0.75f, 0.75f);
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        string cleaned = field.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        bool needsQuotes = cleaned.Contains(',') || cleaned.Contains('"');
        if (!needsQuotes) return cleaned;
        return "\"" + cleaned.Replace("\"", "\"\"") + "\"";
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            string field;
            if (line[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                field = sb.ToString();
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && line[i] != ',') i++;
                field = line.Substring(start, i - start);
                if (i < line.Length && line[i] == ',') i++;
            }
            fields.Add(field);
        }
        return fields.ToArray();
    }
}
