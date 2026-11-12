# As A Kid — Episode 1 Devlog

### Project Overview

A PSX-style first-person psychological narrative game (**Unity 6 `6000.4.4f1`**, **URP `17.4.0`**, **PC & Android**). Episode 1 spans `MainMenu` → `House_Export` → `Start` (Classroom) → `Story` (Underground Metro & Interrogation Room) → `MoroccanBeach` → `Ending` → `Credits` (see [`STORY.md`](../STORY.md)).

*(The early `Scene: S1 (Classroom)` and `2025` entries below document the original prototype setup prior to Episode 1 expansion.)*

---

### Scene: S1 (Classroom — Early Prototype Notes)

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

### Early S1 Checklist Status (Superseded by Later Milestones)

- **Scene Transition**: `ClassroomGazeScene.cs` now handles seated desk clue inspection, opens Page 54, and transitions smoothly via `ScreenFader` into `Story.unity`.
- **Localization**: Expanded `Assets/Resources/Localization/dialogue.csv` to 270 keys across `en`, `fr`, `ar`, and `darija`.
- **Teacher Voice Clips**: Wired via `ClassroomGazeScene` / `TrafehVoiceLines`.


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


---

### 2025-02-16 — Trigger Safety: Require Carried Item

- Added a `requiredCarriedItem` field to `DialogueTriggerZone.cs`, referencing a `CarryableItem`.
- The trigger now only fires its conversation if the required item's `IsBeingCarried` is true at the moment the player enters the zone, preventing metro/story beats from firing if the teddy bear was left behind.
- Field is optional — leaving it empty preserves old behavior for trigger zones that don't need the check.


---

### 2025-02-16 — Teddy Bear Find-Item Quest

- Added an `OnConversationFinished` event to `NpcConversation.cs`, invoked at the end of `Play()`. No existing dialogue calls are affected; the event is opt-in and only used where explicitly subscribed.
- Added `TeddyFindQuest.cs`: listens for a designated "ask" `NpcConversation` to finish, then sets `questActive = true`. Exposes `CompleteQuest()`, which plays a "found it" `NpcConversation` and sets `questCompleted = true`.
- Added `FindableItem.cs`: placed on the item the player must locate. While `questGiver.questActive` is true, pressing `E` within `interactDistance` collects the item, disables it, and calls `CompleteQuest()`.
- Removed the earlier `TeddyQuestManager.cs` singleton approach in favor of `TeddyFindQuest`, which hooks into the existing dialogue flow instead of introducing a parallel one.
- Wired the same `NpcConversation` component used by the existing dialogue trigger as `TeddyFindQuest.askConversation`, so quest state updates automatically when that specific conversation finishes.



---

### 2026-07-29 — Beggar NPC Interaction (Reel Feature)

- Created `BeggarInteraction.cs`: a proximity-trigger sequence for a floor-bound "beggar" NPC — optional pre-dialogue sound (cough/mumble), then a camera look-at beat, then `NpcConversation.Play()`.
- Initial version used a dedicated `CutsceneCamera` + `CinemachineBrain` + `PlayerVcam`/`BeggarVcam` priority swap, mirroring the `ClassroomGazeScene` cinematic pattern. Discontinued after causing a camera-teleport bug: `CinemachineBrain` was fighting the FirstPersonController asset's own per-frame camera writes (head bob/zoom), desyncing Cinemachine's internal blend position from the camera's actual transform.
- Replaced with a simpler rotation-only approach: `BeggarInteraction` disables the FPS controller and manually lerps `PlayerCamera`'s rotation toward a `lookTarget` transform (placed at the beggar's face height), re-aiming every frame during the lerp-in for exact tracking, then snaps once on arrival.
- On sequence end, lerps the camera back out to its pre-trigger rotation (captured at the start of the beat) before re-enabling the FPS controller, avoiding a snap when control resumes.
- Known caveat: if the FPS controller tracks its own internal pitch/yaw state separately from `transform.rotation`, a subtle snap could still occur one frame after control resumes; not yet confirmed whether this happens with the current asset.

