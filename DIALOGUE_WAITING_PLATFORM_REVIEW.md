# Waiting Platform Dialogue & Phase 4 Tag Review

Internal design review document for **The Waiting Platform** (`Story 1.unity`).
- Contains all 7 bench NPCs + Doudou whispers/reactions.
- Stores the **Phase 4 Alignment Tags** (`DOUDOU-LEANING` vs `TRUTH-LEANING`) for all **21 questions**. These tags are intentionally kept **out** of `Assets/Resources/Localization/dialogue.csv`.
- Uses `{DROWNED}` strictly as a neutral placeholder where a name or relation is needed.

---

## Phase 4 Question Alignment Summary

| NPC (`npcId`) | Key | Question (EN) | Tag | Why |
| :--- | :--- | :--- | :--- | :--- |
| `neighbor` | `npc_neighbor_q1` | Why is nobody looking at me today? | `DOUDOU-LEANING` | Seeks normal reassurance instead of asking what happened. |
| `neighbor` | `npc_neighbor_q2` | Do the neighbors talk about our house? | `DOUDOU-LEANING` | Worries about appearances / staying hidden from gossip. |
| `neighbor` | `npc_neighbor_q3` | What did everyone hear that afternoon? | `TRUTH-LEANING` | Directly asks about the siren and the afternoon event. |
| `shopowner` | `npc_shopowner_q1` | Why is your little stall closed today? | `DOUDOU-LEANING` | Safe surface deflection focusing on the shop shutter. |
| `shopowner` | `npc_shopowner_q2` | What do I still owe you for? | `TRUTH-LEANING` | Confronts the debt/guilt he keeps avoiding. |
| `shopowner` | `npc_shopowner_q3` | Did my small wish take {DROWNED} away? | `TRUTH-LEANING` | Faces the memory of what he wished for that day. |
| `traveler` | `npc_traveler_q1` | Will the next train take me home? | `DOUDOU-LEANING` | Hopes to escape back home without remembering. |
| `traveler` | `npc_traveler_q2` | Does Doudou keep me safe down here? | `DOUDOU-LEANING` | Clings to Doudou's shelter instead of questioning it. |
| `traveler` | `npc_traveler_q3` | How do I leave this station forever? | `TRUTH-LEANING` | Asks for the real way out, accepting the water. |
| `bully` | `npc_bully_q1` | Why won't anyone play with me anymore? | `DOUDOU-LEANING` | Focuses on loneliness / being left out rather than why. |
| `bully` | `npc_bully_q2` | What do the other kids whisper behind me? | `TRUTH-LEANING` | Pushes past the insult to hear what actually happened. |
| `bully` | `npc_bully_q3` | Why do you sound just like my head? | `TRUTH-LEANING` | Recognizes the bully's voice as his own inner guilt. |
| `teacher` | `npc_teacher_q1` | Am I in trouble for leaving class? | `DOUDOU-LEANING` | Treats the moment as an ordinary school rule problem. |
| `teacher` | `npc_teacher_q2` | Why didn't anyone ask me at school? | `DOUDOU-LEANING` |Questions why adults avoided it—safe distance from the memory. |
| `teacher` | `npc_teacher_q3` | Why did I freeze on page fifty-four? | `TRUTH-LEANING` | Directly confronts the reading trigger about the coast. |
| `mama` | `npc_mama_q1` | Mama, why won't you look at me? | `DOUDOU-LEANING` | Wants comfort and eye contact rather than asking the hard question. |
| `mama` | `npc_mama_q2` | Were you scared at the hospital, Mama? | `TRUTH-LEANING` | Enters the real memory of the hospital nights. |
| `mama` | `npc_mama_q3` | Mama, are you angry about {DROWNED}? | `TRUTH-LEANING` | Faces his deepest fear of her blame head-on. |
| `baba` | `npc_baba_q1` | Which road takes us back home, Baba? | `DOUDOU-LEANING` | Looks for a practical escape route away from the sea. |
| `baba` | `npc_baba_q2` | Why are you so quiet with me? | `DOUDOU-LEANING` | Seeks reassurance instead of naming what stands between them. |
| `baba` | `npc_baba_q3` | Baba, why didn't you save {DROWNED}? | `TRUTH-LEANING` | Forces out the unspoken grief and shared guilt. |

**Distribution:** 11 `DOUDOU-LEANING`, 10 `TRUTH-LEANING`.

---

## Full Script Reference (English)

