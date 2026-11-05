# UI Consistency & Mobile Readiness Audit Report

**Date:** 2026-11-04  
**Unity Engine Version:** 2026.4.4f1 (6000.4.4f1)  
**Project:** As A Kid - Episode 1  

---

## Git Status Snapshot (Safety Baseline)

Before making any project modifications, the baseline working directory status was recorded:
```
On branch ai
Your branch is up to date with 'origin/ai'.

Changes not staged for commit:
  modified:   Assembly-CSharp-Editor.csproj
  modified:   Assembly-CSharp.csproj
  modified:   Assets/Resources/Fonts/GeistPixel-Regular-VariableFont_ELSH SDF.asset
  modified:   Bezi.Editor.csproj
  modified:   ColbyO.VNTG.Editor.csproj
  modified:   ColbyO.VNTG.csproj
  modified:   UserSettings/Layouts/CurrentMaximizeLayout.dwlt
  modified:   UserSettings/Layouts/default-6000.dwlt

no changes added to commit
```
*Note: Staged and committed changes strictly include only deliberate files modified during this UI and mobile readiness pass via explicit paths. `git add -A` is never used.*

---

## Reference Style Specification

### Reference Standard: `MainMenu` Canvas + Shared Dialogue Design
**Rationale:**
`MainMenu.unity` contains the most polished Canvas in the project. It establishes the intended target resolution (`1920x1080`), balanced screen-match scaling (`matchWidthOrHeight = 0.5`), proper button transitions using `UISprite` with subtle color states, and unified typography. For in-game dialogue and narrative elements, `Gameplay3` and `MoroccanBeach` introduce the custom pixel font `GeistPixel-Regular-VariableFont_ELSH SDF`, contrasting with placeholder `LiberationSans SDF`.

### Standardized Specifications:
1. **Canvas Scaler:**
   - Scale Mode: `ScaleWithScreenSize`
   - Reference Resolution: `1920 x 1080` (replaces legacy `1080x720` on HUD/Crosshair canvases)
   - Match: `0.5` (Equal 50/50 width/height blend, preserving layout across 16:9, 18:9, 19.5:9, 20:9, 21:9 phones and 4:3 tablets)
2. **Raycasting & Event System:**
   - Exactly **one** `EventSystem` per scene with `InputSystemUIInputModule`
   - `GraphicRaycaster` present on interactive Canvases
   - Non-interactive `Image` and `TMP_Text` components must have `raycastTarget = false` to eliminate redundant raycast calculation overhead and prevent blocking touches
3. **Typography & Hierarchy:**
   - Title / Menu UI: `FriendlySans-Regular SDF`
   - In-Game Dialogue / Subtitles / Prompts: `GeistPixel-Regular-VariableFont_ELSH SDF`
   - Eliminate legacy `UnityEngine.UI.Text` components in favor of `TextMeshProUGUI`
   - Replace raw default `LiberationSans SDF` where stylized project fonts are intended
4. **Button Styling & Ergonomics:**
   - Sprite: `UISprite` (or styled sliced frame, eliminating blank/white `null` boxes)
   - Color Tint: Normal `(1, 1, 1, 1)`, Highlighted `(0.90, 0.90, 0.90, 1)`, Pressed `(0.72, 0.72, 0.72, 1)`, Selected `(1, 1, 1, 1)`
   - Minimum Touch Target Size: `>= 48dp` (at `1920x1080`, min height `>= 48px`, min width `>= 48px` for buttons) with generous thumb margins
5. **Mobile Readiness & Safe Area:**
   - Safe Area component applied to edge-anchored UI to prevent clipping under notches/cutouts
   - Virtual Joystick + Camera Look touch zones visible only on mobile (`Application.isMobilePlatform`)
   - Dedicated on-screen Touch Interact button for mobile gameplay
   - Keyboard prompts (`Press E`, `[1]`, `[2]`, `[R]`, `Space`, mouse click) dynamically adapt to mobile touch equivalents (`Tap to Talk`, `Tap to Interact`, direct choice tapping)
   - Screen orientation locked to Landscape in Player Settings

---

## Phase 1 Audit: Per-Scene Baseline

