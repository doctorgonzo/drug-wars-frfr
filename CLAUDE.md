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
- **Run summary / high score** — `RunSummaryUI.cs` on GameOver and YouWin scenes. Shows net worth, cash, debt, days, cops encountered/busted, cities visited. High score persisted via `PlayerPrefs`. **New file:** `RunSummaryUI.cs`

### Tutorial & Intro
- **Intro sequence** — `IntroSequence.cs` drives a 4-panel click-through narrative before CharCreation. Crossfades via `CanvasGroup`. `StartUIHandler` routes to `"Intro"` scene first. **New file:** `IntroSequence.cs`
- **In-game tutorial** — `TutorialManager.cs` shows a 5-step dialog on first play only (PlayerPrefs flag `TutorialSeen_v1`). Skip button available. **New file:** `TutorialManager.cs`
- **CharCreation → city fade** — `HandleContinue` is now a coroutine with `FadeController.FadeOut` before `SceneManager.LoadScene`. File: `CharCreationUI.cs`

### Bug Fixes
- **Dealer panel drug bleed-through:** Fixed pool clearing to wipe ALL children from `dealerInfoPanel`, not just the current dealer's tracked items. File: `DealerClicks.cs`
- **Equipment shop NRE on direct scene load:** Added null guards to `isOwned` checks and buy methods. File: `EquipmentShop.cs`
- **Equipment shop layout rebuilt:** Replaced prefab-based layout (broken nested Canvas) with fully code-built UI cards using `HorizontalLayoutGroup` + `VerticalLayoutGroup`. File: `EquipmentShop.cs`
- **Stale RuntimeInventory YAML:** Removed orphaned serialized data from all dealer `.asset` files.
- **Dealer panel not switching:** Added `static DealerClicks activeDealer` — only the exact instance that opened the panel can close it. File: `DealerClicks.cs`
- **FadeController rewritten:** No longer `DontDestroyOnLoad`. Each scene owns its own instance, starts black, auto-fades in on `Start()`. Every scene needs a `FadeController` wired up. File: `FadeController.cs`

---

## Architecture Notes
- **PlayerStats** is a singleton (`DontDestroyOnLoad`) split across partial classes: `.cs`, `.Identity.cs`, `.Equipment.cs`, `.Economy.cs`, `.Progression.cs`
- **Items** use `ScriptableObject` templates (`Item`, `Drug`, `Weapon`, `Trenchcoat`) with `ItemInstance` runtime copies
- **Dealers** are ScriptableObjects with `RuntimeInventory` (List<ItemInstance>) initialized at game start
- **Price system:** `Dealer.GetModifiedBuyPrice()` chains: base cost × dealer mult × city COL × city type mult × favorite drug mult × daily volatility × market event
- **Heat** triggers cop encounter at max (100). Decays via coroutine with cooldown.
- **GameTime** fires `DayChanged` event → `DebtManager` applies interest → `PriceService.InGameDay` updates for deterministic daily prices
- **GameTime.cs** has encoding issues — cannot be read by tooling, edits must use grep + targeted writes
- **FadeController** must exist in every scene including CharCreation, Intro, GameOver, YouWin
