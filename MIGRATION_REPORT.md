# House Scene Migration Report

## 1. Executive Summary
The house scene (`House_Export.unitypackage`) has been successfully migrated, cleaned, restyled, and integrated into `as-a-kid-ep1-unity` (Unity 6 URP, PC + Android). The scene now serves as the opening chapter of Episode 1, seamlessly handing off into the classroom scene (`Start.unity`).

* **Branch**: `migrate-house-scene`
* **Scene Path**: `Assets/House/Scenes/House_Export.unity`
* **Target Build Platforms**: PC & Android
* **Key Commits**:
  - `32de515`: Initial migration, asset import, and mobile player rig setup.
  - `994617d`: Fix black wall rendering artifacts (camera culling mask & realtime point lights).
  - `17ba916`: Standardize interaction prompts to `"tap to interact"` and `"tap to talk"`, suppress legacy white text.
  - `87a5633`: Style all interactable messages with widescreen cinematic letterbox borders (`Canvas/TalkPanel`).
  - `1c9a405`: Implement morning narrative loop (Doctor's Certificate, 200 DH, and school transition).
  - `3b3b68a`: Connect Main Menu play button to `House_Export` as the Episode 1 starting scene.

---

## 2. Items Stripped & Deleted
In accordance with migration guidelines:
1. **Plane.007 (`DoorToStore`)**: Removed along with its trigger colliders, references to `StoreWay`, and leftover scenes.
2. **Staircase Geometry & Dead Area (`RoofDoor`, `Plane.009`)**: Removed stair mesh, steps colliders, and NavMesh/trigger leftovers. Dead doors were cleanly patched/disabled to prevent gaps in walls or unreachable holes.
3. **Legacy Player Rig & Cameras**: Old FPS controller, standalone Cinemachine rigs, and old `EventSystem` were removed and replaced with the project's standard mobile-ready `FirstPersonController` rig.
4. **Duplicate Managers & Canvases**: Removed old pause canvases, timescale overrides, standalone audio managers, and old fade canvas in favor of the project's `ScreenFader`, `NpcDialogueManager`, `MobilePauseMenu`, and `SoundManager`.

---

## 3. Interactables & Interaction Port
All 16 interactables were mapped to the project's unified `IInteractable.Interact()` contract, supporting both PC (`[E]`, mouse click) and Android mobile on-screen touch controls:

| Object Name | Interaction Behavior | Script Entry Point | Prompt Displayed |
| :--- | :--- | :--- | :--- |
| **Mama** | Starts morning dialogue; gives medical certificate for school | `DialogueTrigger.Interact()` | `"tap to talk"` |
| **BABAK** | Morning conversation & choice | `DialogueTrigger.Interact()` | `"tap to talk"` |
| **200dhs** | Kitchen drawer money pickup; triggers pickup toast & audio | `PickupObject.Interact()` | `"tap to interact"` |
| **DoorHinche (Locked Door)** | Displays locked feedback monologues | `DoorController.Interact()` | `"tap to interact"` |
| **OutsideDoor** | Checks objectives; fades out to `Start.unity` | `HouseOutsideDoor.Interact()` | `"Leave for School"` / `"Front Door"` |
| **FridgeDoor** | Comments on empty fridge | `MessageTrigger.Interact()` | `"tap to interact"` |
| **CabinetDoor** | Comments on hiding spot | `CabinetDoorController.Interact()` | `"tap to interact"` |
| **Drawers** | Drawer slide open/close / locked feedback | `DrawerController.Interact()` | `"tap to interact"` |
| **Window** | Window locked feedback | `WindowController.Interact()` | `"tap to interact"` |
| **Radio** | Toggles radio audio track | `Radio.Interact()` | `"tap to interact"` |

---

## 4. UI Restyling & Cinematic Letterbox Presentation
All legacy UI elements were replaced with the project's standardized UI systems:
* **Prompt HUD**:
  * Managed by `NpcInteractionText` / `Canvas/PromptBox`.
  * Context-aware text: `"tap to interact"` for props, `"tap to talk"` for NPCs.
  * All legacy floating white `"press e"` world/screen prompts were removed.
* **Cinematic Letterbox Monologues**:
  * Routed through `HouseUIBridge.ShowMessage(...)` $\rightarrow$ `NpcDialogueManager.ShowInteractableMessage(...)`.
  * Top band (`TopBand`, 140px) and bottom band (`BottomBand`, 220px) smoothly slide in to create widescreen borders.
  * Text typed using project pixel font: `GeistPixel-Regular-VariableFont_ELSH SDF`.
  * Auto-dismisses after duration or immediately upon tap/click/`[E]`/Space.
* **Localization**:
  * Full English, French, and Darija translations in `Assets/Resources/Localization/dialogue.csv`.
  * Resolves keys: `door_locked`, `fridge_comment`, `cabinet_comment`, `drawer_locked`, `window_locked`, `cupboard_locked`, `door_cant_leave_no_papers`, `door_cant_leave_no_money`, `house_objective_go_school`.

---

## 5. Narrative & Scene Flow
The game flow for Episode 1 now flows seamlessly:
1. **Main Menu (`MainMenu.unity`)**: Player clicks "Play".
2. **House Morning (`House_Export.unity`)**:
   - Objective 1: Talk to Mama $\rightarrow$ She announces the doctor granted the medical certificate to return to school.
   - Objective 2: Pick up money from kitchen drawer.
   - Objective 3: Front door unlocks with prompt `"Leave for School"`.
3. **School Classroom (`Start.unity`)**:
   - `HouseOutsideDoor` fades out with `ScreenFader.TransitionToScene("Start", 1.5f)`.
   - Player wakes up seated at the classroom desk as the teacher lectures (*"Can you open to page 54?"*).
4. **Metro Station (`Story.unity`)**:
   - Inspection of book triggers psychological dream descent into the metro with Mistfox.
5. **Beach (`MoroccanBeach.unity`)** $\rightarrow$ **Ending (`Ending.unity`)** $\rightarrow$ **Credits (`Credits.unity`)**.

---

## 6. Verification Checklist
- [x] Zero compilation errors across `Assembly-CSharp`.
- [x] Verified wall rendering in Play mode (no black wall artifacts).
- [x] Verified interaction prompts (`"tap to interact"` & `"tap to talk"`).
- [x] Verified cinematic letterbox slide-in and typewriter on interactables.
- [x] Verified door locked gates until Mama & money objectives are complete.
- [x] Verified scene transition into `Start.unity`.
- [x] All commits pushed to `origin/migrate-house-scene`.