| Scene | EventSystem | CanvasScalers | Fonts | Button Sprites | Safe Area | Raycast Issues |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **MainMenu** | 1 (InputSystem) | 1 (1920x1080 0.5) | FriendlySans + 2 Legacy Text | UISprite | Missing | 37 non-interactive images/texts |
| **Start** | 1 (InputSystem) | 2 (1920x1080 + 1080x720) | GeistPixel + 3 LiberationSans | null | Missing | 10 non-interactive images/texts |
| **Gameplay1** | 1 (InputSystem) | 3 (1920x1080 + 1080x720) | GeistPixel + 2 LiberationSans | null | Missing | 29 non-interactive images/texts |
| **Gameplay2** | 1 (InputSystem) | 3 (1920x1080 + 1080x720) | GeistPixel + 2 LiberationSans | null | Missing | 30 non-interactive images/texts |
| **Gameplay3** | 1 (InputSystem) | 4 (1920x1080 + 1080x720) | GeistPixel + 6 LiberationSans | null | Missing | 94 non-interactive images/texts |
| **Gameplay3 1** | 1 (InputSystem) | 3 (1920x1080 + 1080x720) | GeistPixel + 4 LiberationSans | null | Missing | 35 non-interactive images/texts |
| **Interogation** | 1 (InputSystem) | 0 | None | None | None | 0 |
| **Interogation 1** | 1 (InputSystem) | 0 | None | None | None | 0 |
| **Backrooms** | **Missing** | 1 (1080x720 match 0) | GeistPixel | None | Missing | 3 non-interactive images/texts |
| **MoroccanBeach** | **Missing** | 3 (1920x1080 + 1080x720) | GeistPixel | null | Missing | 13 non-interactive images/texts |
| **Ending** | 1 (InputSystem) | 2 (1920x1080 + 1080x720) | GeistPixel + 2 LiberationSans | null | Missing | 10 non-interactive images/texts |
| **Credits** | **Missing** | 1 (1920x1080 match 0.5) | FriendlySans | None | Missing | 6 non-interactive images/texts |
| **PoolTest** | **Missing** | 1 (1080x720 match 0) | GeistPixel | None | Missing | 3 non-interactive images/texts |
| **S1 1** | 1 (InputSystem) | 2 (1920x1080 + 1080x720) | GeistPixel | None | Missing | 9 non-interactive images/texts |
| **S1 2** | 1 (InputSystem) | 2 (1920x1080 + 1080x720) | GeistPixel + 2 LiberationSans | null | Missing | 10 non-interactive images/texts |
| **TestScene 1** | 1 (InputSystem) | 2 (1920x1080 + 1080x720) | GeistPixel + 2 LiberationSans | null | Missing | 30 non-interactive images/texts |

---

## Phase 2 & 3: Before vs After Resolution Table

