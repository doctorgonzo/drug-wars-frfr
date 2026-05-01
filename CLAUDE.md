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
- **Per-dealer restock interval:** New `restockIntervalDays` int field on `Dealer.cs` (default 2, Inspector-tweakable; 0 disables). Dealer inventories rebuild from their template `Inventory[]` when `currentDay - lastRestockDay >= interval`.
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
- **High-tier drug cost cuts:** Heroin base $1,200 → $800; Ecstasy base $250 → $167 (~33% reduction each). The favorite-drug-demand fix amplified high-tier drug returns; trimming the base costs keeps the late-game economy from trivializing the debt. Crack left at $100 — already nerfed and below Ecstasy's pre-cut value, so leave it alone. Files: `Assets/Scriptable Objects/Drugs/Heroin.asset`, `Ecstasy.asset`

### Decision-Density Pass
Four interlocking changes to make every turn matter more.

**Time pressure tightened.** `PlayerStats.Progression.cs` now hard-codes `dayLimit = 22` and `dailyInterestRate = 0.08f` inside `InitializeDebt()` so stale scene serializations (the older 30-day / 5%-interest values) can't override the current tuning at runtime. Players must move actual product, not grind safe weed for 30 days.

**Weapons influence non-combat cop interactions.** `Weapon.cs` adds three new stats — `RunSuccessBonus`, `BribeLeverage`, `PenaltyReduction` — read by `CopEncounterUIManager`:
  - `OnRunClicked` adds the bonus to the run-success roll.
  - `OnBribePayClicked` subtracts `BribeLeverage` from `cop.minBribeFraction` (a 9mm or shotgun gives the cop pause).
  - All three penalty paths (search-steal, search-arrest fine, run-arrest fine, combat cash loss) multiply by `(1 - PenaltyReduction)`.

  | Weapon | Damage | Run Bonus | Bribe Leverage | Penalty Reduction |
  |---|---|---|---|---|
  | Pocket Knife | 8 | 0% | 0% | 0% |
  | Handgun | 18 | +8% | -8% | -10% |
  | Shotgun | 35 | +15% | -15% | -20% |

  Surfaced in `EquipmentShop` via `BuildWeaponStatsLine()`.

**City heat memory.** New `PlayerStats.CityHeat.cs` partial. Selling raises a per-city heat value alongside the player's global heat. On arrival in a city via `TravelManager`, `ApplyCityHeatOnArrival` adds `cityHeat * 0.35` to the player's heat (with a "COPS REMEMBER YOU" toast). The value decays by 8/day via `GameSessionManager.HandleDayChanged`. Persisted in `RunStatsSnapshot.cityHeatNames/cityHeatValues`. The travel preview card surfaces it as `POLICE: QUIET / WARM / HOT / BURNING`. So camping one trade route is no longer free — the player has to rotate or eat the heat.

**Daily tip events.** New `DailyTip.cs` rolls one tip per in-game day (deterministic on `PriceService.RunSeed + InGameDay`, 65% chance a tip exists). Two tip types:
  - **DealBuy** — target city sells the target drug at 65–80% of normal price (applied in `Dealer.GetModifiedBuyPrice`).
  - **HotSell** — target city pays 125–145% on the sell side (applied in `Dealer.GetModifiedSellPrice` after sellRatio + favoriteDrugDemandMultiplier).

  `MarketNewsTicker` appends the tip headline to its rotation, so players see "TIP — Cheap LSD in Tokyo today, save ~25%" and have to decide whether the travel cost + days lost are worth chasing it. Tips are city-specific and one-day-only — miss the window and it's gone.

### Slot Capacity Overhaul
- **Slots now constrain volume, not just variety.** Previously a slot held one unique drug type and stack sizes were unlimited; players could win with the starter Tan trenchcoat (3 slots) by stockpiling 999+ units of two drugs. Now each drug has a `UnitsPerSlot` value (Drug.cs) and a stack consumes `ceil(amount / UnitsPerSlot)` slots.
- **Per-drug bulk** (lower = bulkier):

  | Drug | UnitsPerSlot |
  |---|---|
  | Weed | 50 |
  | Shrooms | 40 |
  | LSD | 30 |
  | Ecstasy | 20 |
  | Crack | 15 |
  | Heroin | 10 |