---

### 2026-07-29 — Dialogue Typing Sound

- Added `dialogueAudioSource`, `typingSound`, and `finishSound` fields to `NpcConversation.cs`, passed through to `NpcDialogueManager.ShowDialogue()` via two new optional parameters (existing callers unaffected).
- Updated `NpcDialogueManager.TypeLine()` to loop `typingSound` on `audioSource` for the duration of each line's type-out, then explicitly `Stop()` it the instant the line finishes — replacing an earlier per-character `PlayOneShot` approach that could leave sound ringing past the last character.
- `finishSound` plays once via `PlayOneShot` after `ShowDialogue()` fully returns (i.e. after any end-of-conversation choice), independent of the typing loop's `Stop()`/`loop` state.

---

### 2026-07-29 — Beggar Yes/No Choice (Pending Bezi Setup)

- Prompted Bezi to add `YesButton`/`NoButton` under the existing `choicePack` container, styled to match `talkPanel`, wired to `NpcDialogueManager.MakeChoice(0)` and `MakeChoice(1)` respectively.
- `needsChoiceAtEnd` to be set `true` on the beggar's `NpcConversation` so `HandleChoices()` runs after its lines finish.
- Not yet implemented: nothing currently reads `lastChoiceIndex` after `HandleChoices()` completes, so the Yes/No choice is captured but does not yet branch dialogue or gameplay outcome.

---

### 2026-11-03 — Metro Trash Timing Minigame & Resident Evil Item Pickup Toast

- **World Pickup Ticket Gating**:
  - Gated the old world pickup ticket spawning behind `useTrashMinigame = true` in `TeddyFindQuest.cs`, `DialogueTriggerZone.cs`, and `FindableItem.cs`. The old spawn logic remains intact for trivial rollback.
- **Trash Can Ticket Source (`TrashTicketSource.cs`)**:
  - Attached to the metro trash bin as the new ticket source.
  - Proximity detection and HUD prompt integration via `NpcInteractionText.Instance`. Supports keyboard (`E`), gamepad, and mobile on-screen interact button.
  - On minigame success, sets `MetroStorySequenceController.Instance.transitPassCollected = true`, triggers `CheckFloor1Progress()`, completes `TeddyFindQuest` if wired, and persists found state in `PlayerPrefs` (`"Metro_TicketFound"`).
- **Timing Minigame UI (`TimingMinigameUI.cs`)**:
  - Overlay with horizontal ping-pong marker, randomized green success zone, and inspector-configurable speed, zone width, fail speed increase, and max speed.
  - Locks and safely restores `FirstPersonController` movement and camera look, including fallback handling if destroyed or disabled abnormally.
  - Fail feedback: short container shake, red flash overlay, fail buzzer SFX, and retries with new randomized zone.
  - Optional SFX hooks (rummaging loop, tick, success, fail) with procedural PSX synthesis fallbacks if audio clips are unassigned.
  - Mobile-friendly fullscreen tap capture target.
- **Resident Evil-Style Item Pickup Toast (`ItemPickupToast.cs`)**:
  - Reusable singleton with `Show(Sprite icon, string localizedLabel, int amount)`.
  - Slides in from top-right, holds for 2.5s, and slides back out using unscaled time (`Time.unscaledDeltaTime`) so it functions even during pauses.
  - Smooth cubic ease-out in and ease-in out.
  - Back-to-back notifications queue sequentially.
  - Harmonized with project UI: pure black background (`RGBA(0, 0, 0, 0.92)`), crisp thin white outline (`RGBA(1, 1, 1, 0.45)`, dist `1.2, -1.2`), and `GeistPixel-Regular-VariableFont_ELSH SDF` font matching `PromptBox` and `TalkPanel`.
  - Ticket graphic upgrade: dedicated recessed icon frame (`58x38` with inner outline), `Train_Ticket.png` reimported uncompressed with Bilinear filtering + mipmaps to remove nearest-neighbor downsampling noise and DXT1 block artifacts.
