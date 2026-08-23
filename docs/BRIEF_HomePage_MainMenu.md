# BRIEF: Home Page / Main Menu — Myriad of Dragons

> **2026-08-11 Phase 1 addition:** The final landscape Home direction must reserve a clear Guild
> entry and notification state. The Guild destination contains overview, roster, chat,
> announcements, donations, help, research, store, rankings and event offices. Do not place those
> systems directly on Home or bake their labels into artwork. Home may show only concise guild
> status: emblem, league/rank, unread chat/announcement state, help requests and claimable reward.

**For:** an AI or engineer building this feature standalone.
**Deliverable:** one C# file, `Assets/Scripts/UI/HomeScreen.cs`, plus any small helpers it needs.
**Dependency on existing combat code:** none. This screen must not read or modify battle state.

---

## 1. Non-negotiable technical constraints

Get these wrong and the code will not fit the project.

| Constraint | Detail |
|---|---|
| Engine | Unity 6 (`6000.5.6f1`), C# |
| UI library | **Legacy uGUI only** — `UnityEngine.UI.Canvas`, `Image`, `Text`, `Button`, `HorizontalLayoutGroup`, `AspectRatioFitter`, `Mask`, `Outline`. **NOT TextMeshPro. NOT UI Toolkit/UXML.** |
| Construction | **100% procedural at runtime in C#.** There are no scenes, no prefabs, no `[SerializeField]` wiring. Anything requiring the Inspector will not work. |
| Entry point | A `MonoBehaviour` with a `public void Initialize()` method. Do **not** rely on `Awake()`/`Start()` — the project calls `Initialize()` explicitly so EditMode tests can drive it outside Play Mode. |
| Destroy calls | Use `DestroyImmediate()`, never `Destroy()`, in any code path reachable from `Initialize()`. `Destroy()` silently does nothing outside Play Mode and tests will fail. |
| Coroutines | Only run in Play Mode. Guard with `if (!Application.isPlaying) return;` before `StartCoroutine`. |
| Fonts | `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`, falling back to `"Arial.ttf"` for older editors. |
| Sprites | `Resources.Load<Sprite>("UI/...")` — everything lives under `Assets/Resources/`. |

Copy the structural patterns from `Assets/Scripts/UI/GameBootstrap.cs`. It is the reference
implementation for procedural uGUI in this project.

---

## 2. What to build

A home screen the player lands on before entering a battle. Specified in
`Myriadtutorial Final.docx` (Slide 7). Reproduced here so you need no other document.

### 2.1 Top HUD bar
- Currencies, left to right: **Food, Gold, Jewels, Gems**. Icon + value each.
- **Stamina bar** with `current / max` and a regeneration timer.
- **Flag** counter (battle entries) with its own timer.

### 2.2 Top-left: player profile
Tappable, opens a profile modal. Shows avatar portrait, **level**, class icon, guild prefix +
guild name. The modal lists: level, class, guild, last login, total cards, friends, reviews,
trades. Close via an X in the modal's top-right.

### 2.3 Top-right: settings
Opens a settings modal with sliders for **BGM** and **SFX**, plus toggles for **Sound** and
**Notifications**. All reversible. Close via X.

### 2.4 Centre: Empire view
A visual base with tappable buildings. Ten exist in the design: Castle, Barracks, Storage,
Training Grounds, Barn, Gold Mine, Gate, Laboratory, Tree of Knowledge, Prison. **For this task,
render them as a tappable grid of labelled buttons** — each currently just logs which building
was tapped. The real building screens are a separate task.

### 2.5 Bottom navigation — pinned, exactly five tabs
```
[ EMPIRE ]  [ COLLECTION ]  [ BATTLE ]  [ SOCIAL ]  [ SHOP ]
```
**BATTLE is the centre tab and must be visually emphasised** (larger, or raised). It is the core
loop and must never be buried in a submenu.

