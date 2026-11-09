# Truth or Dare System Removal Specification & Audit

## 1. System Inventory & Component Mapping

### Trigger
- **Script:** `Assets/Scripts/Story/MetroStorySequenceController.cs`
- **Method:** `CheckFloor3Progress()` / `AutoTriggerTeddyRoutine()` triggered upon `HandleBullyFinished()` -> `OnNpcSpoken(2)`.
- **Dialogue Prompt:** In `TruthOrDareSequence()`, Teddy asks `teddy_met_people_01` ("They are mean... but they don't know you like I do.") and `teddy_met_people_02` ("How about we play our game? Truth or Dare?").
- **Choice UI:** `NpcDialogueManager.Instance.SetChoiceLabels("TRUTH", "DARE")` presented through `ChoicePack` buttons.

### Mini-Game Components
- **Scripts:**
  - `Assets/Scripts/Story/PaperInspectController.cs`: Generates the runtime paper inspect Canvas (`PaperInspect_Canvas`), drawing texture buffer ($620 \times 820$), pencil drawing mechanics, and Clear/Done buttons.
  - `Assets/Scripts/Story/Floor3PaperInteractable.cs`: 3D world interaction trigger for the paper sitting on `Floor3_Table_Back`.
  - `Assets/Scripts/Story/PlatformEndTrigger.cs`: Trigger volume for the obsolete platform-end walking dare.
- **Scene Objects (in `Assets/Scenes/Story.unity`):**
  - `Floor3_Table_Back` (GameObject ID: 1795000001): 3D table mesh in Floor 3.
  - `Floor3_Paper_Interactable` (GameObject ID: 1795000011): 3D paper mesh and interactable collider on the table.
  - `PaperInspectSystem` / `PaperInspect_Canvas` (GameObject ID: 711183734 & 1280034464): Fullscreen drawing/inspect UI Canvas.
  - `PlatformEndTrigger` (GameObject ID: 1425081818): Obsolete trigger in Floor 3.
- **Story Controller Phases (in `MetroStorySequenceController.cs`):**
  - `StoryPhase.Floor3_TruthOrDare`
  - `StoryPhase.Floor3_TruthResolution`
  - `StoryPhase.Floor3_DareObjective`
- **Localization Keys (in `Assets/Resources/Localization/dialogue.csv`):**
  - `teddy_met_people_02` ("How about we play our game? Truth or Dare?")
  - `truth_01` (Teddy Truth prompt)
  - `truth_02` (Medical discharge report text)
  - `truth_03` (Child reaction to report)
  - `truth_04` (Teddy comfort dialogue)
  - `truth_complete_01` (Truth conclusion)
  - `dare_01` (Teddy Dare prompt)
  - `dare_02` (Math problem challenge)
  - `dare_solving_01` (Child solving math)
  - `dare_complete_01` (Teddy dare congratulation)

### Destination & Exit
- **Destination:** Table `Floor3_Table_Back` on Floor 3 with `Floor3_Paper_Interactable`.
- **Exit Target:** `PaperCompletedRoutine()` exited to `TeddyTurnRoutine()`.
- `TeddyTurnRoutine()` initiates lines `turn_01`..`turn_06`, triggers the heavy door knocking, activates `doorTrigger`, and transitions to `DoorWaiting` leading to the parents/teacher interrogation sequence.

### Consumers & Readers of the Result
- **Result Flags:** `paperObjectiveCompleted`, `dareObjectiveCompleted`, `isTruthOrDareStarted`.
- **Downstream Verification:** Searched entire codebase (`House`, `MoroccanBeach`, `Story`, `Ending`, `Credits`, `SaveSystem`).
  - No subsequent scene, script, or dialogue branch checks whether the player chose Truth or Dare, or whether the paper was solved.
  - The psychological themes (the accident, memory trauma, academic decline, and isolation) are revealed with greater emotional depth and cinematic staging during the interrogation scene (`BehindDoorCinematicRoutine`).

---

## 2. New Story Flow Architecture

1. **Bully Conversation Ends:** The player finishes speaking with the Floor 3 bully (`HandleBullyFinished` / `OnNpcSpoken(2)`).
2. **Direct Teddy Comfort & Waking Sequence:**
   - Teddy delivers comfort line `teddy_met_people_01`: *"They are mean... but they don't know you like I do."*
   - Teddy directly begins the waking sequence (`TeddyTurnRoutine`):
     - `turn_02`: *"{TEDDY_NAME}: Wake up."*
     - `turn_03`: *"Child: What?"*
     - `turn_04`: *"{TEDDY_NAME}: Wake up."*
     - `turn_05`: *"Child: But I am awake."*
     - `turn_06`: *"{TEDDY_NAME}: No."*
3. **Door Knocking & Interrogation Room:**
   - Heavy door knocking sounds begin immediately.
   - Objective updates to: *"Behind the door..."*
   - Player approaches and interacts with the door, triggering the full cinematic confrontation between the teacher and parents.
4. **All Truth/Dare and Paper Objects Removed:**
   - No truth/dare prompt, choice menu, or table paper objective.
   - Flow is continuous, atmospheric, and eliminates the pacing lull of the paper mini-game.
