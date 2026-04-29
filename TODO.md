# Drug Wars FRFR — Fun & Polish TODO

## High Impact (change how the game feels immediately)

- [ ] **Sound effects** — Zero audio hooks exist anywhere. Cash register on sell, heartbeat as heat climbs, police siren on cop encounter. Single biggest gap in the whole project.
- [ ] **More cop personalities** — Only one `Cop.asset` (NewCop) exists. The personality system (corruption, violence, greed, diligence) is fully built but never used. Add 4–5 cop SOs: the dirty bribe-hungry cop, the by-the-book hardass, the lazy cop who just wants you gone.
- [ ] **Random one-off events** — Market is deterministic (same boom/bust per day seed). Add a small event system that fires once per day: tip-offs, robbery, confiscation, windfalls, city raids. Even 6–8 events would make every run feel different.
- [x] **Quick Sell All button** — Red "SELL ALL DRUGS" button at top of player inventory panel when a dealer is open. Sells all drugs at once with a single combined profit/loss popup.
- [x] **Price memory / last seen** — Player inventory shows sell price + per-unit profit/loss in green/red + avg paid. Dealer panel shows ▲/▼ arrow with "was $X" when price has shifted since last visit.

## Medium Impact (core loop depth)

- [ ] **Dealer reputation** — Sell to TJ ten times, get 5% better prices. Creates a reason to have a "main guy" and makes revisiting a city feel meaningful.
- [ ] **Unlock gating on equipment** — Shotgun and Black Trenchcoat are always visible in EquipmentShop. Gate them behind X cop encounters survived or $50k in lifetime sales. Makes gear a progression goal instead of a cash-permitting purchase.
- [x] **City events beyond boom/bust** — Lockdown (heat ×2, prices ×0.55), Festival (favorite drug sell ×2), Shortage (all drug prices ×1.8, heat ×1.3). All wired mechanically in Dealer.cs and DealerClicks.cs. Ticker leads with the event message.
- [ ] **Timed "hot tip" mechanic** — Random notification: "Word on the street — Crack shortage in Baghdad for the next 6 hours." Creates urgency and makes the time system matter beyond the debt countdown.
- [ ] **Belgrade needs dealers** — Belgrade has no dealers assigned (known issue in CLAUDE.md). Three cities with one broken is rough.

## Lower Impact / Good Polish

- [ ] **Drug stash at home base** — Let player store drugs in Milwaukee without carrying them. Risk/reward: avoid heat, but stash could get stolen.
- [ ] **Personal best leaderboard** — Run summary exists but only tracks one high score. Store top 5 runs (day reached, net worth, cops beaten) in PlayerPrefs. Gives replays a goal.
- [ ] **Hook or remove the Level property** — `PlayerStats.Progression.cs` has a `Level` field that's never set or used. Either wire it to something (every $10k profit = +1 level, unlock something) or delete it.
- [ ] **Loan shark enforcement** — Borrowing mid-game at 20% premium has no drama. Add a "shark goon" cop-style encounter tied to overdue or growing debt to make borrowing feel dangerous.
- [ ] **Visual city differentiation** — Cities probably look similar scene-to-scene. Even a background color tint or different ambient art per city makes travel feel like it matters geographically.
