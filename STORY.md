# As A Kid — Episode 1: Full Story (From First to Last)

> **Genre:** PSX-Style First-Person Psychological Narrative / Horror  
> **Languages Supported:** English (`en`), French (`fr`), Arabic (`ar`), and Darija (`darija`) via [`Assets/Resources/Localization/dialogue.csv`](Assets/Resources/Localization/dialogue.csv)  
> **Scene Progression:** [`MainMenu`](Assets/Scenes/MainMenu.unity) → [`House_Export`](Assets/Scenes/House_Export.unity) → [`Start`](Assets/Scenes/Start.unity) → [`Story`](Assets/Scenes/Story.unity) → [`MoroccanBeach`](Assets/Scenes/MoroccanBeach.unity) → [`Ending`](Assets/Scenes/Ending.unity) → [`Credits`](Assets/Scenes/Credits.unity)

---

## Core Premise & Characters

The protagonist (**Child**) is never given a spoken name. Recently discharged from the hospital after a traumatic incident at the beach, he is returning to school for the first time. Unknown to his classmates, the drowning accident took someone close to him who jumped back into rough waves to save him — and the boy is consumed by survivor's guilt, believing the tragedy happened because he cried out for help.

Inside his mind, that trauma manifests through a distorted underground metro station guided by his stuffed teddy bear, **Doudou** (`دودو` in Arabic/Darija, `{TEDDY_NAME}` in dialogue data), an imaginary coping shield keeping him away from painful memories.

| Character | Role in Episode 1 |
|---|---|
| **Child** | Unnamed schoolboy protagonist trying to get through his first day back at school |
| **Doudou (`{TEDDY_NAME}`)** | His worn teddy bear; inside the dream/dissociation, an imaginary companion trying to keep him sheltered from the truth |
| **Mama (Mother)** | Wakes him for school at home, insists he bring his medical clearance certificate to the headmaster, and worries over his nightmares |
| **Baba (Father)** | Advises him to go straight to school and straight back home; later confides to the teacher how badly the boy is struggling at night |
| **Teacher** | Lectures the morning class (`Page 54`), meets privately with the parents to understand what happened before the hospital, and tries to wake the boy when he collapses in class |
| **Neighbor / Old Traveler** | Cryptic figure on the Metro platform who warns that the elevator leads to repressed memories and reveals the breaker pattern |
| **Shop Owner** | Eerie vendor standing beside a shuttered stall on Floor 1, waiting for the boy |
| **Bully** | Hostile classmate manifestation on Floor 3 mocking the boy's hospital absence and imaginary friend |

---

## Act 0 — Main Menu ([`MainMenu.unity`](Assets/Scenes/MainMenu.unity))

- **Entry Point:** Player selects **Episodes → Episode 1: Metro → Play** (or configured first scene `House_Export`).
- Selecting **Options** lets the player choose the language (`English`, `Arabic`, `French`, `Darija`), mouse/touch sensitivity, audio volume, FPS cap (`60 FPS` toggle), and HUD FPS counter.
- Pressing **Play** resets session flags (`HasSeenIntroThisSession`, `IsEndingActive`, `Metro_TicketFound`) and fades into [`House_Export.unity`](Assets/Scenes/House_Export.unity).

---

## Act I — Morning at Home ([`House_Export.unity`](Assets/Scenes/House_Export.unity))

### 1.1 Bedroom Wake-Up Cinematic
- The screen opens from darkness on a pillow perspective inside the boy's bedroom.
- **Mama** stands in the open bedroom doorway and calls out (`mama_doorway_wake`):
  > **Mama:** *"Yallah fyeq a wlidi, nod lmdrassa..."*
- Mama steps back and pulls the bedroom door shut (`Door5`) before returning to the kitchen stove (`Cooking_Loop`).
- An alarm clock (`AlarmClock`) rings while muffled by sleep (`550 Hz` → `1800 Hz` → `22000 Hz` low-pass filter) across three slow eyelid blinks (`EyelidBlink`).
- Reaching the clock stops the alarm with a click (`ClockClick`), followed by a morning breath sigh (`MorningBreath`), quiet morning birds ambience (`Birds.mp3`), and a smooth 5-second transition sitting up and standing beside the bed.

