# MIGRATION PLAN: House Scene Integration

## Project Specifications & Environment
- **Target Project**: `as-a-kid-ep1-unity`
- **Unity Version**: Unity 6 (`6000.4.4f1`)
- **Render Pipeline**: Universal Render Pipeline (URP `17.4.0`) - matches export source scene.
- **Color Space**: Linear
- **Active Input Handler**: Both (New Input System `com.unity.inputsystem` + Legacy `Input Manager`).
- **Target Platforms**: PC (Standalone) + Mobile (Android).
- **Art & Narrative Style**: PSX-style psychological horror / retro narrative adventure.
- **Reference UI Style**: `DialogueCanvas` (Screen Space - Overlay, Canvas Scaler: 1920x1080 Match 0.5, Font: `GeistPixel-Regular-VariableFont_ELSH SDF`, with `SafeAreaHandler` and `MobileUIAdapter`). Dialogue box uses pixel borders, retro letterbox bars, typewriter sound, and speaker color coding (`Mother` = `#DDA0DD` Soft Lilac, `Father` = `#90CDF4` Slate Blue).

---

## Package Dependencies Comparison
| Package | House Export (`HANDOFF.md`) | New Project (`Packages/manifest.json`) | Status |
| :--- | :--- | :--- | :--- |
| **TextMeshPro / uGUI** | `com.unity.ugui` (2.0.0) | `com.unity.ugui`: `2.0.0` | Installed |
| **Universal RP** | `com.unity.render-pipelines.universal` (17.4.0) | `com.unity.render-pipelines.universal`: `17.4.0` | Installed |
| **Cinemachine** | `com.unity.cinemachine` (2.10.3) | `com.unity.cinemachine`: `2.10.3` | Installed |
| **ProBuilder** | `com.unity.probuilder` (5.2.4) | `com.unity.probuilder`: `5.2.4` | Installed |
| **Timeline** | `com.unity.timeline` (1.8.7) | `com.unity.timeline`: `1.8.7` | Installed |
| **AI Navigation** | `com.unity.ai.navigation` (1.1.5) | `com.unity.ai.navigation`: `1.1.5` | Installed |

*Result: All necessary packages are present in the target project. No package installations required.*

---

## Scope of Migration

### 1. Assets to Import
- Scene: `Assets/House/Scenes/House_Export.unity`
- 3D Meshes & Geometry: House structure, furniture, props, interactable drawers, doors, fridge, radio.
- Characters & Animations: `Baba` (Father) and `Mama` (Mother) meshes, materials, animator controllers, and idle/look animations.
- Audio: Living room ambiance, ambient hum, radio audio clips, interaction sound effects.
- Lighting: Prefabs, light fixtures, baked lightmaps and probe assets under `Assets/House/`.

### 2. Assets & Objects to Drop / Delete
- **`Plane.007` (`DoorToStore`)**: Deleted completely, along with its colliders, scripts, and all references to scene `"StoreWay"`.
- **`Stairs` Root Object**: Deleted completely (stair mesh and step colliders).
  - *Gap Mitigation*: Wall/floor seams inspected. Dead doors (`RoofDoor` / `Plane.009` `DoorToHome`) disabled or blocked cleanly to prevent player falling out of bounds.
- **Old Standalone Managers**: Old `GameManager`, `UIManager`, `ObjectiveUI`, `FadeCanvas`, and standalone pause/menu handlers are stripped from the active scene.
- **Old Player Rig**: Old FPS controller, old camera rigs, old EventSystem are not present in `House_Export.unity`.

### 3. Systems to Convert & Hook
- **Interactables (`IInteractable`)**:
  - Hook all 16 interactables (`Baba`, `Mama`, `200dhs`, `FridgeDoor`, `Mjr`, `Cube.038`, `Cube.039`, `Colliduer`, `Cabinet (1)`, `Radio`, `OutsideDoor`, etc.) to the new project's player interaction raycaster (`PromptBox` + `MobileInteractButton`).
  - Input trigger: `KeyCode.E` (PC) and `MobileTouchControls.IsInteractTriggeredThisFrame` (Mobile).
- **NPC Dialogue**:
  - Port Baba and Mama interaction to trigger `NpcDialogueManager` or new localized dialogue nodes matching the new project's dialogue architecture.
  - Baba and Mama `NPCLookAt` updated to target `Camera.main.transform` dynamically.
- **Outside Door / Scene Transition**:
  - `OutsideDoor` (`LocationChooser`) transitioned to trigger `ScreenFader.Instance.FadeOut(...)`. Target scene parameter left empty by default with a warning log if triggered without destination.
- **Localization**:
  - All house strings (prompts, dialogue, locked door texts) integrated into `Assets/Resources/Localization/dialogue.csv` with English, French, and Arabic keys.
- **UI Elements Restyling**:
  - Interaction prompt restyled to match `PromptBox` retro pixel box.
  - Subtitles/Dialogue restyled to `DialogueCanvas` standard (`GeistPixel-Regular-VariableFont_ELSH SDF`, letterboxing, safe area, color tags).
- **Mobile Controls**:
  - Connect `ModularFirstPersonController.MobileTouchControls` for virtual joystick and touch drag look.
  - Ensure mobile interact button appears within safe area when raycast hits an `IInteractable`.
- **Performance & Android Optimization**:
  - Textures > 1024 inspected and set to ASTC compression for mobile where appropriate.
  - Lighting optimized: Realtime lights reduced to 1 directional shadow caster; interior lamps set to baked / low-cost mixed with Light Probes.

---

## Potential Risks & Mitigation Strategies
1. **Class Name Collisions**:
   - *Risk*: `GameManager`, `UIManager`, or `ObjectiveUI` scripts in the imported package may conflict with existing project types.
   - *Mitigation*: Isolate imported house scripts inside the `House` namespace or rename them with a `House` prefix (e.g. `HouseGameManager`) and compile check before wiring.
2. **Layer & Tag Mismatches**:
   - *Risk*: Player controller raycast relies on specific layers (e.g. `Interactable` layer or default raycast mask).
   - *Mitigation*: Ensure Layer 3 is designated `Interactable`, Tag `Money` exists, and the player interaction raycast mask includes interactables.
3. **Async / Long Import Execution**:
   - *Risk*: `AssetDatabase.ImportPackage` might exceed Unity MCP 30-second timeout if invoked directly synchronously.
   - *Mitigation*: Trigger import via an Editor script or monitor background compilation carefully.
4. **Stairs Deletion Geometry Holes**:
   - *Risk*: Removing the staircase may expose untextured backfaces or holes to the outside.
   - *Mitigation*: Inspect scene boundaries where stairs connected to upper floor; seal openings with simple blocking geometry or disable doors leading to vacant stairwell.