- **Timing Minigame UI Theme Refresh & SFX Integration**:
  - Minigame frame restyled with deep black background, matching `PromptBox` border, `GeistPixel` font headers/hints, dark recessed bar track with border, and vibrant green timing zone.
  - Wired `SearchingSoundeffect.mp3` (`Assets/Assets/Episode 1/Sounds/SearchingSoundeffect.mp3`) directly into `TimingMinigameUI.rummagingLoopClip` across all metro scenes (`Gameplay3`, `Gameplay1`, `Gameplay3 1`).
  - Automated scene baker (`MetroTrashMinigameSetup.cs`) updated to build the black-and-white theme, wire audio, and maintain clean scene serialization.

- **Localization**:
  - Added new English, French, and Arabic translations to `Assets/Resources/Localization/dialogue.csv` for item labels, interaction prompts, and minigame instructions.


---

### 2026-11-03 — Metro Fuse Box Audio Pack & Android Mobile Audio Optimization

- **Audio Pack Integration (`MetroFuseBoxPuzzle.cs`)**:
  - Added serialized `AudioClip` fields for the complete fuse box audio set with configurable volume sliders and procedural audio fallbacks:
    - `electricityHumClip` (`ElectricityHum.mp3`): ambient/restored power electrical hum loop.
    - `fuseUpClip` (`FuseUp.wav`): snappy breaker switch flip-up sound (OFF).
    - `fuseDownClip` (`FuseDown.wav`): weighty breaker switch flip-down sound (ON).
    - `doorOpenClip` (`FuseBox_Open_Trimmed.wav`): cabinet door creak on open.
    - `doorCloseClip` (`Fusebox_Close.mp3`): solid latch slam on inspection exit.
    - `powerSurgeClip`: power restoration surge fanfare.
  - Directional Breaker Sound: Flipping a switch ON (downward) triggers `fuseDownClip`; flipping OFF (upward) triggers `fuseUpClip`.
  - State Persistence: If `isPowerRestored` is already true when re-entering the scene, `StartElectricalHum()` automatically spins up the hum loop with solved switch rotations.

- **Audio Latency Optimization**:
  - Discovered `FuseBox_Open.mp3` had 1.682s of initial dead silence, causing the door opening sound to play 1.2s after the door animation finished.
  - Exported `FuseBox_Open_Trimmed.wav` (starts immediately at 0.0s with 15ms anti-click fade) to sync open audio with visual door movement, leaving the original source intact.

- **Android Mobile Audio Optimizations (Unity `AudioImporter`)**:
  - **Force to Mono (`forceToMono = true`)**: Applied across all fusebox clips to ensure proper 3D spatial panning via `AudioSource.spatialBlend = 0.85f` while halving uncompressed RAM usage.
  - **Load Types & Compression**:
    - `ElectricityHum.mp3` (18.89s loop): Set to `CompressedInMemory` + `Vorbis` (Quality 65%, `OptimizeSampleRate`). Avoids loading 3.6 MB of uncompressed audio into mobile RAM while streaming loop playback with near-zero overhead.
    - `FuseUp.wav` (0.43s) & `FuseDown.wav` (0.32s): Set to `DecompressOnLoad` + `ADPCM` (`OptimizeSampleRate`). Zero-latency tactile feedback on mobile touch/tap with negligible RAM (<35 KB) and zero CPU decoding cost.
    - `FuseBox_Open_Trimmed.wav` (1.45s) & `Fusebox_Close.mp3` (1.54s): Set to `DecompressOnLoad` + `Vorbis` (Quality 70%). Immediate door audio playback without run-time hitching.
  - Applied both Default and platform-specific `Android` override sample settings (`SetOverrideSampleSettings("Android", ...)`).

