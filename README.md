# As A Kid (Episode 1) — Moroccan Psychological Horror

[![Downloads](https://img.shields.io/badge/itch.io-+6,000%20Downloads-FA5C5C?style=for-the-badge&logo=itch.io&logoColor=white)](https://yassir001.itch.io/as-a-kid-demo)
[![Engine](https://img.shields.io/badge/Engine-Unity%206-black?style=for-the-badge&logo=unity&logoColor=white)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-Universal%20RP%20(URP)-blue?style=for-the-badge)](https://unity.com/srp/universal-render-pipeline)
[![Community](https://img.shields.io/badge/Instagram-27K%2B%20Community-E4405F?style=for-the-badge&logo=instagram&logoColor=white)](https://www.instagram.com/thats_yessir)
[![Platform](https://img.shields.io/badge/Platform-Windows%20PC-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://yassir001.itch.io/as-a-kid-demo)

> *"A haunting journey through the nostalgic corridors of a Moroccan childhood, where the boundary between innocent memory and lingering dread fades into shadow."*

**As A Kid** is a solo-developed narrative psychological horror title that gained widespread indie acclaim with **over 6,000 downloads on itch.io**. Set in an authentic Moroccan domestic environment, players navigate liminal urban transit spaces, familiar apartment rooms, and eerie dreamscapes brought to life with a stylized retro PSX aesthetic.

🎮 **[Play the Demo on itch.io](https://yassir001.itch.io/as-a-kid-demo)**  
📺 **[Watch Devlog & Technical Breakdowns (@thats_yessir)](https://www.instagram.com/thats_yessir)**

---

## 🌟 Key Highlights & Milestones

* **6,000+ Downloads on itch.io**: Featured and played by numerous indie horror content creators worldwide.
* **Cultural Authenticity**: The first psychological horror game set in a traditional Moroccan urban and domestic environment.
* **Full Darija & English Localization**: Custom-engineered multi-language subtitle and dialogue system supporting both Darija (Moroccan Arabic) and English.
* **Retro PSX / VHS Aesthetic**: Implements custom vertex snapping, affine texture mapping, retro color palettes, and CRT scanline post-processing.
* **Expanding Universe**: Currently in active development of the sequel, *As A Kid: Barzakh*.

---

## 🕹️ Narrative & Gameplay Mechanics

### 1. Complex Narrative Sequencing
The game is driven by modular event-driven sequence managers:
* **Metro Corridor Sequence** (`MetroStorySequenceController.cs`): Orchestrates cinematic train arrival events, flickering lighting, environmental sound cues, and psychological manifestations.
* **Classroom Gaze Mechanic** (`ClassroomGazeScene.cs`): Detects player viewing angle and field-of-view; triggers chilling psychological events only when the player looks away or stares into specific dark corners.
* **Liminal Elevator State Machine** (`FloorManager.cs`, `ElevatorButton.cs`): Dynamic multi-floor transitions reflecting fragmented childhood memories.
* **Escape / Raft Construction Quest** (`RaftPlayerInteraction.cs`, `RaftBuildArea.cs`, `BeachEndingController.cs`): Interactive object collection and multi-stage assembly culminating in a bittersweet narrative finale.

### 2. Dialogue & Localization Engine
* Custom **`LocalizationManager`** supporting real-time language toggling (English / Darija) with CSV/JSON lookup tables.
* Decoupled dialogue events (`DialogueManager.cs`, `DialogueInputAdvancer.cs`, `DialogueFadeScreen.cs`) synchronizing audio voice lines, subtitles, and cinematic camera cuts.

### 3. Tactile Inspection & Interaction
* Raycast-driven physical interaction pipeline allowing inspection, pickup, and rotation of nostalgia-laden props (school bags, cassette tapes, domestic utensils).

---

## 🏗️ Technical Architecture & Script Hierarchy

```
Assets/Scripts/
├── Dialogue/
│   ├── LocalizationManager.cs          # Multi-language table lookup (Darija / English)
│   ├── DialogueManager.cs              # Dialogue state machine & UI typewriter effect
│   ├── DialogueInputAdvancer.cs        # Input handling for narrative branching
│   ├── CameraCutsceneController.cs     # Cinematic focus switching during dialogue
│   └── ClassroomGazeScene.cs           # Gaze-tracking psych mechanics in the classroom
├── Story/
│   ├── MetroStorySequenceController.cs # Master narrative controller for metro chapter (41KB)
│   ├── ClassroomEndingController.cs    # Classroom transition & puzzle logic
│   ├── PoolStoryManager.cs             # Dream sequence pool puzzle
│   └── DoorInteractable.cs             # Physical door pivots, locks, and keys
├── Raft/
│   ├── RaftPlayerInteraction.cs        # Crafting & item placement mechanics
│   ├── RaftBuildArea.cs                # Socket collision system for assembly
│   └── BeachEndingController.cs        # Cinematic beach sequence controller
├── Elevator/
│   ├── FloorManager.cs                 # Multi-floor dispatching & room loading
│   └── ElevatorButton.cs               # Interactive 3D elevator keypad
├── Carry/
│   ├── CarryableItem.cs                # Rigidbody physics item pickup & carry
│   └── TeddyFindQuest.cs               # Key story item quest manager
└── Screen/
    └── ScreenFader.cs                  # Smooth visual transitions between chapters
```

---

## 🎨 Art, Shaders & Sound Design

* **PSX Retro Shaders**: Built on Unity URP with custom render passes to simulate 32-bit hardware quirks: vertex coordinate truncation, low internal resolution scaling, and affine texture warping.
* **Atmospheric Soundscape**: Multi-layered ambient audio design with dynamic reverb zones capturing the quiet dread of empty concrete stairwells, metro subways, and rainy Moroccan streets.
* **Original 3D Modeling**: Handcrafted low-poly 3D models modeled in Blender, using authentic Moroccan architectural references, furniture, and textures.

---

## 🎮 Controls

| Action | Keyboard & Mouse |
| :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` |
| **Look** | Mouse |
| **Interact / Inspect** | `E` or `Left Click` |
| **Advance Dialogue** | `Space` or `Left Click` |
| **Sprint** | `Left Shift` |
| **Pause Menu** | `Escape` |

---

## 💻 Unity Project Setup

### Requirements
* **Unity Version**: Unity 6 (6000.0+) or Unity 2022.3 LTS with Universal Render Pipeline (URP).
* **Render Pipeline**: Universal RP.

### Instructions
1. Clone the repository:
   ```bash
   git clone https://github.com/Yassir-Essabbahy/as-a-kid-ep1-unity.git
   ```
2. Open **Unity Hub** and click **Add** > **Add project from disk**.
3. Select the `as-a-kid-ep1-unity` root directory.
4. Open the project with Unity 6 (recommended) or Unity 2022.3 LTS.
5. In the Project window, navigate to `Assets/Scenes/` and load the start scene: `MainMenu.unity` or `IntroScene.unity`.
6. Press **Play** in the editor.

---

## 👨‍💻 Developer & Community

**Yassir ESSABAHY**  
* Solo Indie Game Developer & 3D Technical Artist  
* Itch.io: [yassir001.itch.io](https://yassir001.itch.io/)  
* Instagram: [@thats_yessir](https://www.instagram.com/thats_yessir) *(27K+ community following devlogs)*  
* Portfolio: [yessirdev.vercel.app](https://yessirdev.vercel.app)  
* LinkedIn: [linkedin.com/in/yessir001](https://www.linkedin.com/in/yessir001/)
