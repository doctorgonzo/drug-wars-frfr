# Drug Wars FRFR — Development Log

## Project Overview
Unity game inspired by the classic Drug Wars. Players buy/sell drugs across cities, manage heat from cops, travel between cities, and try to pay off a loan shark debt within a time limit.

**Project root:** `/Users/nickweisberg/Documents/Drug Wars FRFR/`  
**Scripts:** `Assets/Scripts/`  
**ScriptableObjects:** `Assets/Scriptable Objects/`

---

## Completed Work

### Section 4 — UX & Gameplay Fixes
- **Wallet number formatting:** All money displays use `ToString("N0")` for comma-separated thousands ($10,000 not $10000). Files: `CityUIHandler.cs`, `CharCreationUI.cs`, `InventoryItemUI.cs`, `DealerClicks.cs`, `TravelManager.cs`
- **Heat decay visual feedback:** Added `HeatState` enum (Idle/Cooldown/Decaying) with pulsing color effects on the heat bar and status text ("COOLING DOWN..." / "DECAYING..."). File: `HeatManager.cs`
- **Combat system:** Full turn-based combat loop for cop encounters. Player/cop HP, damage variance, armor reduction, win/loss conditions. Combat log with auto-sizing text. Beating a cop sets heat to 50%. File: `CopEncounterUIManager.cs`

### Section 5 — Performance Optimizations
- **TooltipUI.Update():** Early return `if (!tooltipPanel.activeSelf) return;` — zero work when hidden. File: `TooltipUI.cs`
- **HeatManager:** Replaced `Update()` with a `DecayLoop()` coroutine that only runs while `CurrentHeat > 0`. Starts via `EnsureDecayRunning()` from `AddHeat()` and `Start()`. File: `HeatManager.cs`
- **DealerClicks object pooling:** `PopulateDealerPanel()` and `PopulatePlayerPanel()` now reuse GameObjects via `dealerPool`/`playerPool` lists instead of Destroy/Instantiate. Added `InventoryItemUI.Teardown()` for clean event unsubscription. Files: `DealerClicks.cs`, `InventoryItemUI.cs`

### Tier 1 — Core Game Loop ("Why am I playing?")
- **Loan shark debt system:** $50k starting debt, 5% daily interest, 30-day deadline. `InitializeDebt()`, `ApplyDailyInterest()`, `PayDebt()` on `PlayerStats`. `OnDebtChanged` event for UI. File: `PlayerStats.Progression.cs`
- **Debt initialized** at character creation. File: `CharCreationUI.cs`
- **DebtManager:** Subscribes to `GameTime.DayChanged` to apply daily interest. Checks win (debt paid) / lose (day limit exceeded) conditions. Shows interest warning text. Optional pay debt button + input. **New file:** `DebtManager.cs`
- **Travel advances time:** +6 hours per trip via `GameTime.AddHours()`. Configurable `travelTimeHours`. File: `TravelManager.cs`
- **Market news ticker:** Checks boom/bust events and favorite drug demand on city load. Smooth right-to-left scrolling with auto-sized text, `RectMask2D` soft fade at edges. Loops forever by default. **New file:** `MarketNewsTicker.cs`
- **Debt & day display:** `debtText` and `dayText` added to `CityUIHandler` with reactive updates via `OnDebtChanged`. File: `CityUIHandler.cs`
- **Tabbed panel system:** Generic `TabbedPanel.cs` for switching between content panels (Stats / Inventory / Debt tabs in PlayerInfoPanel). Color tinting on active/inactive tabs. **New file:** `TabbedPanel.cs`

### Tier 2 — "One More Turn" Hooks
- **Equipment shop:** `EquipmentShop.cs` — buy better trenchcoats/weapons in-game. Trade-in system (50% of current gear cost). Per-city inventory via Inspector arrays. **New file:** `EquipmentShop.cs`
- **Gear rebalanced** for meaningful progression:

  | Trenchcoat | Cost | Slots | Armor |
  |---|---|---|---|
  | Tan | $0 | 3 | 2 |
  | Olive | $3,000 | 5 | 4 |
  | Grey | $8,000 | 8 | 7 |
  | Black | $20,000 | 12 | 10 |
  | Leather | $45,000 | 18 | 15 |

  | Weapon | Cost | Damage |
  |---|---|---|
  | Pocket Knife | $0 | 8 |
  | Handgun | $5,000 | 18 |
  | Shotgun | $18,000 | 35 |