### 1.2 Morning Routine Objectives
Managed by [`HouseObjectiveController`](Assets/House/Scripts/HouseObjectiveController.cs):

1. **Wash Face (`phase = 0` — `house_objective_wash_face`):**
   - Attempting to put on clothes, grab the backpack, or talk to Mama beforehand prompts the boy or Mama to send him to the bathroom first (`house_need_wash_face` / `mama_remind_wash_face`).
   - Interacting with the bathroom washbasin ([`HouseWashBasinInteractable`](Assets/House/Scripts/HouseWashBasinInteractable.cs)) tilts the camera down into darkness while water splashes (`WaterSplashFace.wav`), then opens eyes with a refreshed sigh:
     > *"Face washed. Feeling awake!"* (`house_face_washed`)
2. **Get Dressed & Take Backpack (`phase = 1` — `house_objective_get_dressed`):**
   - Opening the wardrobe doors ([`HouseWardrobeInteractable`](Assets/House/Scripts/HouseWardrobeInteractable.cs)) and interacting again puts on school clothes during a quick dip to black (`ClothRustle.wav`).
   - Interacting with the school bag ([`HouseBackpackInteractable`](Assets/House/Scripts/HouseBackpackInteractable.cs)) collects the backpack.
3. **Talk to Mama (`phase = 2` — `house_objective_talk_mom`):**
   - Talking to **Mama** in the kitchen ([`HouseMamaDialogue`](Assets/House/Scripts/HouseMamaDialogue.cs)) reveals the fragile state behind his return to school (`mama_house_envelope`):
     > **Mama:** *"Take this. Hand it directly to the headmaster (Al-Mudir). Don't lose it... we had a hard time convincing the doctor you were ready to go back."*
   - Speaking to her again repeats `mama_house_reminder`: *"Don't lose that certificate. Head straight to school!"*
   - Optionally talking to **Baba** seated in the living room ([`HouseBabaDialogue`](Assets/House/Scripts/HouseBabaDialogue.cs)) gives `baba_house_advice`:
     > **Baba:** *"Watch the road. Straight to school, straight back home. No wandering off."*
   - Exploring the house reveals quiet environmental details: an always-empty fridge (`fridge_comment`), a cabinet he could hide inside from his mom (`cabinet_comment`), a room Dad forbids entering (`door_locked`), and locked drawers/windows.
4. **Leave for School (`phase = 3` — `house_objective_go_school`):**
   - Interacting with the front door before getting Mama's papers gives `house_need_talk_mom`. Once spoken to Mama, the front door reads **"Leave for School"** (`prompt_leave_school`) and fades out into [`Start.unity`](Assets/Scenes/Start.unity).

---

## Act II — The Classroom & Page 54 ([`Start.unity`](Assets/Scenes/Start.unity))

### 2.1 Seated Lecture & Desk Clues
Controlled by [`ClassroomGazeScene`](Assets/Scripts/Dialogue/ClassroomGazeScene.cs):
- The protagonist sits at his desk looking toward the front of the room while the **Teacher** begins class:
  - `classroom_intro_01`: *"Teacher: Good morning, everyone... We have fallen far behind on this week's lessons."*
  - `classroom_intro_02`: *"Teacher: We'll begin immediately. Time moves faster than you think."*
  - `classroom_intro_03`: *"Teacher: Let's start right away."*
- The book on the desk cannot be opened yet (`desk_book_locked_01`: *"I should listen to what the teacher is saying first."*).
- Meanwhile, the boy inspects three required items on his desk ([`ClassroomDeskInteractable`](Assets/Scripts/Dialogue/ClassroomDeskInteractable.cs)), each surfacing quiet fragments of his trauma:
  1. **Pen (`desk_pen_01`):** *"My favorite pen... its plastic smells just like the hospital gift shop."*
  2. **Drawing (`desk_doodle_01`):** *"I drew a wooden boat sinking in grey waves..."*
  3. **Desk Carving (`desk_carving_01`):** *"Someone carved 'WAKE UP' here."*

