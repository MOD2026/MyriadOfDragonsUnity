# Myriad of Dragons — Guild Competition & Rewards Specification v1

**Status:** Approved design reference under MOS v1.2. Implementation has not started.

**Purpose:** Define competition, rankings, rewards and temporary guild offices without turning
raw spending or permanent Power into a compounding reward monopoly.

## 1. Competition principles

1. Guild competition is part of the multiplayer foundation even while synchronous PvP is deferred.
2. Competition rewards participation, coordination, breadth and skilled completion—not purchases.
3. A leaderboard never awards points for spending Gems or real money.
4. Raw Power is visible for prestige but does not drive recurring functional rewards.
5. Milestone rewards are the main reward path. Rank rewards add status and modest bounded value.
6. The top functional reward band is deliberately flat; first place receives stronger visual
   recognition, not a permanent economic snowball.
7. Solo progression remains complete. Guild competition may accelerate ordinary progress but
   never owns an exclusive combat card, skill or permanent stat advantage.
8. All scores, eligibility, reward claims, appointments and buffs are server-authoritative.

## 2. Guild leagues and seasons

- Guild competition runs in 14-day seasons.
- Eligible guilds are placed into regional league groups of approximately 50 guilds, using prior
  league, active-member count and recent activity to avoid matching a new guild directly against
  a mature full guild.
- Launch leagues: Iron, Bronze, Silver, Gold and Dragon.
- The top 10% of a completed group is promoted; the bottom 10% is relegated. New or insufficiently
  active guilds remain in Iron until they meet eligibility requirements.
- A guild needs at least 10 eligible members and activity on at least 5 separate days during the
  season to receive ranked rewards.

### Guild Season Score

Guild Season Score is composed of:

- capped completion points from rotating guild-event objectives;
- capped personal contribution from up to the top 30 eligible members;
- a participation-breadth bonus, up to 20%, based on the share of eligible members who reach the
  personal event milestone;
- later, approved competitive-event results when those modes exist.

Power, purchases, chat volume and passive online time contribute zero season points. Using the top
30 capped contributors prevents roster size alone from deciding the result; the breadth bonus
still rewards leaders who mobilise more than a few high spenders.

## 3. Individual leaderboards

Leaderboards are separated so one account cannot dominate every form of achievement through Power.

| Board | Measures | Reward rule |
|---|---|---|
| Event Contribution | Capped event tasks completed and verified objective points. | Functional band rewards plus cosmetics. |
| Guild Contribution | Valid donations, helps and cooperative objectives under daily caps. | Milestones and guild-scoped recognition. |
| Combat Achievement | Eligible defeated units/enemies, weighted by difficulty/opponent quality and capped by best results. | No farming of friendlies, trivial encounters or repeated weak targets. |
| Builder | Meaningful construction completed during the period, bracketed by Empire tier. | Convenience rewards only. |
| Scholar | Meaningful individual research completed during the period, bracketed by Academy tier. | Convenience rewards only. |
| Power Showcase | Current verified account Power. | Display prestige only: emblem, profile frame, title and Hall of Fame. No recurring functional payout. |
| Lifetime Achievements | Firsts and cumulative milestones. | One-time badges/titles; never repeatedly farmed. |

“Highest Kill” is not a raw unbounded counter. Combat Achievement counts only eligible encounters,
weights stronger opposition or harder PvE content, caps repeated targets and uses the best limited
set of results per day. Exact combat scoring waits for the applicable competitive mode and battle
owner simulation.

## 4. Anti-repeat-winner reward structure

- Every event has accessible personal milestones independent of leaderboard position.
- Guild league placement provides a shared reward to every eligible contributor.
- Rank 1–10 receive the same functional reward package at launch.
- Rank 1, top 3 and top 10 receive different temporary frames, emblems, titles and profile effects.
- Rank 11–25 and percentile bands receive progressively smaller bounded packages.
- Previous winners remain eligible to compete; the game does not secretly handicap them.
- Repeated champions accumulate Hall-of-Fame stars and visual upgrades, not multiplying resource
  payouts or permanent combat stats.
- Rotating scoring categories and tier brackets create more routes to recognition than raw Power.
- A minimum personal-contribution threshold is required before a member can collect a guild rank
  reward, preventing inactive accounts from being carried entirely by others.

## 5. Seven-day appointed guild offices

After an eligible guild event, R4 and R5 may appoint qualified members to temporary offices. The
office lasts 7 days from server-confirmed appointment and grants a bounded personal progression
buff. These offices are separate from R1–R5 ranks.

| Office | Seven-day benefit | Intended behaviour |
|---|---:|---|
| Master Builder | +5% personal construction speed | Rewards construction/event contribution. |
| Royal Scholar | +5% personal research speed | Rewards research/event contribution. |
| Quartermaster | +5% ordinary resource production/gathering | Supports the guild economy without minting premium currency. |
| First Envoy | +1 simultaneous Embassy help request | Improves cooperation rather than combat power. |
| Guild Champion | Prestige emblem, animated frame and title; no combat-stat bonus | Gives the leading competitor a conspicuous status reward without economic snowballing. |

