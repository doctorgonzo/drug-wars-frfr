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

### Tier 2 — "One More Turn" Hooks (continued)
- **City info before traveling** — Floating preview card near the travel dropdown. Shows destination city's population, cost of living (Low/Moderate/High/Very High), and hot drug + demand multiplier. Appears when the dropdown closes (including re-selecting the same city), holds for 3s then fades out over 0.5s. `CanvasGroup` alpha fade, cancels+restarts on rapid switching. Durations tunable in Inspector. Requires `CityPreviewCard` panel wired up in scene. File: `TravelManager.cs`
- **Loan shark mid-game borrowing** — `BorrowFromShark(int amount)` on `PlayerStats.Progression.cs`. Adds requested cash to wallet; adds `amount × (1 + borrowPremiumRate)` to debt (default 20% cut). Capped at `maxSingleBorrow` (default $20,000) per transaction. Both values tunable in Inspector under Loan Shark. `DebtManager.cs` gains `borrowButton`, `borrowAmountInput`, and `borrowInfoText` slots; borrow info text auto-populates with current terms. Reuses `interestWarningText` for borrow feedback. Files: `PlayerStats.Progression.cs`, `DebtManager.cs`
- **Risk/reward system** — Three-tier drug risk model driving cop encounter difficulty:
  - `Drug.RiskTier` (Safe/Medium/Hard) + `Drug.BuyHeatMultiplier` (0.4× default — buying quieter than selling). Both propagated to `ItemInstance`. Files: `Drug.cs`, `Item.cs`
  - `CopEncounterSeed.contrabandRiskLevel` (int 0–2) replaces the old bool — calculated from worst drug in inventory at heat-max. File: `Cop.cs`
  - Cop behavior scales by risk: bribe ×1.0/×1.5/×2.0, run chance −0/−15%/−30%, combat loss 25%/35%/45% of wallet. Hard drugs always arrest on search (no steal/escape). Files: `CopEncounterUIManager.cs`, `Cop.cs`
  - `HeatManager` calculates risk level from inventory; passes it to seed; `riskLevelText` (optional TMP_Text slot) shows CLEAN (green) / MED RISK (amber) / HIGH RISK (red). Heat fill bar idle color shifts from orange → amber → crimson based on what you're carrying. File: `HeatManager.cs`
  - Buy heat uses `BuyHeatMultiplier`; sell heat stays full. File: `DealerClicks.cs`

### Tier 2 — "One More Turn" Hooks
- **Equipment shop:** `EquipmentShop.cs` — buy better trenchcoats/weapons in-game. Trade-in system (50% of current gear cost). Per-city inventory via Inspector arrays. Brings panel to front on open (`SetAsLastSibling`). **New file:** `EquipmentShop.cs`
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

- **Drug risk/reward profiles** — rebalanced existing drugs and added 3 new ones. All values set in `.asset` files:

  | Drug | Base Cost | Supply | Sell Heat | Buy Heat | RiskTier |
  |---|---|---|---|---|---|
  | Marijuana | $30–50 | 40–60 | 1 | 0.4 | Safe (0) |
  | Shrooms | $60 | 45 | 2 | 0.8 | Safe (0) |
  | LSD | $120 | 30 | 3 | 1.2 | Medium (1) |
  | Ecstasy | $250 | 20 | 6 | 2.4 | Medium (1) |
  | Crack | $600 | 12 | 12 | 4.8 | Hard (2) |
  | Heroin | $1,200 | 6 | 18 | 7.2 | Hard (2) |

  New drug assets (need sprites assigned in Inspector): `Assets/Scriptable Objects/Drugs/Shrooms.asset`, `Ecstasy.asset`, `Heroin.asset`

### Bug Fixes
- **Dealer panel drug bleed-through:** Fixed `DealerClicks.ReturnDealerItems()` and `ReturnAllToPool()` to clear ALL `InventoryItemUI` children from the shared `dealerInfoPanel`, not just items tracked in the current dealer's map. Previously, switching dealers would show both dealers' drugs mixed together. File: `DealerClicks.cs`
- **Equipment shop empty when skipping startup:** `EquipmentShop.PopulateShop()` crashed with NRE when `PlayerStats.Instance` was null (direct scene load). Added null guards to `isOwned` checks and buy methods. File: `EquipmentShop.cs`
- **Equipment shop layout completely rebuilt:** `ShopItemPrefab` had a nested Canvas (Screen Space - Overlay) causing all items to render at the same absolute position. After multiple attempts to fix the prefab at runtime (stripping Canvas, repositioning children by anchor), replaced with fully code-built UI cards. Each card uses `HorizontalLayoutGroup` (Icon | Info column | Buy button) with a nested `VerticalLayoutGroup` for text labels (name, stats, cost). Added `ScrollRect` + `RectMask2D` on ShopPanel for scrolling. `ItemListContent` anchored to left 60% of panel, leaving right side free for additional text. Font sourced from prefab via `GetFont()`. File: `EquipmentShop.cs`
- **Stale RuntimeInventory YAML data:** Removed orphaned serialized `RuntimeInventory` data from all dealer `.asset` files (Daryl, Mr.Wong, TJ). This was leftover from when RuntimeInventory was a field; now it's a computed property.
- **Dealer panel not switching:** Clicking Dealer B while Dealer A's panel was open just closed it instead of showing Dealer B. Root cause: `OnPointerClick` checked `dealerInfoPanel.activeSelf` (shared panel), so any active dealer looked like a toggle-off. Fixed by adding a `static DealerClicks activeDealer` field — only the exact instance that opened the panel can close it; clicking a different dealer immediately repopulates and shows the new one. File: `DealerClicks.cs`