- **Automated Editor Setup (`MetroFuseBoxAudioSetup.cs`)**:
  - Added `Tools/Setup Fuse Box Audio Pack` menu command to auto-wire the clips, tune `AudioSource` 3D falloff parameters, and save scenes cleanly.


---

### 2026-11-04 — Metro Footsteps Audio Integration & Multi-Surface Audio System

- **Metro Footstep Audio Integration (PlayerFootsteps.cs)**:
  - Added native SurfaceType.Metro support alongside Concrete, Sand, and Auto modes.
  - Added serialized metroClips array loaded from Assets/Assets/Episode 1/Sounds/Footsteps/Metro/:
    - Footstep01.wav
    - Footstep02.wav
    - Footstep03.wav
    - Footstep04.wav
  - Intelligent auto-detection in GetCurrentSurface():
    - Raycasts ground colliders for metro/train tags and names.
    - Resolves gameplay scene contexts (Gameplay*, Metro) to dynamically use metro footstep sounds on station platforms, tiles, and interior floors.
    - Gracefully falls back to concrete/sand if any clip array is empty.

- **Scene & Prefab Synchronization**:
  - Updated FirstPersonController.prefab with metroClips populated and surfaceType = Auto.
  - Updated all Metro gameplay scenes (Gameplay3, Gameplay1, Gameplay2, Gameplay3 1) with surfaceType = Metro and metroClips assigned.
  - Preserved MoroccanBeach.unity sand footstep overrides without regression.

- **Automated Editor Setup (MetroFootstepsAudioSetup.cs)**:
  - Added Tools/Setup Metro Footstep Audio menu command to batch-wire the 4 metro clips, concrete clips, and sand clips into the prefab and all metro scenes with clean scene serialization.


---

### 2026-11-04 - Mobile Touch Controls: Transparent Analog Stick & Hidden Look Drag Zone

- **Mobile Touch Controls Architecture (`MobileTouchControls.cs`)**:
  - Implemented dual-zone mobile touch control layout mapped to `FirstPersonController`:
    - **Left Screen Half (Analog Stick)**: Semi-transparent virtual joystick for movement (strafe + forward/back).
      - Dynamic floating joystick behavior: centers directly under player thumb on initial touch on the left screen half.
      - Procedurally generated smooth circular sprites (`JoystickBase.png` and `JoystickKnob.png`) with translucent styling (idle alpha 0.28, active alpha 0.70).
      - Clamps movement vector within `maxJoystickRadius` (75px) and feeds normalized `Vector2` into player movement.
    - **Right Screen Half (Hidden Camera Look)**: Completely invisible touch drag zone (`Color.clear`, `raycastTarget = true`).
      - Tracks touch deltas on drag with dedicated horizontal (`0.18`) and vertical (`0.14`) sensitivities.
      - Multi-touch isolated via `PointerEventData.pointerId` to guarantee simultaneous movement and look without finger conflicts.
  - Integrated cleanly with `FirstPersonController.cs` without interfering with desktop/keyboard inputs or cursor locks.
  - Added `forceShowInEditor` toggle so developers can test mobile drag interactions directly with the mouse in the Unity Editor.

- **Prefab & Scene Integration**:
  - Added `MobileControlsCanvas` with `CanvasScaler` (1080p reference, `MatchWidthOrHeight: 0.5`) to `FirstPersonController.prefab`.
  - Updated metro scenes (`Gameplay3`, `Gameplay1`, `Gameplay2`, `Gameplay3 1`) with the mobile controls canvas.
  - Created `Tools/Setup Mobile Touch Controls` editor menu tool (`MobileControlsSetup.cs`) for repeatable batch setup and serialization.


---

### 2026-11-04 - Train Arrival Hitch Fix, Recurring Train Passes & Metro Sound System

