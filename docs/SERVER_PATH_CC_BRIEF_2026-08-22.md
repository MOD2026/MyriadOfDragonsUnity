# Server path — CC brief (read this)

**MOS lock:** multiplayer must be **server-authoritative**. Client never owns Gold, ranks, construction time, Tower claims, guild, or match outcome.

## What you actually need (3 layers)

| Layer | Job | When |
|---|---|---|
| **A. Identity + API** | Login, account, validate Gold spends, construction timers, Tower daily/weekly keys, guild, leaderboards | Before any live competitive / monetised feature |
| **B. Realtime match** | Sync a live fight (placements, ticks, spells) between 2+ clients | When PvP ships — not needed for Campaign/Empire offline MVP |
| **C. Scale/ops** | Matchmaking, anti-cheat depth, regions, monitoring | After players exist |

Offline Campaign + Empire instant-Gold (current plan) does **not** need B or C.

## Most economic path (recommended)

**Do not** rent dedicated game servers first.

1. **Now → soft launch:** keep local Save; finish Campaign + Empire offline.  
2. **First paid cloud:** **Unity Gaming Services (UGS)** on the same Unity project you already use  
   - Authentication (you already started Social contracts)  
   - **Cloud Code** = trusted calendar, claim minting, construction complete, guild rules  
   - Cloud Save / Economy for wallet sync  
   - Cost: free tier → pay-as-you-go; cheapest way to get *authority* without hiring backend  
3. **Realtime PvP later:** **UGS Lobby + Relay + Netcode** *or* **Photon Fusion**  
   - Card battler = small messages per tick, not FPS bandwidth  
   - Start **1v1 Relay** (no always-on dedicated box)  
   - Server validates **match result + economy**; full tick sim on server only if cheating becomes real  

**Avoid until revenue:** custom AWS/GCP dedicated fleet, Kubernetes, hiring a backend team, building your own relay.

## Rough cost shape (order-of-magnitude)

| Stage | Monthly (low players) |
|---|---|
| Offline APK only | **$0** |
| UGS Auth + Cloud Code + Save (guild/Tower/construction trust) | often **$0–50** early; scales with API calls |
| Realtime 1v1 via Relay/Photon | often **tens–low hundreds $** at small concurrent; scales with CCU |

Exact $ depends on DAU/CCU — lock architecture first, not a big invoice.

## Decision you must make later (not today)

**PvP authority model:**  
- **(Cheap)** clients sim ticks + server signs result / spot-checks → OK for soft launch  
- **(Expensive)** every combat tick on server → only if ranked cheating is a real problem  

Default for ASAP: cheap model + UGS trust for economy.

## Build order (money + wartime)

Campaign 10 → Empire construction (offline A) → UGS Auth live → Cloud Code calendar/claims → Guild → then realtime PvP.