Tapping BATTLE should call a `public event Action OnBattleRequested;` — do **not** load a scene or
reference `GameBootstrap` directly. The caller wires that up. This keeps the screen testable and
independent.

### 2.6 Consumables — explicit rule
Stamina and Flag potions **must** be reachable by tapping the Stamina or Flag counter in the top
HUD, which opens a small modal. **Do not put consumables in the Settings menu.** Items hidden in
Settings are never found or used.

### 2.7 Chat bubble
A floating, draggable bubble that opens a chat panel. For this task: render the bubble, make it
draggable, and have the tap raise `public event Action OnChatRequested;`. The chat panel itself
is a separate task.

---

## 3. Rules that came out of prior review — follow them

These were mistakes found in earlier iterations of this project's UI. They cost several rounds of
rework each.

1. **Stamina is NOT combat health.** Stamina gates *entry* to a match. It must never be converted
   into or reduce in-match Avatar HP. The original tutorial implies otherwise; that is wrong — it
   would mean a player who grinds quests enters a boss fight with an unwinnable handicap.
2. **No permanent instruction banners.** Text like "tap a card, then tap a lane" belongs in the
   tutorial overlay, not welded across the screen forever.
3. **Do not use `AspectRatioFitter` on an element whose anchors you have set.** It sizes relative
   to the **parent**, silently discarding those anchors. Put it on an inner child instead. This
   caused a portrait to inflate across the whole board and hide gameplay elements.
4. **Art must fill without distorting.** `preserveAspect = true` letterboxes (dead space);
   `preserveAspect = false` squashes. Use `AspectRatioFitter` in `EnvelopeParent` mode inside a
   `Mask` to crop-fill. See `GameBootstrap.CreateCroppedArt()`.
5. **Text over artwork needs a plate or an `Outline`**, otherwise it is unreadable over busy
   backgrounds.
6. **Buttons should read as buttons** — inset from screen edges, not full-bleed bars. A control
   spanning the full width reads as a UI band, not something tappable.

---

## 4. Available art

Under `Assets/Resources/UI/`:

| Folder | Contents |
|---|---|
| `Portraits/` | 12 character portraits (Paladin, Orc_King, Angel, Assassin, Demon_Queen, Fire_Dragon, Ice_King, Kraken, Necromancer, Phoenix, Forest_Titan, Beast_Master) |
| `Frames/` | Card rarity frames, `Avatar_Circle_Frame`, `Panel_Frame`, `Character_Portrait_Frame` |
| `Frames/NineSlice/` | `Popup_Frame`, `Ornate_Panel_Frame` — ornate 9-sliced borders for modals |
| `Bars/` | `Health_Fill`, `Health_Empty`, `Mana_Fill` — horizontally 9-sliced, ornate |
| `StatusIcons/` | 35 small icons (Fire, Ice, Gold, Crystal, Holy, Poison, Rage, etc.) — **use these for currency icons** |
| `Backdrops/Arenas/` | 10 full-screen 4K landscapes |
| `Icons/` | `Tutorial_Arrow` |

No icons exist specifically for Food/Gold/Jewels/Gems — reuse from `StatusIcons/` (`Gold` for
gold, `Crystal` for jewels/gems, and pick something sensible for food) rather than shipping blank
squares.

---

## 5. Acceptance criteria

- `Initialize()` builds the entire screen with no scene assets and no Inspector wiring.
- Runs clean in an EditMode test: instantiate, call `Initialize()`, assert the five nav buttons
  and the HUD exist. No exceptions outside Play Mode.
- All five tabs present, BATTLE visually emphasised and centre.
- Tapping Stamina or Flag opens the consumables modal.
- Profile and Settings modals open and close.
- `OnBattleRequested` / `OnChatRequested` events fire; the screen references no battle code.
- Add an EditMode test in `Assets/Tests/Editor/` covering construction and the events.

## 6. Handing the result back

Return **whole files**, not diffs or fragments. Whole-file replacement is what has merged cleanly
in this project; partial snippets have repeatedly failed to apply.