- **Root Cause Analysis of Train Arrival Lag Spike**:
  - Discovered `Railway.mp3` (44.5s long) was imported with `loadType: DecompressOnLoad`, `preloadAudioData: false`, and `compressionFormat: PCM`.
  - When `BeginMove()` was called at `delayBeforeMove = 3s`, Unity synchronously blocked the main render thread to allocate and decode 8.5 MB of uncompressed audio on the fly, creating a ~1-second freeze.
  - `OncfAmbiance.MP3` (593.4s / 10 minutes) was similarly configured with `DecompressOnLoad` and was not looping properly in existing scenes (`loop = false`, volume 0.081).
  - Moving the 47-renderer Train hierarchy without a Kinematic Rigidbody caused PhysX to recalculate static hierarchy state.

- **Zero-Latency Audio Optimization (AudioImporter)**:
  - **`Railway.mp3`**: Changed to `AudioClipLoadType.Streaming` + `Vorbis` (Quality 70%) with `loadInBackground = true` and `preloadAudioData = true`. Streams directly from disk with zero main-thread decompression overhead.
  - **`OncfAmbiance.MP3`**: Changed to `AudioClipLoadType.Streaming` + `Vorbis` (Quality 65%) with `loadInBackground = true` and `preloadAudioData = true`. Eliminates ~105 MB of uncompressed memory usage.
  - **`Oncf1.MP3` & `Oncf2.MP3`**: Set to `CompressedInMemory` + `Vorbis` (Quality 70%) for instant station announcement playback.
  - **`TrainHorns.wav`**: Set to `DecompressOnLoad` + `ADPCM` (mono) for instantaneous tactile 3D spatial horn playback.

- **Recurring Train Passes (`TrainArrivalSequence.cs`)**:
  - Added pre-warming in `Awake()` via `clip.LoadAudioData()` to ensure all buffers are warm.
  - Added kinematic `Rigidbody` to root Train object for smooth physics interpolation without static-collider tree rebuilds.
  - **First Pass & Periodic Looping**: Performs the first pass after `delayBeforeMove` (3.5s), then resets off-screen in the tunnel and repeats periodically ("from while to while", configurable min: 45s, max: 75s).
  - **Audio Fading & Horn Cue**: Smooth audio volume fade-in when departing, automatic horn blast (`TrainHorns.wav`) via dedicated secondary `AudioSource` as the train approaches the platform, and smooth fade-out as it enters the exit tunnel.
  - Added `[ContextMenu("Trigger Train Pass Now")]` for live in-editor testing.

- **Metro Sound System (`MetroSoundSystem.cs`)**:
  - **Station Atmosphere**: Continuous looping Moroccan ONCF station ambience (`OncfAmbiance.MP3`) at balanced volume (0.35).
  - **Platform PA Announcements**: Automated announcements using authentic ONCF Moroccan train station chimes (`Oncf1`, `Oncf2`).
  - **Train Arrival Coordination**: Station PA plays an announcement 7-8 seconds before an incoming train passes the platform, warning passengers.

- **Automated Editor Setup (`MetroAudioAndTrainSetup.cs`)**:
  - Added `Tools/Setup Metro Train & Sound System` menu tool to configure all audio sources, clips, and scene components cleanly across `Gameplay3`, `Gameplay1`, `Gameplay2`, and `Gameplay3 1`.


---

### 2026-11-08 — Full Doudou Narrative, Multilingual System & Arabic RTL Support

- Implemented the Episode 1 psychological storyline across `House_Export`, `Start`, `Story`, `MoroccanBeach`, `Ending`, and `Credits`, keeping the child protagonist unnamed across all dialogue.
- Extended `LocalizationManager.cs`, `NpcDialogueManager.cs`, and `ClassroomLifeIsStrangeUI.cs` with `RTLTMPro` shaping and fallback font handling for right-to-left Arabic rendering.
- Wired `LanguageDropdownHandler.cs` in `MainMenu.unity` with persistent `PlayerPrefs` (`SelectedLanguage`).
- Connected full playthrough flow from `House_Export` → `Start` → `Story` → `MoroccanBeach` → `Ending` → `Credits` with dual ending branches in `BeachEndingController.cs`.