### 1. Neighbor (`neighbor`) — *The Gossip*
- **Greeting (`npc_neighbor_greeting`):** Oh, you finally walked over... Keep your voice down, everyone knows how fast words travel here...
- **Doudou Whisper (`doudou_neighbor_whisper`):** Don't listen to his whispers, little one. Neighbors only look for stories to tell.
- **Q1 (`npc_neighbor_q1`) [DOUDOU-LEANING]:** Why is nobody looking at me today?
  - **A1 (`npc_neighbor_a1`):** Because it is polite not to stare, dear... Everyone knows a boy shouldn't see all those worried faces anyway...
- **Q2 (`npc_neighbor_q2`) [DOUDOU-LEANING]:** Do the neighbors talk about our house?
  - **A2 (`npc_neighbor_a2`):** Oh, never harshly, no, never... People just lean over their balconies, whispering, counting who came back through the door...
- **Q3 (`npc_neighbor_q3`) [TRUTH-LEANING]:** What did everyone hear that afternoon?
  - **A3 (`npc_neighbor_a3`):** Everyone knows that siren, sweetie... It wailed down the whole street that afternoon, and the doors haven't stopped whispering about {DROWNED} since...
- **Done (`npc_neighbor_done`):** Don't tell anyone I said a word... Keep looking ahead...
- **Doudou Reaction (`doudou_neighbor_reaction`):** See how they twist everything into noise, little one? Stay near me where it's quiet.

### 2. Shop Owner (`shopowner`) — *The Creditor*
- **Greeting (`npc_shopowner_greeting`):** The shutter is down. Nothing is free on this platform, and your account is still open.
- **Doudou Whisper (`doudou_shopowner_whisper`):** You don't owe him a single coin, little one. Walk past.
- **Q1 (`npc_shopowner_q1`) [DOUDOU-LEANING]:** Why is your little stall closed today?
  - **A1 (`npc_shopowner_a1`):** Stock ran out the day you stopped paying in coins. Some ledgers stay open even when the padlock turns.
- **Q2 (`npc_shopowner_q2`) [TRUTH-LEANING]:** What do I still owe you for?
  - **A2 (`npc_shopowner_a2`):** Small things cost the most at closing time. One cone, one extra minute—you asked, and someone else paid the bill.
- **Q3 (`npc_shopowner_q3`) [TRUTH-LEANING]:** Did my small wish take {DROWNED} away?
  - **A3 (`npc_shopowner_a3`):** You wanted one more swim, one more ice cream by the shore. In your ledger, every wish carries a price nobody can refund.
- **Done (`npc_shopowner_done`):** The shutter stays down. Settle your own ledger.
- **Doudou Reaction (`doudou_shopowner_reaction`):** A child's wish isn't a curse, little one. Stop listening to his numbers.

### 3. Old Traveler (`traveler`) — *The Only Honest One*
- **Greeting (`npc_traveler_greeting`):** Peace on you, my child. A bird that fears the sea builds a cage out of concrete.
- **Doudou Whisper (`doudou_traveler_whisper`):** He speaks in riddles to confuse you, little one. Hold my hand tighter.
- **Q1 (`npc_traveler_q1`) [DOUDOU-LEANING]:** Will the next train take me home?
  - **A1 (`npc_traveler_a1`):** No train leaves a station built inside your own mind. Even the elevator climbs only toward what you fear to remember.
- **Q2 (`npc_traveler_q2`) [DOUDOU-LEANING]:** Does Doudou keep me safe down here?
  - **A2 (`npc_traveler_a2`):** A blanket warms a shivering child, yes... Until the walls of the tent grow heavier than the storm outside.
- **Q3 (`npc_traveler_q3`) [TRUTH-LEANING]:** How do I leave this station forever?
  - **A3 (`npc_traveler_a3`):** You built this roof to hide from the shore, my boy. You cannot cross dry land—you must face the water.
- **Done (`npc_traveler_done`):** Salt heals wounds that silence hides. Go gently.
- **Doudou Reaction (`doudou_traveler_reaction`):** Don't listen to him, little one. The water is cold—you don't need to go back there.

### 4. Bully (`bully`) — *The Schoolyard Voice*
- **Greeting (`npc_bully_greeting`):** Took you long enough. Empty seat, empty head—still talking to thin air, weirdo?
- **Doudou Whisper (`doudou_bully_whisper`):** He was never your friend, little one. Cover your ears.
- **Q1 (`npc_bully_q1`) [DOUDOU-LEANING]:** Why won't anyone play with me anymore?
  - **A1 (`npc_bully_a1`):** Because you vanished to the hospital and came back broken. Nobody sits near the kid who stares at nothing.