### 2.2 Opening Page 54
- Once the lecture lines finish and all three desk clues are inspected, the Teacher calls (`classroom_intro_04`):
  > **Teacher:** *"Could everyone please open page 54? Now."*
- Interacting with the book blends the camera down onto the desk as the cover opens. The boy's internal monologue begins:
  - `player_intro_01`: *"I have no energy left for this... Didn't we study this last week? Or did those days pass while I was gone?"*
  - `player_intro_02` / `lookback_book_01`: *"Wait... this dark illustration on the page was never here before."*
  - `lookback_book_02`: *"It feels as if... it is opening its mouth to call me."*
- As the wall clock ticks away and muffles out beneath a swelling sub-bass rumble, the camera pushes deep into the dark illustration on Page 54 and dissolves into [`Story.unity`](Assets/Scenes/Story.unity).

---

## Act III — The Underground Metro & Behind the Door ([`Story.unity`](Assets/Scenes/Story.unity))

Orchestrated by [`MetroStorySequenceController`](Assets/Scripts/Story/MetroStorySequenceController.cs):

### 3.1 Floor 1 — Finding Doudou on the Bench
- **Objective:** `Find Teddy...` (`obj_find_teddy`).
- **Doudou** (`Teddy_Bear_Box`) sits grounded on a bench on the cold, dimly lit Floor 1 station platform.
- Approaching him triggers his first conversation (`teddy_metro_01`–`02`):
  > **Doudou:** *"You're late today, little one... I was afraid you'd swallow all those colorful pills and forget my path."*  
  > **Child:** *"Why does the station feel so cold?"*
- After the conversation, the boy picks Doudou up (`CarryPoint`) and carries him through the station.

### 3.2 Floor 1 — Platform NPCs, Station Clues & Restoring Power
- **Neighbor (`Neigbhor_NPC`, `neighbor_01`–`05`):** Whispers *"Ah... you're finally here... We've been waiting for you"* before falling silent (`"..."`).
- **Shop Owner (`ShopOwner_NPC`, `shop_01`–`05`):** Stands at a closed shopfront saying they thought he wouldn't come and are waiting *"For you."*
- **Station Props:**
  - **Vending Machine (`vending_machine_01`):** Broken, carrying a torn note: *"Don't listen to the train."*
  - **Intercom (`intercom_01`):** Crackles that *"upper platform service is restricted..."*
  - **Ticket Booth (`ticket_booth_closed_01`–`02`):** Locked shutter; Doudou tells him they must search the platform for a ticket.
- **Fuse Box Inspection (`teddy_fuse_hint_01`–`02`, `obj_neighbor_knows`):**
  - Checking the dark breaker cabinet ([`MetroFuseBoxPuzzle`](Assets/Scripts/Elevator/MetroFuseBoxPuzzle.cs)) reveals it has no power; Doudou suggests asking the **Neighbor**, who knows how electrics work (`neighbor_fuse_advice_01`–`05`).
- **Old Traveler / Beggar (`begger_metro_01`–`04`):**
  - Approaching with Doudou triggers a warning about where the elevator goes and how to wire the box:
    > **Old Traveler:** *"The elevators here don't take you home, my child—they take you to what you fear to remember... Flip breakers 1, 2, 4, and 5 DOWN, and leave the center breaker UP, or the power will cut from this little mind forever."*
- **Solving the Fuse Box:**
  - Flipping breakers **1, 2, 4, and 5 DOWN** and leaving **breaker 3 UP** restores power with a surge and electrical hum.
  - Doudou reflects (`teddy_metro_03`–`04`):
    > **Doudou:** *"This is where you hide when their voices outside get louder than you can bear."*  
    > **Child:** *"I feel dizzy... the smell here feels just like medical disinfectants."*