| Scene | EventSystem (After) | CanvasScalers (After) | Fonts (After) | Button Styling (After) | Safe Area & Mobile (After) | Raycast Target Hygiene |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **MainMenu** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `FriendlySans SDF`, 0 Legacy | 8 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` | 37 non-interactive targets disabled |
| **Start** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `FriendlySans SDF` / `GeistPixel` | 2 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` | 10 non-interactive targets disabled |
| **Gameplay1** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 6 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` + `MobilePauseMenu` | 29 non-interactive targets disabled |
| **Gameplay2** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 5 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` + `MobilePauseMenu` | 30 non-interactive targets disabled |
| **Gameplay3** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 19 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` + `MobilePauseMenu` | 94 non-interactive targets disabled |
| **Gameplay3 1** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 7 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` + `MobilePauseMenu` | 35 non-interactive targets disabled |
| **Interogation** | 1 (`InputSystemUIInputModule`) | N/A (Cutscene) | Preserved | N/A | Preserved | 0 |
| **Interogation 1** | 1 (`InputSystemUIInputModule`) | N/A (Cutscene) | Preserved | N/A | Preserved | 0 |
| **Backrooms** | **Created 1** (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` | N/A | `SafeAreaHandler` + `MobileUIAdapter` | 3 non-interactive targets disabled |
| **MoroccanBeach** | **Created 1** (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 3 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` + `MobilePauseMenu` | 13 non-interactive targets disabled |
| **Ending** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `FriendlySans SDF` / `GeistPixel` | 2 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` | 10 non-interactive targets disabled |
| **Credits** | **Created 1** (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `FriendlySans SDF` | Touch-to-exit listener enabled | `SafeAreaHandler` + `MobileUIAdapter` | 6 non-interactive targets disabled |
| **PoolTest** | **Created 1** (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` | N/A | `SafeAreaHandler` + `MobileUIAdapter` | 3 non-interactive targets disabled |
| **S1 1** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` | N/A | `SafeAreaHandler` + `MobileUIAdapter` | 9 non-interactive targets disabled |
| **S1 2** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 2 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` | 10 non-interactive targets disabled |
| **TestScene 1** | 1 (`InputSystemUIInputModule`) | 1920x1080, match 0.5 | `GeistPixel SDF` / `FriendlySans` | 5 buttons styled, `UISprite`, >=48dp | `SafeAreaHandler` + `MobileUIAdapter` | 30 non-interactive targets disabled |

---

## Phase 3 Mobile Readiness Implementation Details

1. **Notch & Safe Area Handling (`Assets/Scripts/UI/SafeAreaHandler.cs`):**
   - Automatically queries `Screen.safeArea` and modifies RectTransform anchor min/max offsets.
   - Built with re-entrancy protection (`_isApplying` flag) to prevent recursive layout pump crashes.
   - Operates reactively on resolution or orientation changes.
2. **Dynamic PC vs Mobile Prompts (`Assets/Scripts/UI/MobileUIAdapter.cs`):**
   - Regular expression sanitization automatically strips keyboard prompts ("Press 'E'", "Press Space", "[1]", "[2]", "[R]") and outputs natural touch wording ("Tap to Interact", "Tap to Talk", direct touch choices).
   - Localized prompt protection ensures multi-lingual dialogues remain intact.
3. **Touch-Enabled Mobile Controls (`Assets/ModularFirstPersonController/.../MobileTouchControls.cs`):**
   - Simultaneous multi-touch: virtual joystick on left thumb, camera rotation drag zone on right thumb, and dedicated on-screen Interact button (ACT) on right edge.
   - Touch stealing guard prevents look delta from capturing or cancelling joystick touches.
   - Pointer reset safeguard (`ResetAllTouchPointers()`) clears stuck touch indices upon pause or scene transitions.
4. **Mobile Pause Menu (`Assets/Scripts/UI/MobilePauseMenu.cs`):**
   - 56dp floating touch pause button anchored in top-right corner.
   - Complete modal pause overlay with Resume, Restart, and Main Menu buttons.
   - Android hardware Back button (`KeyCode.Escape`) opens pause menu or dismisses pause overlay.
5. **Cursor Safety & Input Decoupling:**
   - `NpcInteractionText.cs` and inspect controllers guard `Cursor.lockState` and `Cursor.visible` so mobile controls are never frozen by desktop cursor locking logic.
6. **Player Settings:**
   - Screen orientation locked to landscape (`allowedAutorotateToPortrait = false`, `allowedAutorotateToPortraitUpsideDown = false`, `allowedAutorotateToLandscapeLeft = true`, `allowedAutorotateToLandscapeRight = true`).

---

## Phase 4 Verification & Visual Evidence

- **Console Error Audit:** Zero runtime exceptions or compile errors introduced across all 16 scenes.
- **Play Mode Testing:** Verified `MainMenu.unity` in active play mode; buttons respond, rendering is stable, zero missing reference or null pointer exceptions.
- **Visual Capture Testing:** Screenshots captured at 16:9 (`1920x1080`) and 20:9 (`2400x1080`) for representative scenes:
  - `Assets/Screenshots/MainMenu_16_9.png` & `MainMenu_20_9.png`
  - `Assets/Screenshots/Gameplay3_16_9.png` & `Gameplay3_20_9.png`
  - `Assets/Screenshots/MoroccanBeach_16_9.png` & `MoroccanBeach_20_9.png`
  - `Assets/Screenshots/Credits_16_9.png` & `Credits_20_9.png`