- **Q2 (`npc_bully_q2`) [TRUTH-LEANING]:** What do the other kids whisper behind me?
  - **A2 (`npc_bully_a2`):** Same stuff they mumble at recess. "He got pulled out... he called for help..." I just say it louder so they laugh.
- **Q3 (`npc_bully_q3`) [TRUTH-LEANING]:** Why do you sound just like my head?
  - **A3 (`npc_bully_a3`):** Because I didn't invent those words... You already whisper worse to yourself every night about {DROWNED}, don't you?
- **Done (`npc_bully_done`):** Leave me alone... Stop staring like that.
- **Doudou Reaction (`doudou_bully_reaction`):** Don't believe a word you tell yourself at night, little one. Stay with me.

### 5. Teacher (`teacher`) — *The Careful Adult*
- **Greeting (`npc_teacher_greeting`):** Ah, you're here on time. Let's stay calm and take this one step at a time, shall we?
- **Doudou Whisper (`doudou_teacher_whisper`):** Careful, little one. He writes everything down in that blue folder.
- **Q1 (`npc_teacher_q1`) [DOUDOU-LEANING]:** Am I in trouble for leaving class?
  - **A1 (`npc_teacher_a1`):** Not in trouble, no. Let's stay calm—why do you think you needed to step outside today?
- **Q2 (`npc_teacher_q2`) [DOUDOU-LEANING]:** Why didn't anyone ask me at school?
  - **A2 (`npc_teacher_a2`):** We didn't want to distress you. Tell me gently—what goes through your mind when the classroom goes quiet?
- **Q3 (`npc_teacher_q3`) [TRUTH-LEANING]:** Why did I freeze on page fifty-four?
  - **A3 (`npc_teacher_a3`):** Every time the reading mentions the coast, you stop breathing. We noticed, of course—we just never dared ask about {DROWNED}.
- **Done (`npc_teacher_done`):** Let's stay calm. You may return to your seat.
- **Doudou Reaction (`doudou_teacher_reaction`):** Adults only want neat answers on paper, little one. Close the book with me.

### 6. Mama (`mama`) — *The Broken Tenderness*
- **Greeting (`npc_mama_greeting`):** Come sit beside me, wlidi... Don't step near the edge, habibi, don't step near the edge.
- **Doudou Whisper (`doudou_mama_whisper`):** Her voice shakes too much, little one. She wants to tell you something painful.
- **Q1 (`npc_mama_q1`) [DOUDOU-LEANING]:** Mama, why won't you look at me?
  - **A1 (`npc_mama_a1`):** If I look at you, habibi, I'll start crying again. Just stay dry here... stay dry here, wlidi.
- **Q2 (`npc_mama_q2`) [TRUTH-LEANING]:** Were you scared at the hospital, Mama?
  - **A2 (`npc_mama_a2`):** Those nights beside your bed were the hardest of my life, habibi... Watching you shiver, begging in your sleep.
- **Q3 (`npc_mama_q3`) [TRUTH-LEANING]:** Mama, are you angry about {DROWNED}?
  - **A3 (`npc_mama_a3`):** No, wlidi, never... Never. Why can't my tongue say it out loud to you—it wasn't your fault, habibi, it wasn't...
- **Done (`npc_mama_done`):** Stay warm, wlidi... Stay warm, habibi...
- **Doudou Reaction (`doudou_mama_reaction`):** Why didn't she say it before, little one? Maybe she's only trying to make you feel better.

### 7. Baba (`baba`) — *The Silent Wall*
- **Greeting (`npc_baba_greeting`):** Late again. Keep your coat buttoned. Look straight ahead.
- **Doudou Whisper (`doudou_baba_whisper`):** Don't wake his anger, little one. You know how quiet he gets.
- **Q1 (`npc_baba_q1`) [DOUDOU-LEANING]:** Which road takes us back home, Baba?
  - **A1 (`npc_baba_a1`):** Not this one. Go straight home from school. Never stop by the sea road.
- **Q2 (`npc_baba_q2`) [DOUDOU-LEANING]:** Why are you so quiet with me?
  - **A2 (`npc_baba_a2`):** Talking changes nothing. Doors lock, shoes dry, life moves. Leave it alone.
- **Q3 (`npc_baba_q3`) [TRUTH-LEANING]:** Baba, why didn't you save {DROWNED}?
  - **A3 (`npc_baba_a3`):** I see that afternoon every time I shut my eyes. How could I tell you it wasn't your fault... when I blame myself too?
- **Done (`npc_baba_done`):** Go straight home. Do not look back.
- **Doudou Reaction (`doudou_baba_reaction`):** Even he couldn't fix it, little one. Stay here where nobody blames anyone.