---

## TODO — Remaining Improvements

### Tutorial & Intro
- **Intro sequence** — `IntroSequence.cs` drives a click-through narrative panel screen before CharCreation. 4 panels: "Three weeks ago / The Deal / Your Options / Get to Work." Crossfades between panels via `CanvasGroup` alpha. Last panel's Next button fades out and loads `CharCreation`. `nextSceneName` configurable in Inspector. `StartUIHandler` now loads `"Intro"` (configurable via `newGameSceneName` field) instead of `"CharCreation"` directly. **New scene required:** `Intro` — add `FadeController` + wire `IntroSequence` component. **New file:** `IntroSequence.cs`
- **In-game tutorial** — `TutorialManager.cs` shows a 5-step dialog on first Milwaukee visit only. Steps: welcome/premise → dealers → heat → travel → debt. Fades in/out. Skip button available. Completion persisted via `PlayerPrefs` key `TutorialSeen_v1`. **New file:** `TutorialManager.cs` — add to Milwaukee (and optionally other city scenes).
- **CharCreation → city fade** — `HandleContinue` is now a coroutine; calls `FadeController.FadeOut` before `SceneManager.LoadScene`. Removed the stale `FadeIn` call that did nothing. File: `CharCreationUI.cs`

### Tier 3 — Juice & Feel
- **Run summary / high score** — `RunSummaryUI.cs` on GameOver and YouWin scenes. Shows: net worth, cash, debt, days, cops encountered/busted, cities visited. High score (best net worth on a win run) persisted via `PlayerPrefs`. `isVictory` bool in Inspector controls headline color/text. `TotalCopEncounters` and `CitiesVisited` added to `PlayerStats.Progression.cs`; encounter count incremented in `CopEncounterUIManager.StartEncounter()`, city count incremented in `TravelManager.TravelSequence()`. **New file:** `RunSummaryUI.cs`
- [x] **Profit/loss feedback** — `ProfitLossPopup.cs` shows "+$X PROFIT" (green) / "-$X LOSS" (red) / "BREAK EVEN" (yellow) on every sale. Uses `AvgPurchasePrice` on `ItemInstance` for accurate tracking (weighted avg on stack buys). Animation: scale-overshoot snap-in → hold → float-up fade-out via coroutine. Singleton pattern, requires a UI GameObject with CanvasGroup + TMP_Text. Files: `ProfitLossPopup.cs` (new), `Item.cs`, `DealerClicks.cs`
- [x] **Net worth tracker** — `NetWorth` computed property on `PlayerStats.Economy.cs` (wallet + inventory cost basis). Displayed via `netWorthText` in `CityUIHandler`, reactive to `OnWalletChanged` and `OnInventoryChanged`. Buying is net-zero; value changes on profitable/unprofitable sells.
- [x] **City info before traveling** — floating preview card on travel dropdown, fades out after 3s. File: `TravelManager.cs`

### Other Known Issues / Tech Debt
- `GameTime.cs` has encoding issues — cannot be read by tooling, edits must use grep + targeted writes
- ~~GameOver and YouWin scenes need to be created and added to Build Settings~~ ✓ Done
- ~~Equipment shop right panel (40% of ShopPanel width) is empty — reserved for future text/info display~~ ✓ Done
- ~~New drugs need to be added to dealer `Inventory` arrays on Dealer ScriptableObjects~~ ✓ Done
- ~~New drug assets (Shrooms, Ecstasy, Heroin) need sprites assigned in Inspector~~ ✓ Done
- ~~Inventory risk/reward spread~~ ✓ Done
- ~~Loan shark mid-game borrowing~~ ✓ Done
- **InventoryTabUI card positioning:** Fixed stale `offsetMin.x` pushing cards off-screen left (zero X offsets in `EnsureLayout`). Fixed cards clipping at top via `grid.padding` top = 34. File: `InventoryTabUI.cs`

---

## Architecture Notes
- **PlayerStats** is a singleton (`DontDestroyOnLoad`) split across partial classes: `.cs`, `.Identity.cs`, `.Equipment.cs`, `.Economy.cs`, `.Progression.cs`
- **Items** use `ScriptableObject` templates (`Item`, `Drug`, `Weapon`, `Trenchcoat`) with `ItemInstance` runtime copies
- **Dealers** are ScriptableObjects with `RuntimeInventory` (List<ItemInstance>) initialized at game start
- **Price system:** `Dealer.GetModifiedBuyPrice()` chains: base cost × dealer mult × city COL × city type mult × favorite drug mult × daily volatility × market event
- **Heat** triggers cop encounter at max (100). Decays via coroutine with cooldown.
- **GameTime** fires `DayChanged` event → `DebtManager` applies interest → `PriceService.InGameDay` updates for deterministic daily prices