---

### 2026-11-09 to 2026-11-10 — Truth or Dare Removal, Dialogue Workflow Editor & Darija Language

- **Truth or Dare Removal (`TRUTH_OR_DARE_REMOVAL.md`)**: Stripped the obsolete Floor 3 Truth/Dare prompt, choice gate, table/paper drawing mini-game, and `PlatformEndTrigger` in favor of a continuous flow from `Bully_NPC` → `TeddyTurnRoutine()` ("Wake up") → `DoorWaiting` → interrogation room cinematic.
- **Workflow Editor (`DialogueWorkflowEditorWindow.cs`)**: Added **Tools → Dialogue & Translation Manager** (`Window → Dialogue & Translation Manager`) to inspect, filter by speaker/stage, edit, and add rows in `Assets/Resources/Localization/dialogue.csv` in chronological playthrough order.
- **Darija Localization & Arabic Script Standardization**: Added the `darija` column alongside `en`, `fr`, and `ar` across all 270 keys in `dialogue.csv`, standardized pure Arabic script (avoiding Latin characters inside Arabic/Darija dialogue and canvas UI), and wired `LocalizeStaticSceneUI()` across menus, prompts, HUDs, pause overlay, and credits.
- **Visual-Only Typewriter**: Disabled per-character typing sound effects across playthrough scenes while keeping clean visual typewriter presentation.

---

### 2026-11-10 to 2026-11-11 — Story Staging, Interrogation Room Integration & House Birds Ambience

- **Train Motion & Grounded Teddy Intro (`TrainArrivalSequence.cs`, `MetroStorySequenceController.cs`)**: Smoothed train movement timing/interpolation and updated the Floor 1 Teddy intro so `Doudou` remains seated on the bench while first speaking (`teddy_metro_01`–`02`), then transfers to `CarryPoint` once the dialogue finishes.
- **Interrogation Room Behind Floor 3 Door (`Story.unity`, `MetroStorySequenceController.cs`)**:
  - Replaced capsule placeholders for parents with seated character models (`ParentsSitting.controller`) and scaled interrogation furniture, characters, and camera framing to realistic fit.
  - Attached the `interrogation` room directly behind `Floor3/Door_Behind` and rewrote `cinematic_01`–`09` so `Teacher`, `Mother`, and `Father` remain unaware of the child outside while discussing the post-hospital fallout, the drowning rescue, and the boy's survivor guilt before transitioning into `DarkRoomRoutine()` → `MoroccanBeach`.
- **House Morning Ambience (`HouseWakingCinematic.cs`, `House_Export.unity`)**: Replaced the house morning ambience clip with quiet birds ambience (`Assets/Assets/Episode 1/Sounds/Birds.mp3`, low-pass filtered on `AmbianceAudioSource`) and cleared unintended startup one-shot audio (`alarmAudioSource.playOnAwake = false`).

---

### 2026-11-11 — Static Occlusion Culling, URP Pipeline Optimization & FPS Benchmark

