# PSX Backrooms — Devlog

### Project Overview

A PSX-style horror game set in a school classroom. The game opens with a fully scripted cinematic sequence driven by a dialogue system before handing control to the player.

---

### Scene: S1 (Classroom)

#### Cinematic Flow

The scene runs a linear sequence of camera cuts and dialogue, fully automated through `ClassroomGazeScene.cs`. The player has no control during this phase.

```
[START]
TeacherVCam active — Teacher talks (Teacher_Intro)
    │
    │  eventId: "look_at_book" on last line
    ▼
BookVCam activates — Cinemachine blend begins, book rotates open
    │
    │  OnBookBlendFinished() fires (wired via CinemachineCameraEvents)
    ▼
Player_Intro plays (entityLines) — Player internal monologue, 2 lines
    │  eventId: "look_at_teacher" on last line
    │  → TeacherVCam instant cut + CinemachineImpulse shake
    │
    │  entityLines sequence completes
    ▼
Teacher_2 plays — Teacher responds, 1 line
    │
    │  teacher2Lines sequence completes
    ▼
BookVCam reactivates — camera cuts back to book
Player_2 plays — Player reacts to book, 2 lines
    │
    │  player2Lines sequence completes
    ▼
BookZoomVCam activates — snaps to BookZoomPoint, blends in (zoom into book picture)
    │
    │  OnBookZoomBlendFinished() fires
    ▼
onZoomComplete UnityEvent — scene transition (to be wired in Inspector)
[END OF CINEMATIC]
```

---

### Dialogue System

All dialogue runs through a decoupled manager + event architecture.

**Assets** (`Assets/Scripts/Data/`):

| File | Sequence ID | Speaker | Lines |
|---|---|---|---|
| `Teacher_Intro.asset` | TeacherIntro | Teacher | 4 |
| `Player_Intro.asset` | EntityLines | Player | 2 |
| `Teacher_2.asset` | Teacher2 | Teacher | 1 |
| `Player_2.asset` | Player2 | Player | 2 |

**Localization** (`Assets/Resources/Localization/dialogue.csv`):
- Supported languages: `en`, `fr`, `ar`
- Keys: `classroom_intro_01–04`, `player_intro_01–02`, `teacher_2_01`, `lookback_book_01–02`

**Key scripts** (`Assets/Scripts/Dialogue/`):

- `DialogueManager.cs` — singleton, drives the UI (`DialogueBox`, `SpeakerText`, `BodyText`). Call `PlaySequence(sequence, showFade)` to start any conversation.
- `DialogueInputAdvancer.cs` — listens for `Space`. Input is locked for 3 seconds after each new line is shown (`OnLineShown` event).
- `DialogueEvents.cs` — static event bus: `OnSequenceStart`, `OnLineShown`, `OnLineEvent`, `OnSequenceComplete`.
- `DialogueFadeScreen.cs` — full-screen black Image on the Canvas that fades out (1s) when `OnSequenceStart` fires. Only triggers on sequences where `showFade: true` (default).
- `LocalizationManager.cs` — resolves localization keys to display text.

**Dialogue UI** (Canvas hierarchy):
```
Canvas
├── DialogueBox
│   ├── SpeakerText
│   └── BodyText
└── FadeOverlay      ← DialogueFadeScreen component, always active, alpha 0 at rest
```

---

### Cameras

All cameras are Cinemachine VCams managed by `ClassroomGazeScene.cs`.

| GameObject | Purpose | Default state |
|---|---|---|
| `TeacherVCam` | Frames the teacher | Active at scene start |
| `BookVCam` | Frames the open book | Inactive |
| `BookZoomVCam` | Tight zoom into book picture (FOV 12°) | Inactive |

- `BookZoomVCam` snaps to `BookZoomPoint` (empty GameObject) before activating — move `BookZoomPoint` in the Scene view to reframe the zoom target.
- `BookVCam` has a `CinemachineCameraEvents` component wired to `OnBookBlendFinished()`. A one-shot flag (`_entityLinesStarted`) prevents this from re-triggering on later book camera switches.
- `BookZoomVCam` has a `CinemachineCameraEvents` component wired to `OnBookZoomBlendFinished()`, which fires `onZoomComplete`.

---

### Inspector Checklist (ClassroomGazeScene)