### 3.3 Floor 1 — Searching the Trash Cans for a Train Ticket
- **Objective:** `Search the trash cans for a train ticket...` (`objective_search_trash`).
- Five trash cans on the platform ([`TrashTicketSource`](Assets/Scripts/Story/TrashTicketSource.cs)) launch a timing-bar minigame ([`TimingMinigameUI`](Assets/Scripts/Story/TimingMinigameUI.cs)): stop the moving marker inside the green zone while rummaging.
- Empty cans trigger Doudou's nudge (`teddy_trash_empty_01`), and a guaranteed pity system ensures the ticket (`Train_Ticket`) cannot be missed.
- Once found (`ticket_toast_label`), Doudou warns (`teddy_metro_05`–`06`):
  > **Doudou:** *"Well done... you have the ticket. Now let's go up, but promise me one thing: don't believe what they tell you up there."*  
  > **Doudou:** *"Let's go, the elevator is ready for us!"*
- **Objective updates:** `Head upstairs...` (`obj_head_upstairs`).

### 3.4 The Elevator Ride & Void Train False-Stop
- Pressing the elevator button ([`ElevatorButton`](Assets/Scripts/Elevator/ElevatorButton.cs)) checks that **power is restored**, the **train ticket is collected**, and **Doudou is being carried**.
- Midway up, the elevator stalls at a **false stop** and opens onto a pitch-black void track (`VoidTrackScene`).
- Headlights flicker out of the tunnel, a horn blares, and a speeding subway train (`VoidTrain`) hurtles straight toward the open elevator doors while the camera shakes violently.
- The elevator doors slam shut right at impact, leaving breathless silence (`elevator_train_reaction_01`–`03`):
  > **Child:** *"Doudou... it almost hit us! My head hurts... the noise was so loud..."*  
  > **Doudou:** *"Shh... breathe, little one. It was just an echo from the outside trying to break into your thoughts. Hold on to me tight. As long as you hold me, nothing here can hurt you. We're almost at Floor 3."*
- The elevator resumes climbing and opens onto **Floor 3**.

### 3.5 Floor 3 — The Bully, Clues & "Wake Up"
- **Objective:** `Where are we...?` (`obj_where_are_we`).
- Optional Floor 3 discoveries (`Floor3`):
  - **Departure Board (`departure_board_01`):** All destinations are erased except **`ROOM 302`**.
  - **Lost Ribbon (`teddy_lost_piece_01`):** A ribbon belonging to Doudou lying on the floor.
- **Bully Confrontation (`Bully_NPC`, `bully_floor3_01`–`05`):**
  > **Bully:** *"Do you really think you're special because you're sick? Look around... nobody plays with you because you talk to thin air. Even when you collapsed, we never wanted to see you return."*  
  > **Child:** *"But I always used to play with you..."*  
  > **Bully:** *"That was before that day. You're nothing more than a blue folder in a doctor's drawer now."*  
  > **Child:** *"My teddy says you're wrong."*  
  > **Bully:** *"... That thing is not your friend."*
- **Doudou Turns & Commands Awakening (`TeddyTurnRoutine`):**
  - Doudou dismisses the bully as *"merely shards of what you overheard in the schoolyard after you returned"* (`teddy_met_people_01`), then repeats calmly (`turn_02`–`06`):
    > **Doudou:** *"Wake up."*  
    > **Child:** *"What?"*  
    > **Doudou:** *"Wake up."*  
    > **Child:** *"But I am awake."*  
    > **Doudou:** *"No."*
- Heavy knocking echoes from **Door_Behind** leading into `Floor3/interrogation`. **Objective:** `Behind the door...` (`obj_behind_door`).