- **Per-Trenchcoat per-RiskTier multiplier** (Trenchcoat.cs `RiskTierCapacityMultipliers[3]`, indexed 0=Safe, 1=Medium, 2=Hard) — cheap coats penalize risky drugs harder, premium coats give a small bonus, so gear progression is meaningful for Crack/Heroin/Ecstasy specifically:

  | Trenchcoat | Safe | Medium | Hard |
  |---|---|---|---|
  | Tan | 1.0 | 0.7 | 0.5 |
  | Olive | 1.0 | 0.8 | 0.65 |
  | Grey | 1.0 | 1.0 | 1.0 |
  | Black | 1.0 | 1.1 | 1.15 |
  | Leather | 1.0 | 1.2 | 1.3 |

- **Resulting capacity** (slots × effective UnitsPerSlot, where effective = base × trenchcoat multiplier):

  | Trenchcoat | Slots | Weed cap | Ecstasy cap | Heroin cap |
  |---|---|---|---|---|
  | Tan | 3 | 150 | 42 | 15 |
  | Olive | 5 | 250 | 80 | 35 |
  | Grey | 8 | 400 | 160 | 80 |
  | Black | 12 | 600 | 264 | 144 |
  | Leather | 18 | 900 | 432 | 234 |

- **`PlayerStats.Economy.cs` API:**
  - `GetUsedSlots()` — sums `ceil(amount / UnitsPerSlot)` per drug stack.
  - `GetSlotCostForBuy(drugName, amountToAdd, unitsPerSlot)` — slot delta a hypothetical buy would add.
  - `GetMaxBuyableAmount(drugName, unitsPerSlot)` — clamp helper used by `DealerClicks.OnPlusClicked` to cap buys at remaining capacity.
- **Buy guard** in `DealerClicks.cs` now clamps to `GetMaxBuyableAmount()` and shows "Trenchcoat only has room for X more" instead of the old binary "No free slots!".
- **Tooltip surfaces bulk** — drug item tooltips append `"Bulk: N units per slot"` so players can plan loadouts.
- **Save/load:** `UnitsPerSlot` is restored from the drug template SO at load time, so old saves work without a save-format change. `Item.cs` `ItemInstance` carries the field at runtime; the constructor and copy-constructor both propagate it.

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