- **Drug risk/reward profiles** — rebalanced existing drugs and added 3 new ones:

  | Drug | Base Cost | Supply | Sell Heat | Buy Heat | RiskTier |
  |---|---|---|---|---|---|
  | Marijuana | $30–50 | 40–60 | 1 | 0.4 | Safe (0) |
  | Shrooms | $60 | 45 | 2 | 0.8 | Safe (0) |
  | LSD | $120 | 30 | 3 | 1.2 | Medium (1) |
  | Ecstasy | $250 | 20 | 6 | 2.4 | Medium (1) |
  | Crack | $600 | 12 | 12 | 4.8 | Hard (2) |
  | Heroin | $1,200 | 6 | 18 | 7.2 | Hard (2) |

- **City info before traveling** — Floating preview card near the travel dropdown. Holds 3s then fades out. File: `TravelManager.cs`
- **Loan shark mid-game borrowing** — `BorrowFromShark(int amount)` on `PlayerStats.Progression.cs`. 20% premium, capped at $20,000 per transaction. Files: `PlayerStats.Progression.cs`, `DebtManager.cs`
- **Risk/reward system** — Three-tier drug risk model (Safe/Medium/Hard) driving cop encounter difficulty. Bribe cost, run chance, and combat loss all scale by risk tier. Hard drugs always arrest on search. Files: `Drug.cs`, `Item.cs`, `Cop.cs`, `CopEncounterUIManager.cs`, `HeatManager.cs`, `DealerClicks.cs`

### Tier 3 — Juice & Feel
- **Profit/loss feedback** — `ProfitLossPopup.cs` shows "+$X PROFIT" / "-$X LOSS" / "BREAK EVEN" on every sale. Scale-overshoot snap-in → hold → float-up fade-out. Files: `ProfitLossPopup.cs`, `Item.cs`, `DealerClicks.cs`
- **Net worth tracker** — `NetWorth` computed property on `PlayerStats.Economy.cs`. Displayed in `CityUIHandler`, reactive to wallet and inventory changes.
- **Run summary / high score** — `RunSummaryUI.cs` on GameOver and YouWin scenes. Originally showed net worth, cash, debt, days, cops encountered/busted, cities visited. **Expanded** — see "Endgame Stats & Leaderboard" below.

### Tutorial & Intro
- **Intro sequence** — `IntroSequence.cs` drives a 4-panel click-through narrative before CharCreation. Crossfades via `CanvasGroup`. `StartUIHandler` routes to `"Intro"` scene first. **New file:** `IntroSequence.cs`
- **In-game tutorial** — `TutorialManager.cs` shows a 5-step dialog on first play only (PlayerPrefs flag `TutorialSeen_v1`). Skip button available. **New file:** `TutorialManager.cs`
- **CharCreation → city fade** — `HandleContinue` is now a coroutine with `FadeController.FadeOut` before `SceneManager.LoadScene`. File: `CharCreationUI.cs`

### WebGL & Save System
- **WebGL-safe save/load:** Replaced all `System.IO` in `SaveLoadHelper.cs` with `PlayerPrefs` (JSON string under key `"DrugWarsSave"`). Works identically on WebGL, desktop, and mobile.
- **Save data fixed:** `SaveData.cs` now includes `debt` and `avgPurchasePrice` fields that were missing.
- **GameSessionManager save/load overhauled:**
  - Added `allTrenchcoats[]`, `allWeapons[]`, `playerSprites[]` Inspector arrays — used to resolve equipment and player portrait on load (previously only searched dealer inventories, missing starter gear).
  - `SaveGame()` writes player sprite index, debt, and all inventory `avgPurchasePrice`.
  - `LoadGame()` dynamically creates a `PlayerStats` GameObject if `Instance` is null (fixes NRE on main menu Continue).
  - `RestoreDayAfterSceneLoad` callback via `SceneManager.sceneLoaded` fixes `GameTime.Awake()` overwriting the loaded day back to 1.
  - Item SO registry: `SavedToItem()` now looks up the original Item SO by name to reconstruct `ItemInstance` with correct sprite, heat values, risk tier etc.