### 3.6 Behind the Door — What Actually Happened (`BehindDoorCinematicRoutine`)
- Interacting with the Floor 3 door removes Doudou from view, silences all station ambience, and reveals an interrogation/meeting room seated with **Teacher**, **Mother**, and **Father**.
- The three adults speak **completely unaware** of the boy standing outside (`cinematic_01`–`09`, `scream_01`):
  - **Teacher:** *"Thank you both for coming in. Since you brought him back from the hospital last month, I've been really worried about him in class."*
  - **Mother:** *"At the hospital the doctors told us his lungs cleared up and his body was fine... they said all he needed was rest at home."*
  - **Father:** *"But he isn't fine. He wakes up at night drenched in sweat, holding that old teddy bear and begging for forgiveness."*
  - **Teacher:** *"Forgiveness for what? Yesterday during reading he froze over a page about the coast... what happened before he ended up in the hospital?"*
  - **Father:** *"He got carried out too far, and someone went right back in after him... the ambulance barely got our boy back in time."*
  - **Mother:** *"Ever since he woke up in that hospital bed, he thinks everything was his fault... he keeps saying he shouldn't have called out for help."*
  - **Teacher:** *"A boy his age shouldn't carry a weight like that alone. Has he said anything else about that afternoon?"*
- Unable to bear hearing it, the boy cries out from outside the door:
  > **Child:** *"Stop talking about it! It was my fault... I shouldn't have called out!"*  
  > **Doudou:** *"Cover your ears and hold on to me. Stay here with me—nobody can make you go back to that day."*  
  > **Child:** *"Doudou!"*

### 3.7 The Dark Room Reveal (`DarkRoomRoutine`)
- The scene fades to black and opens inside a quiet pitch-dark space (`DarkRoom_Environment`) where Doudou speaks softly (`darkroom_01`–`08`):
  > **Doudou:** *"You've finally heard them... this is the heavy truth I've been carrying for you all these months."*  
  > **Child:** *"Why are they saying those things...? I just wanted everything to be like before."*  
  > **Doudou:** *"Because nothing stays the same. The cold, the hospital, the fear... you made this place to keep yourself safe. But you cannot stay hidden here forever. The tide is waiting."*  
  > **Child:** *"Where are you taking me?"*  
  > **Doudou:** *"To the shore where it all began."*  
  > **Child:** *"The beach..."*  
  > **Doudou:** *"Come. Let's go back to the beach and face the water together."*
- Transitions to [`MoroccanBeach.unity`](Assets/Scenes/MoroccanBeach.unity).

---

## Act IV — The Shore & The Choice ([`MoroccanBeach.unity`](Assets/Scenes/MoroccanBeach.unity))

### 4.1 Confrontation by the Ocean ([`BeachIntroController`](Assets/Scripts/Raft/BeachIntroController.cs))
- The boy stands beside a wooden beach table facing Doudou while the camera pans between him and the ocean surf (`beach_intro_01`–`06`):
  > **Doudou:** *"Do you know, little one? I have always wondered... if you woke up one day fully healed... if you stopped feeling this terror... would I cease to exist?"*  
  > **Child:** *"You are my friend, Doudou. I will never forget you."*  
  > **Doudou:** *"I am not your friend in the way you imagine. I am merely a beautiful idea forged out of fear so that you might survive. I am the wall you built between yourself and the operating rooms and the exhausted faces of doctors. But walls turn into prisons if left standing for too long... The boat is ready on the beach. And now, you must decide how you will cross to the other shore."*  
  > **Child:** *"The boat... I have to decide how to let go."*

### 4.2 Gathering & Building the Raft ([`RaftPlayerInteraction`](Assets/Scripts/Raft/RaftPlayerInteraction.cs) & [`RaftBuildArea`](Assets/Scripts/Raft/RaftBuildArea.cs))
- The HUD displays `Wood: 0/3` and `Cloth: 0/1`.
- Carrying one piece at a time across the sand, the player builds the 4 stages of the raft at the shoreline:
  1. **Foundation Logs** (`Wood Log` 1/3)
  2. **Main Decking** (`Wood Log` 2/3)
  3. **Front Decking** (`Wood Log` 3/3)
  4. **White Cloth Sail** (`Sail Cloth` 1/1)
