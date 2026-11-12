# As A Kid — Episode 1 (`as-a-kid-ep1-unity`)

A PSX-style first-person psychological narrative adventure built in **Unity 6 (`6000.4.4f1`)** with **Universal Render Pipeline (`URP 17.4.0`)** targeting **PC (Standalone)** and **Android**.

For the complete narrative walkthrough and dialogue script from first scene to last, see [`STORY.md`](STORY.md).

---

## Episode 1 Build Scene Flow

Defined in [`ProjectSettings/EditorBuildSettings.asset`](ProjectSettings/EditorBuildSettings.asset):

1. [`Assets/Scenes/MainMenu.unity`](Assets/Scenes/MainMenu.unity) — Title screen, Episodes menu, Options (Language, Sensitivity, Sound, 60 FPS Lock, FPS Counter), and Credits trigger
2. [`Assets/Scenes/House_Export.unity`](Assets/Scenes/House_Export.unity) — Act I: Morning wake-up cinematic (`Mama` at the doorway, 3 blinks, alarm clock), washing face, dressing & taking backpack, getting the medical certificate from `Mama`, and leaving for school
3. [`Assets/Scenes/Start.unity`](Assets/Scenes/Start.unity) — Act II: Seated classroom lecture (`Teacher`), desk clue inspection (Pen, Drawing, Desk Carving), opening Page 54, and dream transition
4. [`Assets/Scenes/Story.unity`](Assets/Scenes/Story.unity) — Act III: Underground Metro Floor 1 (grounded `Doudou` bench encounter, NPCs, 5-switch fuse box puzzle, trash-can timing minigame for the train ticket), Elevator Void Train false-stop, Floor 3 (`Bully` confrontation, `"Wake up"` turn), `Floor3/interrogation` room cinematic, and Dark Room reveal
5. [`Assets/Scenes/MoroccanBeach.unity`](Assets/Scenes/MoroccanBeach.unity) — Act IV: Shoreline confrontation with `Doudou`, gathering 3 Wood Logs and 1 Sail Cloth to build the 4-stage raft, and choosing between two endings
6. [`Assets/Scenes/Ending.unity`](Assets/Scenes/Ending.unity) — Act V: Classroom awakening (`Teacher` calling out while the boy has collapsed at his desk)
7. [`Assets/Scenes/Credits.unity`](Assets/Scenes/Credits.unity) — 4-shot Cinemachine vignette credits sequence returning to `MainMenu`

*(Note: `Assets/Scenes/Backrooms.unity` is present in `Assets/Scenes/` but disabled (`enabled: 0`) in build settings.)*

---

## Key Systems & Tooling

- **Multilingual Localization & RTL UI (`en`, `fr`, `ar`, `darija`)**:
  - Data source: [`Assets/Resources/Localization/dialogue.csv`](Assets/Resources/Localization/dialogue.csv) (`270` localized keys across 4 columns).
  - Runtime manager: [`LocalizationManager`](Assets/Scripts/Dialogue/LocalizationManager.cs) with automatic Arabic RTL shaping (`RTLTMPro`), fallback font setup, and `LocalizeStaticSceneUI()`.
  - Editor workflow tool: [`DialogueWorkflowEditorWindow`](Assets/Editor/DialogueWorkflowEditorWindow.cs) (**Tools → Dialogue & Translation Manager** / **Window → Dialogue & Translation Manager**) organizes all story dialogue, prompts, and UI strings in chronological playthrough order for editing and translation review (including Arabic/Darija script checks).
- **PC & Android Touch Controls**:
  - [`FirstPersonController`](Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.cs) and [`MobileTouchControls`](Assets/ModularFirstPersonController/FirstPersonController/MobileTouchControls.cs) provide multi-touch analog movement (`260x260` `JoystickBase` with safe-area/parent clamping and `ActivePointer` tracking), right-side camera look, dedicated mobile Interact/Drop buttons, and runtime prompt tapping via [`NpcInteractionText`](Assets/Scripts/Interactions/NpcInteractionText.cs) (including external prompt leases for [`ElevatorButton`](Assets/Scripts/Elevator/ElevatorButton.cs) and [`DoorInteractable`](Assets/Scripts/Story/DoorInteractable.cs)).
  - Batch scene setup utility: [`MobileControlsSetup`](Assets/Editor/MobileControlsSetup.cs) (**Tools → Setup Mobile Touch Controls**).
  - UI adaptation & notches: [`SafeAreaHandler`](Assets/Scripts/UI/SafeAreaHandler.cs), [`MobileUIAdapter`](Assets/Scripts/UI/MobileUIAdapter.cs), and [`MobilePauseMenu`](Assets/Scripts/UI/MobilePauseMenu.cs).
- **URP Shaders, Quality Profiles & Optimizations**:
  - Quality profiles in [`ProjectSettings/QualitySettings.asset`](ProjectSettings/QualitySettings.asset): **`Mobile` (`0`)** mapped to Android/iOS (`Mobile_RPAsset` + `Mobile_Renderer`), and **`PC` (`1`)** mapped to Standalone (`PC_RPAsset` + `PC_Renderer`).
  - Fullscreen VHS/fisheye post-process pass via [`VHSRendererFeature`](Assets/Fears%20to%20Fathom%20Vhs%20for%20URP/VHSPostProcessFeature.cs) and `VHS_Mobile_BlackFisheye_URP.shader` active across both PC and Mobile renderers.
  - Static occlusion culling (`OcclusionCullingData.asset`) baked across `MainMenu`, `House_Export`, `Start`, `Story`, and `MoroccanBeach`, plus [`GameSettingsManager`](Assets/Scripts/Settings/GameSettingsManager.cs) runtime `60 FPS` cap and FPS counter.

---

## Project Documentation

- [`STORY.md`](STORY.md) — Complete Episode 1 narrative from start to finish
- [`Assets/DEVLOG.md`](Assets/DEVLOG.md) — Chronological development log covering early prototypes through Episode 1 completion, optimizations, and mobile polish
- [`TRUTH_OR_DARE_REMOVAL.md`](TRUTH_OR_DARE_REMOVAL.md) — Removal specification for the obsolete Floor 3 Truth or Dare mini-game and updated interrogation transition
- [`MIGRATION_REPORT.md`](MIGRATION_REPORT.md) & [`MIGRATION_PLAN.md`](MIGRATION_PLAN.md) — Integration notes for `House_Export.unity`
- [`UI_AUDIT.md`](UI_AUDIT.md) — UI consistency, Canvas Scaler, RTL, and mobile readiness audit