- **Cameras**: `TeacherVCam`, `BookVCam`, `BookZoomVCam`, `CatchImpulse`
- **Player Handoff**: `PlayerRig`, `PlayerHandoffPoint`
- **Dialogue**: `Teacher Intro Lines`, `Entity Lines`, `Teacher 2 Lines`, `Player 2 Lines`
- **Book**: `Book Transform`, `Start Angle X`, `End Angle X`, `Open Duration`
- **Zoom Transition**: `Book Zoom Point` (position in Scene view), `On Zoom Complete` (wire scene transition here)

---

### Pending / Next Steps

- Wire `On Zoom Complete` to the next scene or transition effect
- Fill in `fr` and `ar` translations for `teacher_2_01`, `lookback_book_01`, `lookback_book_02` in `dialogue.csv`
- Position `BookZoomPoint` to frame the target picture in the book
- Add voice clips to `DialogueLine` assets if needed


---

### 2025-02-14 — NPC Dialogue Setup in TestScene

- Added the NPC dialogue UI under `Canvas`: inactive `TalkPanel` with `DialogueText`, inactive `ChoicePack`, and three choice buttons wired to `NpcDialogueManager.MakeChoice(int)`.
- Created `NpcDialogueManager` and assigned the dialogue panel, choice container, and text field.
- Added `NpcInteractionText` to `FirstPersonController` with a HUD interaction prompt.
- Created `PlayerVcam` and priority-0 `TalkZoomVcam` for dialogue camera switching.
- Created `LocalizationManager` configured to load `Resources/Localization/dialogue.csv`.
- Added the `InteractNPC` tag.
- Added placeholder `NPC_Box_01` and `NPC_Box_02` near the player spawn. Each has a humanoid-sized cube shape, a `BoxCollider`, `NpcConversation` with empty `lineKeys`, `NpcLookAt`, and an empty `Animator` with no controller assigned.


---

### 2025-02-14 — NPC Dialogue Presentation Updates

- Kept the cursor locked and hidden throughout `NpcInteractionText` talk sequences.
- Added per-NPC `dialogueColor` and optional `dialogueFont` fields to `NpcConversation`.
- Updated `NpcDialogueManager.ShowDialogue` to apply each conversation's color and font before typing begins.
- Updated `NpcConversation.Play()` to pass its presentation settings to the dialogue manager.
- Preserved the existing two-parameter `ShowDialogue` overload for compatibility with other callers.


---

### 2025-02-14 — Lock Player Control During NPC Dialogue

- Updated `NpcInteractionText` to disable the `FirstPersonController` component when a talk sequence starts, preventing movement and camera look input during dialogue.
- Re-enabled the player controller when the dialogue and camera reset complete.
- Added a public `playerController` reference with an automatic same-object fallback for `FirstPersonController`.


---

### 2025-02-14 — Teddy Bear Carry Setup

- Created `CarryPoint` under `FirstPersonController/Joint/PlayerCamera` at local position `(0.3, -0.2, 0.5)` for held-item positioning.
- Prepared the scene for the carry-after-dialogue teddy bear configuration.
- No teddy bear object was found in `TestScene.unity`, so `Rigidbody`, collider, `CarryableItem`, and `NpcConversation` carry settings could not be assigned. Expected object path: `/TeddyBear` (or another clearly named teddy bear GameObject) once it is added to the scene.


---

### 2025-02-15 — Teddy Bear Static Carry Fix

- Rewrote `CarryableItem.cs` to hard-lock position and rotation to the carry point every `LateUpdate`, removing the previous physics/lerp-based follow behavior.
- Fixed a rotation snapping/tumbling bug on pickup caused by interpolating from the object's original scene rotation toward the target instead of snapping instantly.
- `StartCarrying` now snaps the object to the carry point immediately with no smoothing.


---

### 2025-02-15 — Teddy Bear Dialogue Trigger

- Added `CarriedItemSpeaker.cs`: lets a carried item trigger its own `NpcConversation` while being held, either via key press (default `F`) or via a world trigger zone.
- Added `DialogueTriggerZone.cs`: a standalone trigger volume that fires an assigned `NpcConversation.Play()` when the player enters it, with an optional trigger-once flag.
- Set up a second, independent `NpcConversation` component on a child object (`BearMetroLine`) separate from the teddy bear's original greet dialogue, so the metro trigger zone plays distinct lines from `dialogue.csv` instead of replaying the initial greet dialogue.
- Confirmed both conversations resolve their lines through `LocalizationManager.Instance.Get(key)`, keeping all teddy bear dialogue CSV-driven rather than hardcoded.