### Endgame UI Layout Fixes
Two bugs caught after first playtest of the auto-spawned RunSummaryUI:
- **Stats block was invisible.** The main `VerticalLayoutGroup` had `childControlHeight = false`, so it ignored each row's `LayoutElement.preferredHeight` and collapsed every child to Unity's default 100px sizeDelta. Headline and subhead happened to fit; the stats/leaderboard `Content` row was clipped to nearly nothing. Set to `true` (and the inner LB column's VLG too) so all rows render at their requested heights.
- **Wrong main-menu scene name.** `mainMenuSceneName` was a `[SerializeField]` defaulting to `"Start"` — but the actual scene is `"Startup"`. Existing scene serializations could pin it to a stale value. Replaced both `mainMenuSceneName` and `playAgainSceneName` with `private const` (`"Startup"` and `"CharCreation"`) so Inspector drift can't override.
- **Belt-and-suspenders:** Auto-built Canvas now spawns at scene root (not under `transform`) so an inactive parent can't hide it, and `sortingOrder` bumped to 1000 so any pre-existing scene UI doesn't paint over it. `EnsureUIBuilt` always runs (gated only against double-build), so a partially-wired legacy `RunSummaryUI` in the scene still gets the full layout.

File: `RunSummaryUI.cs`

### Drug SO Cleanup — Pot/Marijuana → Weed
- **Two stale dealer-inventory drug SOs** (`Assets/Scriptable Objects/Dealers/DarylInventory/Pot.asset` and `TJInventory/Pot.asset`) had `m_Name = Pot` but `Name = Marijuana` — so dealer panels showed "Marijuana" while the file on disk was "Pot". Both fields normalized to `Weed`. Files renamed to `Weed.asset` + matching `.meta`. The `.meta` GUIDs are preserved through `git mv`, so dealer `Inventory[]` references hold.
- **City `drugBonuses` entries** — Madison, Belgrade, and Toronto each had both a `Weed` AND a `Marijuana`/`Pot` entry with matching multipliers. Dropped the legacy duplicates.
- **`City.cs` tooltip** — example name updated `'Marijuana'` → `'Weed'`.

### JuiceFX — Visual Game-Feel Pass
- **`JuiceFX.cs`** — auto-spawned manager (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) that builds its own `ScreenSpaceOverlay` Canvas at `sortingOrder = 5000` and sits there providing visual flourishes to the rest of the game. No Editor wiring; all assets are procedural. **New file:** `JuiceFX.cs`
- **Procedural coin sprite** — `MakeCircleSprite()` generates a soft-edged circle into a `Texture2D` once at boot, then reused for all coin particles. No image asset needed.
- **Public API:**
  - `CoinBurstAtUI(RectTransform anchor, int count, Color tint)` / `CoinBurstAtScreen(Vector2 screenPos, ...)` — spawns pooled `Image`-based coin particles with random velocity + gravity + fade.
  - `FlashScreen(Color, duration, peakAlpha)` — full-screen Image fade in/out.
  - `NumberPunch(TMP_Text, scaleAmount, duration)` — quick scale-up-and-settle on a label.
  - `TweenIntegerText(TMP_Text, from, to, prefix, suffix, duration)` — eased count-up/down between integers (ease-out cubic).
- **Wired sites:**
  - `CityUIHandler` wallet, net worth, debt all tween + punch when their values change. Each tracks `_lastXxxDisplayed` and skips the tween on first call (so initial seed isn't a tween from 0).
  - `DealerClicks` Sell-All + per-item sell pop coins from the wallet display anchor (`CityUIHandler.WalletRect`). Coin count scales with the take. Gold tint on profit, red on loss.
  - `HeatManager.AddHeat` punches the heat label and red-flashes the screen on big jumps (alpha scales with `heatAmount / maxHeat`).

### ResetRunStats — Full New-Run Wipe
- **Bug:** PlayerStats is `DontDestroyOnLoad`, so the same instance carries from run to run. `ResetRunStats` originally only reset the per-run stat counters — wallet/inventory/heat/equipment/day all leaked from the previous run. A player ending a winning run with $50k would walk into "Play Again" still holding $50k.
- **Fix:** `ResetRunStats()` now does a full new-run wipe: `PlayerWallet → 10,000` (`DefaultStartingWallet` const), `inventory.Clear()` + `NotifyInventoryChanged()`, `CurrentHeat = 0`, `CurrentTrench`/`CurrentWeapon` nulled, `LastSeenBuyPrice` cleared, `Debt = 0` (then re-set by `InitializeDebt()` immediately after).
- **`GameTime.ResetToStart()`** — new method, fires `SetTime(startDay, startHour, ...)` with `invokeEvents: false` so listeners don't react to "day went from 22 back to 1." Called from `ResetRunStats`.
- **`GameSessionManager.ResetForNewRun()`** — calls `InitializeAllDealers()` to rebuild dealer runtime inventories from their templates. Called from `ResetRunStats` so leftover stock and stale restock timers from the last run don't carry over.
- **`PriceService.InGameDay = 1`** and **`DailyTipService.InvalidateCache()`** also reset so daily price hashes and the cached tip start fresh.
- **`CharCreationUI.Start`** now calls `ResetRunStats()` *before* reading `PlayerWallet` for the gear-affordability filter. Otherwise leftover cash leaked into "can I afford this trenchcoat?" and the player ended a "new" run still sitting on the previous wallet.

### Achievement System
- **`Achievement.cs`** — ScriptableObject (`Drug Wars/Achievement` create menu). Each SO defines one achievement entirely from the Inspector:
  - `Title`, `Description`, `Icon` (display)
  - `Stat` (enum: `PlayerWallet`, `NetWorth`, `TotalSalesRevenue`, `CombatWins`, `UniqueCitiesVisited`, `OwnsTrenchcoat`, `OwnsWeapon`, `DayDebtCleared`, plus ~20 others covering all RunStats and Progression counters)
  - `Comparison` (enum: `GreaterThanOrEqual`, `LessThanOrEqual`, `Equal`)
  - `Threshold` (float — the numeric target)
  - `RequiredItemName` (string — for `OwnsTrenchcoat`/`OwnsWeapon` stats, matches against `Item.Name`)
  - `CashReward` (int — bonus cash on unlock, 0 = none)
  - `IsSecret` (bool — hidden until unlocked), `SortOrder` (int)
  - `Id` is the SO asset name (used as persistence key)
- **`AchievementManager.cs`** — auto-spawned via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, `DontDestroyOnLoad`. No Editor wiring required.
  - Loads achievement list from `GameSessionManager.AllAchievements` (Inspector array on GSM, same pattern as `allTrenchcoats`/`allWeapons`)
  - Subscribes to `OnWalletChanged`, `OnInventoryChanged`, `OnDebtChanged`, and new `PlayerStats.OnRunStatRecorded` static event
  - On any stat change, evaluates all locked achievements against current `PlayerStats`; skips non-gameplay scenes (Startup/Intro/CharCreation/GameOver/YouWin)
  - Reentrancy guard (`_checking` flag) prevents recursive evaluation when a cash reward triggers `OnWalletChanged`
  - Toast queue drains sequentially with 3.5s spacing. Gold color `(1, 0.85, 0.2)`. Shows "ACHIEVEMENT UNLOCKED!\n{Title}" + cash reward if any. Coin burst via `JuiceFX` on cash rewards.
  - Persistence: `PlayerPrefs["Achievements_v1"]` stores JSON list of unlocked achievement IDs. Survives across runs (unlocked = permanent).
  - Public API: `IsUnlocked(Achievement)`, `IsUnlocked(string id)`, `UnlockedCount`, `TotalCount`, `GetAll()`, `GetUnlocked()`, `ResetAll()`, `ReloadAchievements()`
- **`PlayerStats.RunStats.cs`** — added `public static event Action OnRunStatRecorded`. Fired from every `Record*()` method after the stat changes.
- **`GameSessionManager.cs`** — added `Achievement[] achievements` SerializeField + `IReadOnlyList<Achievement> AllAchievements` getter.
- **Inspector setup:** Create Achievement SOs via right-click → Drug Wars → Achievement. Drag into `GameSessionManager.achievements` array. Example achievements:
  - "First Blood" — Stat: `CombatWins`, Comparison: `>=`, Threshold: `1`
  - "Globe Trotter" — Stat: `UniqueCitiesVisited`, Comparison: `>=`, Threshold: `6`
  - "Speed Run" — Stat: `DayDebtCleared`, Comparison: `<=`, Threshold: `10`
  - "Leather Up" — Stat: `OwnsTrenchcoat`, RequiredItemName: `Leather`

### Market Saturation + Sell-Price Cap
Two interlocking changes to fix the "buy 20 crack in Milwaukee, fly to Miami, win in one trip" exploit. Together they convert one-shot wins into 2–3 trip routes and make trenchcoat upgrades / multi-city play actually matter.

**Hard ceiling on sell price.** `Dealer.ComputeBaseSellPriceF()` clamps the post-multiplier sell price at `item.Cost × 3.0` for drugs. Stops the perfect-storm stack (boom + festival + favorite × 2.0 + hot tip × 1.45 + volatility × 1.2 on top of buy-side inflation) from running 6–10× base. Drugs only — equipment isn't event-multiplied so it doesn't need the cap. Constant `MaxSellPriceBaseMult` in `Dealer.cs`.

**Per-(city, drug) market saturation.** New partial `PlayerStats.MarketState.cs` tracks how flooded each city's market is for each drug. Selling N units bumps the saturation for that city+drug pair; saturation decays 40% per day. The displayed sell price reflects *current* saturation — the 1st unit fetches full price, the 20th fetches less. Per-unit bump scales with risk tier:

  | RiskTier | Drug | Bump/unit | Floor (mult=0.30) at |
  |---|---|---|---|
  | 0 (Safe) | Weed/Shrooms | 0.005 | ~200 units |
  | 1 (Medium) | LSD/Ecstasy | 0.015 | ~67 units |
  | 2 (Hard) | Crack/Heroin | 0.030 | ~33 units |

  Mult formula: `max(0.30, 1 - 0.7 × saturation)`. Decay: `× 0.6` daily (40% recovery). Floored at 0.30 so dumping a wagon-load of crack still pays *something* — just badly.

**Two pricing APIs on `Dealer`:**
  - `GetModifiedSellPrice(item)` — per-unit price at current saturation. Used for UI display + per-unit profit calc. Naturally drops as the player sells.
  - `GetSellRevenueForBatch(item, amount)` — total revenue for a batch sale, using the average mult between start and end saturation. The 20th crack is priced *lower* than the 1st within the same transaction, so even a single big "Sell All" sees diminishing returns.

`DealerClicks.SellAll` and `OnPlayerSellClicked` now call `GetSellRevenueForBatch` then `PlayerStats.BumpMarketSaturation(city, drug, qty, riskTier)`. The bump grows the saturation so the *next* sale (same day, same city, same drug) is more penalized.

**Reset / persistence:**
  - `PlayerStats.ResetMarketState()` called from `ResetRunStats()`.
  - `GameSessionManager.HandleDayChanged` calls `DecayMarketSaturation` next to `DecayAllCityHeat`.
  - `RunStatsSnapshot.marketSaturationKeys/Values` parallel lists persist saturation across save/load (same pattern as cityHeat).

**News ticker feedback.** `MarketNewsTicker.CheckAndShowEvents` was rewritten to rebuild its message list every loop iteration (was a one-shot at scene load). New `AppendSaturationMessages` adds a tier-coded line per (current city, drug) where saturation has crossed a threshold:
  - `≥ 0.4` → `<color=#FFD700>SLOWING</color> — buyers getting picky`
  - `≥ 0.7` → `<color=#FF8800>SATURATED</color> — prices tumbling`
  - `≥ 1.0` → `<color=#FF4444>FLOODED</color> — buyers paying scraps`

Player sees these within ~one full ticker cycle of selling, so the price drop has narrative cover. Ticker panel auto-hides during silent periods (no events, no tips, no saturation) and re-activates when a message appears. Required new `PlayerStats.AllMarketSaturation()` enumerable + `TryParseMarketKey` static helper to walk the saturation dictionary from outside.

**Sample math** — 20 crack at perfect-storm stacked Miami, no prior sales:
  - Pre-fix: ~$2,500/unit avg → $50,000 revenue → debt cleared in one trip.
  - With cap only: $1,800/unit cap → $36,000 revenue → 70% of debt in one trip.
  - With cap + saturation (avg mult ~0.79 across the batch): ~$1,422/unit avg → $28,440 → 57% of debt. Player needs at least one more trip — and saturation in Miami is now 0.6, so the next 20 crack sale tomorrow there starts at mult 0.36, forcing a different city.

### Design Backlog
`balance.md` at the project root captures pending design ideas not yet implemented, drawn from a research pass over single-player game theory and engagement fundamentals. Six ideas ranked by impact-to-effort: overlapping deadlines / two clocks, juice (in progress), near-miss framing, special orders / dealer contracts, cop pattern detection (forced mixed strategy), variable-ratio scratch finds. Each entry has a one-line pitch, the source insight, and an effort estimate.

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
- **PlayerStats** is a singleton (`DontDestroyOnLoad`) split across partial classes: `.cs` (Awake + singleton), `.Identity.cs` (name, sprite), `.Equipment.cs` (trenchcoat, weapon), `.Economy.cs` (wallet, inventory, slot math, slot helpers), `.Progression.cs` (heat, debt, day-limit, cop counters), `.RunStats.cs` (per-run counters, click tracking, leaderboard snapshot, **`ResetRunStats()` does the full new-run wipe**), `.CityHeat.cs` (per-city heat memory, decay, arrival application), `.MarketState.cs` (per-city per-drug saturation that diminishes sell prices on bulk dumps).
- **Items** use `ScriptableObject` templates (`Item`, `Drug`, `Weapon`, `Trenchcoat`) with `ItemInstance` runtime copies
- **Dealers** are ScriptableObjects with `RuntimeInventory` (List<ItemInstance>) managed by `GameSessionManager` at runtime. Restock state (`dealerLastRestockDay`) also lives in `GameSessionManager`, keyed by SO instance ID, persisted via `SavedDealerState.lastRestockDay`.
- **Price system:**
  - **Buy:** `Dealer.GetModifiedBuyPrice()` chains: base cost × dealer mult × city COL × city buy mult × daily volatility × market event × visit multiplier (±20%) × city event (Lockdown/Shortage on drugs) × **daily tip `DealBuy` mult** (when today's tip points at this city + drug).
  - **Sell:** `Dealer.GetModifiedSellPrice()` = buy price × dealer sellRatio × **`favoriteDrugDemandMultiplier`** (when item matches the city's `FavoriteDrug`) × per-drug `drugBonuses` × `FestivalSellMult` (when a Festival event is rolling on the favorite drug) × **daily tip `HotSell` mult** (when today's tip points here), then **clamped to `item.Cost × 3.0` for drugs**, then multiplied by **current market saturation mult** (per city, per drug). For a multi-unit batch, use `GetSellRevenueForBatch(item, amount)` which averages the saturation mult across the bump curve.
  - **Note:** `favoriteDrugDemandMultiplier` lives on the SELL side. It used to be on the buy side, which inflated buy prices in the favorite-drug city while sellRatio cancelled out the boost on sell — making "2.0x demand" cities pay-as-usual to sell into and 2× expensive to source from. Moved to sell so the UI label ("2.0x demand") translates to a real 2× sell-price boost.
- **Daily tip events:** `DailyTipService.GetTodaysTip()` deterministically rolls one tip per `(RunSeed, InGameDay)` (65% chance of any tip). `DealBuy` discounts a target city's buy price for a target drug; `HotSell` premiums a target city's sell price. `MarketNewsTicker` shows the headline. Cache invalidated on `ResetRunStats`.
- **City heat memory:** `PlayerStats.CityHeat.cs` tracks per-city heat in a `Dictionary<string, float>`. Selling bumps it via `BumpCityHeat(cityName, heatAmount)` (called from `DealerClicks` SellAll and per-item sell). `GameSessionManager.HandleDayChanged` decays all entries by 8/day. `TravelManager` calls `ApplyCityHeatOnArrival` on travel — boosts player heat by `cityHeat × 0.35`. Persisted via `RunStatsSnapshot.cityHeatNames/cityHeatValues`.
- **Save system:** JSON serialized via `JsonUtility`, stored in `PlayerPrefs["DrugWarsSave"]`. Equipment resolved by name from `GameSessionManager.allTrenchcoats/allWeapons`. Item images reconstructed from SO registry on load.
- **Heat** triggers cop encounter at max (100). Decays via coroutine with cooldown. Cop encounters use `CopEncounterSeed` built from current `PlayerStats` state.
- **GameTime** fires `DayChanged` event → `DebtManager` applies interest → `PriceService.InGameDay` updates for deterministic daily prices. `GameSessionManager` also subscribes for dealer restock checks.
- **PriceService.RunSeed** randomizes all market events per playthrough. Without it, same day+city+item always produces same boom/bust/volatility. Set at char creation, persisted in save data.
- **GameTime.AssignTimeText():** `GameTime` is `DontDestroyOnLoad`, so the Inspector `timeText` reference can't be wired per-scene. The `AssignTimeText()` method (called from `Start` and `OnSceneLoaded`) finds the scene's `TimeText` GameObject via `Resources.FindObjectsOfTypeAll<TMP_Text>()` filtered by `SceneManager.GetActiveScene()`. Necessary because `GameObject.Find` doesn't return objects whose ancestors are inactive (e.g., when `Content_Stats` starts collapsed).
- **FadeController** must exist in every scene including CharCreation, Intro, GameOver, YouWin
- **CityUI Prefab System:** City scenes share 13 prefabbed root objects (managed via `CityUIPrefabTool.cs` Editor menu). Milwaukee is the source of truth. Cross-prefab references are wired automatically by Step 4 (Auto-Wire). Per-city differences (shop inventory, spawn positions) are stored as prefab overrides.
- **Auto-spawned, code-built UI managers** (no Editor wiring required, all bootstrapped via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and persisted with `DontDestroyOnLoad`):
  - **`CheatMenu`** — Esc toggles a debug overlay with `+ $10k`, `Drop heat`, `Quick Start`. Sorting order 32000.
  - **`JuiceFX`** — coin particles, screen flash, number tweens, number punches. Sorting order 5000. Procedural circle sprite for coins. Pool capped at 256 instances.
  - **`RunSummaryUI`** — auto-spawns on YouWin/GameOver scene load and builds the full endgame screen + leaderboard from code if no hand-wired instance is in the scene. `isVictory` derived from active scene name.
  - **`AchievementManager`** — evaluates Inspector-defined `Achievement` SOs against `PlayerStats` on every stat change. Unlocks persisted in `PlayerPrefs["Achievements_v1"]`. Gold toast + optional coin burst on unlock.
  - All four live independently — they don't reference each other and can be removed individually with no cascade.
- **Slot capacity** is two-dimensional: `Drug.UnitsPerSlot` is the per-drug bulk; `Trenchcoat.RiskTierCapacityMultipliers[3]` scales it by the drug's `RiskTier`. Effective per-slot capacity = `UnitsPerSlot × Trenchcoat.GetCapacityMultiplier(riskTier)`. `PlayerStats.GetEffectiveUnitsPerSlot(item)` is the single source of truth for slot math.
- **`ResetRunStats()` is the canonical "new run" entry point.** Called from `CharCreationUI.Start` (early, before the gear-affordability filter), `CharCreationUI.HandleContinue` (idempotent), and `CheatMenu.QuickStart`. Wipes wallet → `$10,000`, inventory, heat, equipment, debt, `LastSeenBuyPrice`, all per-run stat counters, all city heat. Also resets `GameTime` to start, `PriceService.InGameDay = 1`, dealer runtime inventories (via `GameSessionManager.ResetForNewRun()`), and the `DailyTipService` cache.