- Once complete (`Boat: Ready!`), Doudou calls out (`beach_ending_ready`): *"The boat is ready. How will you say goodbye?"*

### 4.3 The Final Choice — Two Endings ([`BeachEndingController`](Assets/Scripts/Raft/BeachEndingController.cs))
Interacting with the finished raft opens the final decision (`beach_choice_prompt`: *"How will you cross?"*):

- **Path A — Leave Doudou Behind (`beach_choice_leave`):** *"Leave him on the sand and walk into the water"*
  - The boy steps onto the raft and drifts out to sea while the camera looks back at Doudou left on the shore. As the waves muffle away, Doudou says goodbye (`beach_farewell_path_a`):
    > **Doudou:** *"You were always brave, little one... Go and live your life. My purpose is fulfilled now."*

- **Path B — Send Doudou Away (`beach_choice_send`):** *"Put Doudou on the boat and cast him into the waves"*
  - The objective shifts to taking Doudou from the table (`beach_tracker_take_teddy`) and carrying him to the raft (`beach_tracker_bring_teddy`).
  - Placing Doudou upright on the wooden deck pushes the little raft out into the waves as the boy watches from the beach (`beach_teddy_farewell` / `beach_farewell_path_b`):
    > **Doudou:** *"If this is what you need to forgive yourself... then take your freedom. I love you, even as you cast me into the storm."*

Both endings fade to black as the ocean sound fades beneath a low-pass filter, transitioning into [`Ending.unity`](Assets/Scenes/Ending.unity).

---

## Act V — Waking in Class & End Credits ([`Ending.unity`](Assets/Scenes/Ending.unity) → [`Credits.unity`](Assets/Scenes/Credits.unity))

### 5.1 The Classroom Reality ([`Ending.unity`](Assets/Scenes/Ending.unity))
- Controlled by [`ClassroomEndingController`](Assets/Scripts/Story/ClassroomEndingController.cs), soft credits music begins (`CreditsMusic` at `0.22` volume) inside cinematic letterbox bars.
- The camera opens on the classroom wall clock (`ClockVCam`), then smoothly blends down to the **Teacher** looking directly at the protagonist collapsed at his desk:
  - `teacher_ending_01`: *"Teacher: Hey! Can you hear me?! Open your eyes!"*
  - `teacher_ending_02`: *"Teacher: Go get the principal immediately!!"*
- Everything from opening Page 54 onward — the freezing Metro station, the speeding train, the overheard meeting behind the door, and the beach raft — took place inside his mind in the moments after he fainted at his desk.
- The screen fades cleanly into [`Credits.unity`](Assets/Scenes/Credits.unity) without interrupting the music.

### 5.2 End Credits ([`Credits.unity`](Assets/Scenes/Credits.unity))
Controlled by [`CreditsSequenceController`](Assets/Scripts/Credits/CreditsSequenceController.cs), the music swells to full volume across four chronological vignettes echoing Episode 1:
1. **Metro Platform & Train Wipe** (`vcamMetro` + [`CreditsTrainMover`](Assets/Scripts/Credits/CreditsTrainMover.cs)) — *EPISODE 1: AS A KID*
2. **Moroccan Beach Table with Lone Teddy & Ocean** (`vcamBeachTeddy`) — *STORY & CREATIVE DIRECTION / ENVIRONMENT & LEVEL DESIGN: Yassir Essabbahy*
3. **Empty Classroom & Wall Clock** (`vcamClassroom`) — *GAMEPLAY SYSTEMS & CINEMATICS / SOUND DESIGN & AUDIO: Core Team*
4. **Open Ocean with Drifting Raft** (`vcamRaftOcean` + [`CreditsRaftBobber`](Assets/Scripts/Credits/CreditsRaftBobber.cs)) — *SPECIAL THANKS: To everyone who supported this journey. Thank you for playing.*

After fading to black, tapping or pressing any key (`credits_return_prompt`) returns to [`MainMenu.unity`](Assets/Scenes/MainMenu.unity).