### Appointment rules

- Eligibility requires completion of the event's personal milestone and at least 3 active days in
  the event period. Each event may add a relevant performance threshold.
- R4 may appoint eligible R1–R3 members. R5 may appoint eligible R1–R4 members.
- Nobody may appoint themselves. R4 cannot appoint another R4.
- One account may hold only one temporary office at a time.
- One holder per office per guild. Buffs do not stack with a second copy of the same office.
- Appointment, replacement and expiry use server time and an append-only audit record.
- Replacing an office holder has a 24-hour cooldown and does not restart more than the remaining
  event-office window. This prevents constant buff swapping.
- Leaving the guild, removal, suspension or account archival immediately ends the office benefit.
- R5 may revoke an office for moderation or inactivity with a recorded reason. R4 may recommend
  revocation but cannot remove an appointment made by R5.
- Total social speed bonuses from guild research and temporary offices require a configured cap;
  the launch target is 20% before ordinary personal research or paid convenience is considered.
- Offices never grant direct card Attack/Health, PvP damage, matchmaking advantage, Gems, Event
  Tokens, Market Credits or exclusive cards/skills.

## 6. Guild and individual rewards

Allowed functional rewards:

- bounded Gold and ordinary resources;
- construction/research speed-ups;
- ordinary crafting materials;
- Guild Contribution;
- non-exclusive card/skill materials with a comparable solo route;
- guild cosmetics, banners, profile frames, titles and Hall-of-Fame marks.

Prohibited rewards:

- direct purchase-to-score conversion;
- exclusive competitive cards or skills;
- permanent combat-stat bonuses from rank;
- recurring material payouts for the Power Showcase;
- unlimited or uncapped faucets;
- rewards claimable without minimum personal participation;
- offensive effects that reduce another player's progression.

## 7. UI and art requirements

Required guild surfaces:

- Guild Overview: identity, league, season score, rank, announcement and active offices.
- Guild Members: R1–R5, contribution, office, last-active privacy state and management controls.
- Guild Research: active project, queue, donations and R4/R5 activation controls.
- Guild Help: individual requests and Help All.
- Guild Store: available contribution, lifetime contribution and purchase limits.
- Rankings: Guild League, Event Contribution, Guild Contribution, Combat Achievement, Builder,
  Scholar, Power Showcase and Lifetime Achievements.
- Hall of Fame: previous guild champions and permanent visual records.

Art direction:

- Ranks and offices require distinct silhouettes and iconography; do not rely on colour alone.
- League emblems progress from restrained Iron to ceremonial Dragon without changing the game's
  established dark-fantasy material language.
- Leaderboard frames must be readable at mobile avatar size and remain separate from runtime text.
- Rank 1/top 3/top 10 variants share one coherent frame family rather than unrelated art styles.
- Guild Champion may use restrained animation, glow or heraldic motion; it must not resemble a
  purchasable premium frame.
- All names, numbers, timers and rank labels remain runtime text for localisation.

## 8. Required tests and telemetry

- Score sources reject purchases, chat volume, passive presence and duplicate/replayed events.
- Per-account, per-target and daily caps are enforced using server time.
- Guild ranking uses the configured capped-member and breadth formulas.
- Ineligible/inactive members cannot claim guild placement rewards.
- Power Showcase never emits functional recurring rewards.
- Rank 1–10 functional rewards are equal where configured.
- Office eligibility, no-self-appointment, one-office limit, authority, expiry and guild-departure
  removal are enforced.
- Buff stacking respects the configured social-speed cap.
- Every score adjustment, reward and office action is auditable.
- Telemetry measures participation distribution, top-spender score share, repeat-winner rate,
  guild-size advantage, reward concentration and suspected farming.

## 9. Industry reference points

- Star Trek Fleet Command alliance tournaments combine alliance ranking, league promotion and
  personal-contribution rewards with limited daily tasks:
  https://scopely.helpshift.com/hc/pt/19-star-trek-fleet-command/faq/8002-alliance-tournaments/
- Marvel Strike Force uses recurring alliance seasons and alliance ranking rewards:
  https://scopely.helpshift.com/hc/en/46-marvel-strike-force/faq/5869-what-is-the-alliance-leaderboard/
- War and Peace alliance mobilisation exposes both alliance and personal rankings and gives R4/R5
  event-management powers:
  https://support.war-and-peace.com/support/solutions/articles/8000125068-alliance-mobilization-event
- Last War uses R4 officer specialisations and R5 leadership, supporting operational positions
  beneath the rank hierarchy:
  https://firstfungroup.zendesk.com/hc/pt-br/articles/45560184910355-Oficiais-da-Alian%C3%A7a
