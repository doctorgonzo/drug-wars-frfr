# Drug Wars FRFR — Design Backlog

Pending design ideas, ranked by impact-to-effort. Drawn from a research pass over single-player game theory (Sid Meier on "interesting decisions," Burgun on hidden information, Kahneman on loss aversion) and engagement fundamentals (MDA framework, Csikszentmihalyi flow, Nijman's "Juice it or Lose It" GDC talk, Skinner reinforcement schedules, Soren Johnson's "one more turn" essays).

---

## 1. Overlapping deadlines ("two clocks")
**Pitch:** Add a second visible countdown beside the loan-shark deadline. E.g. *"Heat investigation closes in 4 days"* or *"Special order expires in 3 days."* Multiple ticking clocks near completion stop the player from finding a natural quit point.

**Source insight:** Soren Johnson's Civ design notes — multiple unfinished, near-termination tasks > one large task. Same pattern in FTL's pursuing fleet, Slay the Spire's escalating elites, Balatro's blind levels.

**Effort:** Medium. New countdown UI element, generation logic, expiry consequences.

---

## 2. Juice — particle effects, number tweens, screen flash, hit feedback
**Pitch:** Coin-burst particles on every sale; wallet/heat numbers animate (count up/down, scale-pop); screen flash red on heat spike; satisfying impact on combat hits.

**Source insight:** Jan Willem Nijman / Kasper, "Juice it or Lose It" (GDC 2012). Same mechanics, with juice, feel ~2× better. Cheapest possible upgrade to perceived quality.

**Effort:** Low–medium. Code-only via a `JuiceFX` singleton.

**Status:** Implementing now.

---

## 3. Near-miss framing
**Pitch:** When a price moves against the player between buy and sell, surface *"YOU ALMOST CLEARED $1,200"* in red. When a cop search misses, show *"BUST MISSED YOU BY 2%."* When a bribe is rejected by a hair, *"COP ALMOST TOOK $X."*

**Source insight:** Kahneman & Tversky / prospect theory — loss aversion is ~2× gain seeking. Near-miss framing exploits this for compulsion (the slot-machine "almost won" effect).

**Effort:** Low. Surface existing data we already compute; track a few extra "what would have happened" branches in the cop encounter.

---

## 4. Special orders / dealer contracts
**Pitch:** Dealer NPC says *"Bring me 20 LSD in 3 days, I'll pay 2× market rate."* Player must commit to a route; tradeoff between the side hustle and the regular grind. Order can fail (no time, no inventory, busted en route) at a small reputation cost.

**Source insight:** Sid Meier's "interesting decisions" — meaningful choice with no dominant option. Forces evaluation across cash, time, heat, slot capacity simultaneously.

**Effort:** Medium-high. New data type, dealer UI surface, expiry handler, payout flow, optional reputation tracking.

---

## 5. Cop pattern detection (forced mixed strategy)
**Pitch:** If the player runs from cops 3+ times in a row, run-success drops sharply. Same for bribe (cops talk, the next one has been warned). The optimal play becomes mixing strategies — sometimes run, sometimes bribe, sometimes fight.

**Source insight:** Mixed-strategy theory adapted to single-player AI. When the system reads the player's pattern, the player must randomize. Without this, every encounter has a single dominant response.

**Effort:** Low. Track recent encounter resolutions on `PlayerStats`, decay over time/days, apply as a chance penalty.

---

## 6. Variable-ratio scratch finds
**Pitch:** ~5% chance on city arrival of finding $500–$5,000, an abandoned briefcase, a junkie's dropped bag. Random unpredictable rewards. No pattern, no farming.

**Source insight:** Skinner's variable-ratio reinforcement schedule. The most addictive reward pattern; what slot machines and roguelike loot rooms run on.

**Effort:** Low. Roll on `TravelManager` arrival; payout via existing `PlayerStats.PlayerWallet` or special drug drop.

---

## Recommended order
1. **Juice (#2)** — biggest perceived-quality jump per hour spent. *In progress.*
2. **Two clocks (#1)** — reshapes pacing across the entire run.
3. **Cop pattern detection (#5)** — small change, eliminates the dominant-strategy run-button-spam.
4. **Near-miss framing (#3)** — emotional stakes.
5. **Variable-ratio scratch (#6)** — drip-feed dopamine.
6. **Special orders (#4)** — biggest design surface, save for last.
