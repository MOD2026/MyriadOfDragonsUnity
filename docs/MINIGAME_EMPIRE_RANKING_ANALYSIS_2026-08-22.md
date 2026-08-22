# Mini-game + Empire ranking — CC deep analysis (2026-08-22)

**Status:** Analysis only — **not locked**. Owner idea under review.  
**Canvas:** `minigame-empire-ranking`

## Owner proposal (summary)

- Hardcore + competitive ranking players strive for  
- Mini-games: **1 daily** + **1 weekly** refresh  
- Main mini-game: **Tower Defence** — daily clear reward; weekly = highest stage cleared  
- Tower strength **purely from Empire building stats**  
- Other small mini-games: not story-linked; also scale from buildings → drive Empire upgrades → rewards → beef deck  

## Verdict

| Question | Answer |
|---|---|
| Does the economy loop make sense? | **Yes** — classic midcore: invest base → event power → rewards → collection → stronger account |
| Workable with locked Empire? | **Yes with rules** — map Barracks→capacity, Castle→tower durability (separate from match-scale naming), Gate→which tower sector (meta, not DPS) |
| Pure Empire TD as *the* ranking? | **No for hardcore brand** — that ranks builders, not theorycrafters |
| Compare to top grossers? | Closest to **AFK Arena Tower / Raid Doom Tower / kingdom builders**, not Snap/Master Duel ladder |

## Recommended shape (if we adopt later)

1. **TD = Empire prestige ladder** (daily clear + weekly max floor)  
2. **Card prestige separate** (Campaign mastery now; PvP ladder later)  
3. **Reward caps** so weekly cannot finish a whale deck alone  
4. **At most 2 satellite mini-games**, same EmpirePower, different reward tables  
5. **Server required** for weekly ranks / daily reset integrity  
6. **Wartime:** after Campaign depth + Empire construction feasibility — TD is a second combat product  

## Loopholes to respect

- TD-only ranking alienates hardcore card players  
- Daily/weekly becomes the new gold faucet (must replace Normal Battle deliberately)  
- Client-side weekly high score = cheat  
- Too many Empire mini-games → one ROI mode, rest FAKE DEPTH  
- Full TD codebase is large vs wartime spine  

## Next (ChatGPT, docs only — when assigned)

Mini-game economy packet: EmpirePower formula, daily/weekly rules, reward caps, mapping Castle/Barracks/Gate → TD stats, what ranking boards exist (Empire vs card). Do not implement Unity TD yet.