- **Static Occlusion Culling & Batching**: Tagged static architecture/props across all 5 active gameplay/menu scenes (`MainMenu`, `House_Export`, `Start`, `Story`, `MoroccanBeach`), tightened camera `farClipPlane` (`120`–`350`), excluded Floor 3 interrogation/doorway geometry from destructive occluders, and baked `OcclusionCullingData.asset` in each scene's folder.
- **URP, SSAO, Lighting & Material Instancing**:
  - `PC_RPAsset.asset`: Disabled unnecessary `m_RequireOpaqueTexture` and `m_RequireDepthTexture`, set `m_ShadowCascadeCount = 2` and `m_ShadowDistance = 35`, `m_AdditionalLightsShadowmapResolution = 1024`, and enabled dynamic batching.
  - `PC_Renderer.asset`: Enabled SSAO `Downsample = 1` and `BlurQuality = Medium`.
  - `Story.unity`: Converted `Interrogation_MainLight` from a shadow-casting Point Light (6 shadow passes) to a downward Spot Light (`spotAngle = 128`, `range = 14`, `intensity = 3.2`), eliminating shadow atlas overflow warnings, and tightened `85.5m` station point lights to `26m`.
  - Enabled GPU instancing (`enableInstancing = true`) across `228` URP materials and disabled shadow casting on non-caster surfaces/tiny clutter.
- **Measured Result (`1563x879`, Uncapped PC Quality, 11 fixed camera viewpoints)**: Mean FPS improved from `189.84 FPS` to `265.80 FPS` (`+40.0%`), with draw calls reduced up to `-37.1%` in `House_Export` (`LivingRoom_East`) and `-95.3%` on `MainMenu`.

---

### 2026-11-11 — Mobile Shaders, Enlarged Multi-Touch Analog Controls & Elevator Button Prompt

- **Mobile Shader Pipeline (`VHSPostProcessFeature.cs`, `VHS_Mobile_BlackFisheye_URP.shader`, `Mobile_Renderer.asset`)**: Skipped preview/reflection cameras, forced `msaaSamples = MSAASamples.None` on temp RenderGraph RT, added clamped `_BlitScaleBias` sampling in `VHS_Mobile_BlackFisheye_URP.shader`, and enabled SSAO + `VHSRendererFeature` on `Mobile_Renderer.asset` alongside `Android` batching and landscape orientation lock.
- **Mobile Touch Controls Overhaul (`MobileTouchControls.cs`, `FirstPersonController.cs`, `MobileControlsSetup.cs`, `FirstPersonController.prefab`)**:
  - Added `ActivePointer` pointer validation (`InputSystem` `Touchscreen.current.touches` / `Mouse.current` and legacy `UnityEngine.Touch`), focus/pause reset guards, safe-area/parent clamping, fixed anchor-origin/pivot math in `OnLeftPointerDown`, and guarded desktop zoom with `!Application.isMobilePlatform`.
  - Removed broken missing scripts on `LeftTouchZone` and `RightLookZone` in `FirstPersonController.prefab` and applied an enlarged analog stick (`JoystickBase` `260x260` at `(280, 240)`, `JoystickHandle` `114x114`, `maxJoystickRadius = 105`, `idleAlpha = 0.48`, `activeAlpha = 0.88`) across `House_Export`, `Story`, and `MoroccanBeach` (`Start` and `Ending` remain cleanly without movement controls).
  - Wired `GraphicRaycaster` and `interactButtonRect` in `MoroccanBeach.unity`.
- **Elevator Button Prompt Canvas Fix (`NpcInteractionText.cs`, `ElevatorButton.cs`, `DoorInteractable.cs`, `dialogue.csv`)**:
  - Root cause: `NpcInteractionText.Update()` called `ClearPrompt()` every frame when its raycast missed `IInteractable`/`InteractNPC`/`MetroFuseBoxPuzzle`, hiding `PromptBox` on the same frame `ElevatorButton.Update()` set it; additionally `prompt_press_elevator_button` was missing from `dialogue.csv`.
  - Added an external prompt lease (`SetPrompt(text, onClick)`, `SetInternalPrompt(text)`, `lastExternalPromptFrame`, `externalPromptClickAction`) in `NpcInteractionText.cs`, added camera-range check (`Mathf.Max(interactDistance, 2.2f)`) and pause/dialogue guards in `ElevatorButton.cs`, wired `DoorInteractable.InteractWithDoor` cleanly, and added `prompt_press_elevator_button` across all 4 languages in `dialogue.csv`.