- **SaveGameButton.cs:** New MonoBehaviour — attach to any Button, wire `onClick` → `SaveGame()`. Optional `TMP_Text` feedback slot shows "SAVED!" for 1.5s. **New file:** `SaveGameButton.cs`
- **Inspector setup required:** `GameSessionManager` needs `allTrenchcoats`, `allWeapons`, and `playerSprites` filled in — same SOs/sprites as CharCreationUI and EquipmentShop.

### Economy Rebalance
- **Per-visit price randomization:** `Dealer.VisitMultiplier` (`[NonSerialized]`) is re-rolled ±20% each time a dealer panel opens in `DealerClicks.OnPointerClick`. Applied at the end of `GetModifiedBuyPrice` so both buy and sell prices fluctuate together. Files: `Dealer.cs`, `DealerClicks.cs`
- **Dealer sell ratios rebalanced:**
  - TJ: `sellPriceRatio` 0.337 → **0.55** (TJ's $50 Pot base now pays $27.50 in Baghdad vs $20 Milwaukee buy — weed trade is viable)
  - Mr. Wong: `sellPriceRatio` 0.50 → **0.65** (Crack: buy $322 Milwaukee, sell $487 Baghdad — 51% profit, worth Hard-tier heat)
- **Baghdad price modifiers added:** Daily volatility ±20%, 10% boom (×1.4), 8% bust (×0.6). Baghdad prices now fluctuate — good/bad days to sell. File: `Baghdad.asset`
- **Core trade loop (Milwaukee → Baghdad):**

  | Drug | Buy (MKE) | Best sell (BGD) | Profit |
  |---|---|---|---|
  | Marijuana | $20 | $27.50 (TJ) | 37% |
  | Shrooms | $32 | $42 (Daryl) | 31% |
  | LSD | $64 | $85 (Daryl) | 33% |
  | Ecstasy | $134 | $176 (Daryl) | 31% |
  | Crack | $322 | $487 (Mr. Wong, fav bonus) | 51% |
  | Heroin | $643 | $846 (Daryl) | 32% |

  Milwaukee has 0.44× drug buy multiplier (cheap source). Baghdad has no modifier (full base prices) + Crack favorite (+25%). Reverse route (BGD→MKE) is always a loss.

### Cop Encounter Fixes
- **`OnPlayerArrested` wired:** `HandleArrest()` now fires on every arrest. It increments `TimesCaughtByCops` (so `repeatsToHostile` escalation in `Cop.cs` actually works) and confiscates all drugs from player inventory. File: `CopEncounterUIManager.cs`
- **Arrest cash penalties:** Search arrest takes 20% cash before calling `HandleArrest`. Run-failure arrest takes 15%. Combat loss already took 25–45% before calling it — no double penalty.
- **`EndEncounter(bool success)` fixed:** Parameter now used — `success: true` fires `OnEscaped`, `success: false` fires `OnEncounterResolved`. File: `CopEncounterUIManager.cs`
- **Stuck state fixed:** Non-hostile run failure that doesn't search AND doesn't arrest (was 65% of that branch, did nothing) now calls `EndEncounter(success: true)` with a warning line — cop lets you off. File: `CopEncounterUIManager.cs`
- **Pre-rolled search outcome consumed:** `_searchPrerolled` flag set in `StartEncounter` when opening is Search. `PerformSearch()` uses `opening.searchResult`/`stealAmount` on first call instead of re-rolling; subsequent searches roll fresh. File: `CopEncounterUIManager.cs`
- **Haggle capped:** `_haggleCount` tracked against `maxHaggles` (default 3, Inspector-configurable). On the final haggle the button disables and dialogue says "Last chance." If exceeded, cop immediately escalates to search or combat. File: `CopEncounterUIManager.cs`
- **Run cooldown coroutine stopped in `EndEncounter`:** Prevents the coroutine re-enabling the Run button during the post-encounter fade window. File: `CopEncounterUIManager.cs`

### CityUI Prefab System
- **Editor tooling:** `Assets/Scripts/Editor/CityUIPrefabTool.cs` — menu items under **Drug Wars → Prefabs** to create, replace, wire, and validate shared UI prefabs across all 6 city scenes. **New file.**
- **Prefab targets (13 root objects):** InfoCanvas, FadeCanvas, ToastManager, DealerManager, HeatManager, DebtManager, TravelManager, MarketNewsTicker, EquipmentShop, TravelUIParent, CityUIHandler, DealerContainer, CityPreviewCard
- **Prefab location:** `Assets/Prefabs/CityUI/` — one `.prefab` per root object
- **Workflow:**
  1. **Step 1** — Open Milwaukee (source of truth), run "Save All CityUI Prefabs" to create/update prefabs
  2. **Step 2** — Open each other city scene, run "Replace With Prefab Instances"
  3. **Step 4** — Run "Auto-Wire Cross References" in each scene to reconnect cross-prefab SerializeField references
  4. **Step 5** — Run "Validate All City Scenes" to confirm all scenes use shared prefabs
  5. Save each scene after Steps 2–4 (Cmd+S)
- **Auto-wire handles all cross-prefab references:**
  - CityUIHandler → all InfoCanvas text/image fields + TravelManager
  - TooltipUI (on CityUIHandler GO) → TooltipCanvas RectTransform, inner TooltipPanel, DealerName/DealerDescription texts
  - HeatManager → HeatSlider, fill image, heat/status/risk texts (all in InfoCanvas/WalletPanel)
  - DebtManager → DebtText, DayCounterText, pay/borrow buttons and inputs (all in InfoCanvas/Content_Debt)
  - DealerManager → CityUIHandler, HeatManager, PlayerInventory content, DealerInfoPanel, StatusMessageText, asset prefabs (DealerItem, DealerPrefab), spawn points from MapCanvas
  - EquipmentShop → ShopPanel, OpenShopButton, CloseButton, ItemListContent, FeedbackText, CityUIHandler, ShopItemPrefab asset
  - TravelManager → TravelUIParent, Dropdown, Button, FareText, CityPreviewCard (child of TooltipCanvas) + preview texts
- **Per-city overrides** (set as prefab overrides in Inspector, not touched by prefab edits):
  - EquipmentShop: `trenchcoatsForSale`, `weaponsForSale` arrays
  - DealerSpawn point positions in MapCanvas (scene-specific layout)
  - TravelManager: `allCities` list (if cities differ)
- **Scene hierarchy notes:**
  - Dealer spawn points (`DealerSpawn1/2/3`) are children of **MapCanvas**, not DealerContainer. DealerContainer is an empty root.
  - CityPreviewCard is a child of **TooltipCanvas** (inside TooltipPanel prefab), not a root object
  - TooltipUI component lives on the **CityUIHandler** root GameObject (alongside the CityUIHandler script)
  - TooltipPanel prefab has two objects named "TooltipPanel" — root (Layer 0, just a Transform) and inner panel (Layer 5, child of TooltipCanvas, the actual UI panel that shows/hides)
  - CityUIHandler is a standalone root object with no children — all its UI references point to InfoCanvas children
  - **PlayerInventory** (ScrollRect) is the dealer-interaction player panel, populated by `DealerClicks`. **Content_Inventory** (inside TabbedPanel) is the Inventory tab populated by `InventoryTabUI` — these are different objects, don't confuse them

### Per-Run Market Randomization
- **`PriceService.RunSeed`:** Random int generated at character creation, included in all hash keys for `DailyVolatility`, `DailyEvent`, and `CityEventManager.GetEventForCity`. Each new playthrough gets unique boom/bust events, price volatility, and city events — previously the same day+city always produced the same results. Files: `PriceService.cs`, `CityEventManager.cs`, `CharCreationUI.cs`
- **Save/load support:** `RunSeed` stored in `SaveData.runSeed`, saved/loaded via `GameSessionManager`. File: `SaveData.cs`, `GameSessionManager.cs`

### Cop Encounter Loss Feedback
- **Itemized loss display:** Every cop encounter outcome now shows red-colored loss details below the cop dialogue via TMP rich text. File: `CopEncounterUIManager.cs`
  - Search steal: "$X confiscated"
  - Search arrest: "$X fine (20%)" + "Drugs seized: Marijuana x5, Crack x2"
  - Run arrest: "$X fine (15%)" + itemized drug list
  - Combat loss: "$X taken (25-45%)" + drugs seized (also in combat log)
- **`BuildDrugConfiscationList()`** captures drug names+amounts before `HandleArrest()` removes them
- **`FormatLossSummary()`** formats cash and drug losses into a `<color=#FF4444>` block appended to dialogue

### Bug Fixes
- **Player inventory scroll position:** PlayerInventory ScrollRect started scrolled to the bottom on city load due to stale oversized Content height (642px) baked in scene. `DealerManager.Start()` now resets `verticalNormalizedPosition = 1f` after one frame. Also added scroll-to-top in `DealerClicks.PopulatePlayerPanel()` for when a dealer is clicked. Files: `DealerManager.cs`, `DealerClicks.cs`
- **Dealer panel drug bleed-through:** Fixed pool clearing to wipe ALL children from `dealerInfoPanel`, not just the current dealer's tracked items. File: `DealerClicks.cs`
- **Equipment shop NRE on direct scene load:** Added null guards to `isOwned` checks and buy methods. File: `EquipmentShop.cs`
- **Equipment shop layout rebuilt:** Replaced prefab-based layout (broken nested Canvas) with fully code-built UI cards using `HorizontalLayoutGroup` + `VerticalLayoutGroup`. File: `EquipmentShop.cs`
- **Stale RuntimeInventory YAML:** Removed orphaned serialized data from all dealer `.asset` files.
- **Dealer panel not switching:** Added `static DealerClicks activeDealer` — only the exact instance that opened the panel can close it. File: `DealerClicks.cs`
- **FadeController rewritten:** No longer `DontDestroyOnLoad`. Each scene owns its own instance, starts black, auto-fades in on `Start()`. Every scene needs a `FadeController` wired up. File: `FadeController.cs`
- **TimeText not updating in non-Milwaukee cities:** `GameTime.AssignTimeText()` used `GameObject.Find("TimeText")` which returns null when the parent (`Content_Stats` inside `TabbedPanel`) starts inactive. Replaced with `Resources.FindObjectsOfTypeAll<TMP_Text>()` filtered by `SceneManager.GetActiveScene()`. Also writes the current time string immediately on assignment so there's no 1-frame stale gap after travel. File: `GameTime.cs`

### Dealer Restock System
- **Per-dealer restock interval:** New `restockIntervalDays` int field on `Dealer.cs` (default 3, Inspector-tweakable; 0 disables). Dealer inventories rebuild from their template `Inventory[]` when `currentDay - lastRestockDay >= interval`.
- **GameSessionManager hook:** Subscribes to `GameTime.DayChanged` lazily (via `SceneManager.sceneLoaded` since `GameTime.Instance` may not exist when `GameSessionManager.Awake()` runs). Tracks per-dealer last-restock day in `Dictionary<int, int> dealerLastRestockDay` keyed by SO instance ID.
- **Save/load support:** `SavedDealerState.lastRestockDay` persists the timer across saves. Old saves without the field default to 0 (graceful fallback — dealer restocks on next day-change after load).

### Bribe System Overhaul (Round 2)
- **Header text always reflects current ask:** `bribeAskText` now shows `"<b>{cop} demands ${X}</b>"` and is refreshed on every state change via `RefreshBribePanelUI()`. Previously stuck on the initial demand line during counter-offers (the "silent rejection" bug).
- **Live slider/input sync:** Added `bribeInput.onValueChanged` listener so the slider tracks typing in real time. Pay button reads from `bribeInput.text` first via `GetCurrentBribeOffer()` — typed values can no longer be silently lost when clicking Pay without blurring the field.
- **Patience cap on failed pays:** `maxFailedPayAttempts` (default 4, Inspector). After that many rejections the cop escalates to search/combat. Previously only haggle clicks counted.
- **Avoid repeated dialogue:** `RandomLine` takes an optional `avoidLine` param; `PickBribeLine()` tracks `_lastBribeLine` so back-to-back rejections show different text.
- **Overpay always accepts:** If `offer >= askAmount`, accept chance is forced ≥0.9 and the ask never increases on rejection. Fixes nonsense "I need a little more" line after overpaying.
- **Slider capped at player cash:** No more phantom slider movement past payable amounts. File: `CopEncounterUIManager.cs`

### Balance Tuning
- **Festival sell multiplier nerf:** `CityEventManager.FestivalSellMult` 2.0× → 1.4×. Crack on a Baghdad festival/boom day previously cleared $3k+; now caps around ~$1,375. File: `CityEventManager.cs`
- **No day-1 interest:** `PlayerStats.InitializeDebt()` no longer calls `ApplyDailyInterest()`. Player starts at exactly $50,000 debt instead of $52,500. Interest still kicks in on the first `DayChanged` event. File: `PlayerStats.Progression.cs`

### Cheat Menu (dev/test)
- **`CheatMenu.cs`** — auto-spawned at game start via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, no Editor wiring required. DontDestroyOnLoad. Press **Esc** anywhere to toggle.
- **Buttons:** `+ $10,000 CASH`, `DROP HEAT TO 0`, `QUICK START (skip intro)`, Close.
- **Quick Start:** bootstraps a default character (name "TEST", first trenchcoat/weapon/sprite from `GameSessionManager`), resets run stats, initializes debt, and loads Milwaukee directly — bypasses Intro and CharCreation.
- **`GameSessionManager` getters:** added `AllTrenchcoats`, `AllWeapons`, `PlayerSprites`, `AllCities`, `FindCityByName(name)` so the cheat menu can resolve defaults without rewiring SOs.
- **All UI is built from code** — Canvas + dimmer + panel + buttons all instantiated in `BuildUI()`. Sorting order 32000 puts it above all gameplay UI.
- **To exclude from release builds:** wrap the `Bootstrap()` method (and `Update()` ESC handler) in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

### Endgame Stats & Leaderboard
- **Per-run stats tracker** — `PlayerStats.RunStats.cs` partial class. Counters for money flow (sales revenue, drug spend, equipment, travel, interest, debt paid, fines, confiscation, combat loss, bribes, borrowed), drug stats (qty bought/sold, biggest single sale, favorite drug by qty), combat outcomes (wins, losses, escapes, successful bribes), peak heat, unique cities (`HashSet<string>`), day-debt-cleared, and total clicks. `ResetRunStats()` called from `CharCreationUI.HandleContinue` for every new run. **New file:** `PlayerStats.RunStats.cs`
- **Click counting** — `PlayerStats.Update()` increments `TotalClicks` on every `Input.GetMouseButtonDown(0)`. PlayerStats is `DontDestroyOnLoad` so this works across scenes.
- **Counter wiring:** `RecordDrugBuy/Sell` in `DealerClicks.cs`; `RecordEquipmentBuy` in `EquipmentShop.cs`; `RecordTravelSpend/CityVisited` in `TravelManager.cs`; `RecordInterestPaid/DebtPaid/Borrowed/DayDebtCleared` in `PlayerStats.Progression.cs`; `RecordBribePaid/Confiscated/FinePaid/CombatCashLoss/CombatWin/CombatLoss/Escape` in `CopEncounterUIManager.cs`; `RecordHeatSample` in `HeatManager.AddHeat`.
- **Leaderboard** — `Leaderboard.cs` static class. Top 10 entries by JSON in `PlayerPrefs["Leaderboard_v1"]`. Sort: wins above losses; within wins, fewest days (tiebreak by net worth desc); within losses, highest net worth. Both win and loss runs are submitted from `RunSummaryUI`. **New file:** `Leaderboard.cs`
- **`RunSummaryUI` rewritten** — single rich-text `statsBlockText` shows all stats grouped by section (TIMELINE / MONEY / DRUGS / HEAT & COPS / MISC) with color-coded values. Old per-stat `TMP_Text` fields kept as optional fallbacks. New fields: `statsBlockText`, `leaderboardUI`, `leaderboardRankText`, `mainMenuButton`, `playAgainSceneName`. Leaderboard entry submitted automatically; rank shown if it cracks the top 10.
- **`LeaderboardUI`** — two render modes: (a) `singleBlockText` mode renders the whole table into one TMP_Text using `<mspace>` for column alignment; (b) row-prefab mode instantiates `LeaderboardRowUI` instances under `rowContainer`. Highlights the just-inserted row. **New files:** `LeaderboardUI.cs`, `LeaderboardRowUI.cs`
- **Save/load** — `RunStatsSnapshot` added to `SaveData`. `GameSessionManager.SaveGame/LoadGame` capture/restore via `PlayerStats.CaptureRunStatsSnapshot/RestoreRunStatsSnapshot`. Old saves without runStats default-construct a snapshot of zeros.
- **No Inspector wiring required.** `RunSummaryUI` auto-spawns on YouWin/GameOver scene load via `[RuntimeInitializeOnLoadMethod]` + `SceneManager.sceneLoaded` callback, then auto-builds the entire UI from code in `EnsureUIBuilt()` — Canvas, headline, two-column stats/leaderboard, and Play Again / Main Menu buttons. `isVictory` is derived from the active scene name (`"YouWin"` → true, `"GameOver"` → false), so no per-scene checkbox toggling.
- **Override path:** if you wire any scene UI manually (e.g. drop a custom RunSummaryUI in the scene with `statsBlockText` set), the auto-spawn skips and your wiring is used instead.
- **Old `HighScore_NetWorth` PlayerPrefs key** — abandoned (not deleted). Safe to clear via `Leaderboard.Clear()` if needed; the game no longer reads or writes the old key.

---

## Known Issues / To Do

### Editor work required (cannot fix via code)
- **Belgrade has no dealers:** `Belgrade.asset` has null dealer slots. Need to assign Daryl + Mr. Wong in the Inspector, then add a `DealerManager` with spawn points to the Belgrade scene (copy from Milwaukee). Once done, add a drug `CityPriceModifier` with `buyPriceMultiplier: 1.1`, `dailyVolatility: 0.18` — Ecstasy favorite will then give ~80% profit on MKE→BGR route.

### WebGL build readiness
- Save/load: ✅ WebGL-safe (PlayerPrefs)
- Threading: ✅ None used
- Native plugins: ✅ None
- Async/await: ✅ None
- Resources.Load fonts: ⚠️ Verify font assets exist at `Assets/Resources/Fonts/`
- Belgrade: ⚠️ Scene needs editor setup before shipping

---

## Architecture Notes
- **PlayerStats** is a singleton (`DontDestroyOnLoad`) split across partial classes: `.cs`, `.Identity.cs`, `.Equipment.cs`, `.Economy.cs`, `.Progression.cs`
- **Items** use `ScriptableObject` templates (`Item`, `Drug`, `Weapon`, `Trenchcoat`) with `ItemInstance` runtime copies
- **Dealers** are ScriptableObjects with `RuntimeInventory` (List<ItemInstance>) managed by `GameSessionManager` at runtime. Restock state (`dealerLastRestockDay`) also lives in `GameSessionManager`, keyed by SO instance ID, persisted via `SavedDealerState.lastRestockDay`.
- **Price system:** `Dealer.GetModifiedBuyPrice()` chains: base cost × dealer mult × city COL × city buy mult × favorite drug mult × daily volatility × market event × visit multiplier (±20%)
- **Save system:** JSON serialized via `JsonUtility`, stored in `PlayerPrefs["DrugWarsSave"]`. Equipment resolved by name from `GameSessionManager.allTrenchcoats/allWeapons`. Item images reconstructed from SO registry on load.
- **Heat** triggers cop encounter at max (100). Decays via coroutine with cooldown. Cop encounters use `CopEncounterSeed` built from current `PlayerStats` state.
- **GameTime** fires `DayChanged` event → `DebtManager` applies interest → `PriceService.InGameDay` updates for deterministic daily prices. `GameSessionManager` also subscribes for dealer restock checks.
- **PriceService.RunSeed** randomizes all market events per playthrough. Without it, same day+city+item always produces same boom/bust/volatility. Set at char creation, persisted in save data.
- **GameTime.AssignTimeText():** `GameTime` is `DontDestroyOnLoad`, so the Inspector `timeText` reference can't be wired per-scene. The `AssignTimeText()` method (called from `Start` and `OnSceneLoaded`) finds the scene's `TimeText` GameObject via `Resources.FindObjectsOfTypeAll<TMP_Text>()` filtered by `SceneManager.GetActiveScene()`. Necessary because `GameObject.Find` doesn't return objects whose ancestors are inactive (e.g., when `Content_Stats` starts collapsed).
- **FadeController** must exist in every scene including CharCreation, Intro, GameOver, YouWin
- **CityUI Prefab System:** City scenes share 13 prefabbed root objects (managed via `CityUIPrefabTool.cs` Editor menu). Milwaukee is the source of truth. Cross-prefab references are wired automatically by Step 4 (Auto-Wire). Per-city differences (shop inventory, spawn positions) are stored as prefab overrides.
