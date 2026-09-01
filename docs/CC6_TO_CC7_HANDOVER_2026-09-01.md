# CC6 → CC7 Command Centre Handover

**Date:** 2026-09-02  
**Project:** Myriad of Dragons Unity  
**Purpose:** Current authority snapshot for the next Command Centre. This document supersedes stale coordination summaries for the items listed below; historical reports remain evidence only.

## Operating protocol

- CC assigns work, consolidates reports, and keeps design/reference work separate from Unity implementation.
- Every UI reference must name its exact Unity implementation owner. A generated image is not an implementation commit.
- Any new or replacement art must be shown to Zihan before approval or implementation. Never promote generated, guessed, quarantined, or untracked art as approved.
- Gameplay/context questions go to BS; art identity/approval goes to AD. Do not invent mechanics, copy, values, assets, or ownership.
- Do not issue circular audits or rerun unchanged tests. If a room has no safe, meaningful task, record that explicitly.
- MT is marketing-only and must not receive Battle/UI design or implementation work.
- Preserve peer work and the dirty shared checkout. Do not blanket-stage, reset, clean, or overwrite another room's changes.

## Current Battle direction — latest owner instruction

The preferred Battle scene is a single gameplay composition based on the supplied sample, not an acceptance sheet:

- Player exact 3×3 deployed formation.
- Approximately four partial, non-identifying enemy fan/hand cards; no enemy 3×3 formation or deck stack.
- Enemy avatar at the top-right, clear of the lower control band.
- Player avatar away from the lower control band.
- Both avatars with readable numeric current/max health and status indicators.
- Face-up player hand in a readable fan.
- A curved/arc skill dock near a lower corner for quick tapping. The dock binds runtime `Spellbook.Count` and shows at most four SpellEffect icons; it must not invent active skills, show empty placeholders, or assume a fixed loadout.
- Owner-approved override: develop a non-linear deck progression from approximately 7 cards to a maximum of 15 cards. The target end-state is 9 formation cards plus up to 6 reserve cards, from which the player selects 3 one-tap reinforcement hotkeys. Barracks remains the progression owner unless MS/owner explicitly changes that decision.
- Owner-approved combat economy: Resource is used for formation only; Energy is shared by spells and reinforcement. Reinforcement is optional, available at ticks 4 and 8, executes with one tap, selects uniformly among legal lanes using the match seed with no rerolls, and fails as a strict no-op. Exact progression milestones, reserve generation, duplicate handling, and migration are CR7/MS implementation tasks under this direction.
- No sacrifice/replacement mechanic. `END TURN` and `AUTO BATTLE` are omitted. Reposition remains explicit; one-tap random targeting remains spell-only.
- One-tap random-target spells have no lane picker. Reposition remains the explicit targeting exception.
- End Turn, resources, turn/timer, targeting cue, and right-side Combat FX remain distinct and readable.
- No `[runtime]` tokens, fabricated values, unsupported copy, enemy tactical identity, or competing acceptance panels.

`UI-090` through `UI-100` are superseded for scene review. `UI-101` is now the latest conditional reference: `battle_faithful_sample_replacement_v11.png` plus `battle_faithful_sample_replacement_v11_lower_control_crop.png`. It restores exactly four real SpellEffect skill icons on the left, keeps three card-derived reinforcement hotkeys in the lower-right safe zone, and uses one compact curved energy ribbon from the selected hotkey to the already-resolved automatic lane. It preserves the player 3×3, enemy avatar/health, limited enemy fan, and no END TURN/AUTO BATTLE/sacrifice/replacement. It is still a review reference, not implementation approval. Every new image must be shown to Zihan before implementation. The reinforcement rail remains conditional until CR7/MS closes the milestone and reserve-generation details.

## Ownership map

| Surface / responsibility | Owner | Current state |
|---|---|---|
| Battle Unity implementation, targeting, hand, skills, Combat FX | CR7 | Existing spell one-tap logic is complete; implement the owner-approved reinforcement economy/progression only after exact details are signed. |
| Battle UI references | UI/UX | UI-101 v11 and its lower-control crop are the latest conditional references; show them to Zihan for approval. UI generates references only and does not approve mechanics or bind production UI. |
| Chat Unity implementation | VS/Social | Integrated at `5e59e27a`; AN independently confirmed 35/35. |
| Chat native/accessibility evidence | VE | Still outstanding. |
| Friends Unity implementation | FR | Integrated on LK line at `56c098a6`; prior focused evidence 11/11. |
| Friends UGS/live verification | Zihan / BE deployment path | Not proven live; requires deployment, backfill, and real gift verification. |
| StoryOverlay and Campaign story entry | WH | Story animation and StoryOverlay work closed at `9c1e427d`; Campaign art authority remains separate. |
| Home implementation | MS/Metagame boundary | AD-015/BS-028 beta context decisions are closed; exact code owner must be recorded before editing `HomePagePresenter.cs`. |
| Daily Login/Quests and Bazaar contracts | MS + BE | Daily Login is gated out of beta by BS-031; Bazaar remains omitted until its contract/deployment is ready. |
| Branch integration | LK | Receives CR/BE chains and reports exact integrated HEAD. |
| Independent verification | AN | Verifies only exact integrated HEADs; no substitutions. |
| Copy authority | ST | Supplies source-backed/localized strings only. |
| Gameplay/context decisions | BS | Resolves mechanics and beta disposition. |
| Art approval | AD | Approves or rejects candidate art. |
| Marketing | MT | Marketing catalog/provenance only; no UI/Battle design. |

## Closed decisions

- Home beta backdrop: use approved `Assets/Resources/UI/Backdrops/Zihan_City_NO NAMES.jpg`.
- Home avatar frame reuse: use actual `Assets/Resources/UI/Frames/Avatar_Circle_Frame.png`; the `_V1` filename does not exist.
- Home fourth currency and Alliance/House: deliberate beta omissions.
- Bazaar live trading: omitted from beta claims.
- Daily Login/Quests: gated out of beta; client-local entitlement claims are not an online entitlement contract.
- One-tap random targeting: deterministic among valid occupied targets; empty set is a no-op; Reposition remains explicit.
- CombatResolutionStage: adapt as the right-side feedback stage; no competing bottom rail.
- Chat: approved bubble treatment and present-but-disabled composer labeled `Chat unavailable`; unsupported copy remains omitted.
- Story animation: authorized 220ms linear fade, no interruption, skip, or speed multiplier.

## Current real blockers

1. **Battle UI approval:** UI-101 is still conditional. Zihan must approve V11 and the lower-control crop before CR7 binds production UI; do not treat any review PNG as implementation approval.
2. **Reinforcement/deck override:** Owner approved the direction: 3 selected hotkeys, one-tap optional reinforcement, shared Energy, uniform seeded-random legal-lane selection, no rerolls, and a 7→15 non-linear deck progression target. CR7/MS must define milestones, reserve generation, duplicate policy, migration, cost, and seed details before coding.
3. **Specification authority:** the owner override changes the live 10–20 deck-cap direction. CR7/MS/BS must publish a versioned mechanics/spec amendment (including old rule, new rule, rationale, affected systems, and migration) before production code claims the 7→15 rule is locked; MOS remains higher authority until amended.
4. **Native evidence:** VE lacks a current approved runtime capture set for Battle, Chat, Friends, and other surfaces. Review PNGs and EditMode tests are not native evidence.
5. **UGS deployment:** Friends code is complete, but deployment/backfill/live gift verification remains outstanding and requires Zihan's Dashboard access.
6. **Daily Login contract implementation:** BS-031 gates it out of beta; BE/MS must provide contractVersion 1 and a neutral gate before re-enablement.
7. **Campaign art authority:** current Campaign candidate boards are not approved. Do not implement candidate art as production art.
8. **Optional Combat FX art:** tome/glyph/projectile assets remain absent; do not invent substitutes. Existing Impact, Shield, audio, and right-side stage work can proceed without them.

## Integration state

- CR Battle work: `cd626ff` → `9013d90` → `2be12b6` → `419f168` → `e3595cb` → `99cb0f9`; focused results reported through 98/98. This is the prior Battle chain; CR7 must produce a separate implementation/reception chain for the owner-approved reinforcement/deck override.
- BE privacy closures include `d94ed80`, `13ce45c`, `a54c698`, `e2de263`, `1d273e92`, `b1b8d2d`; BE reports complete focused server/client coverage. LK reception is still required for the current line.
- Chat: integrated `5e59e27a`; AN-020 confirmed 35/35, 0 compiler errors.
- Friends: integrated `56c098a6`; previous focused evidence 11/11, 0 compiler errors.
- The shared checkout is dirty with peer changes and generated evidence/import artifacts. Preserve them; do not interpret the dirty state as a clean release branch.

## CC7 first actions

1. Read this handover, `docs/MOS_v1.1.md`, `docs/AI_CONTRIBUTING.md`, and the latest `docs/LOCKED_DECISIONS_REGISTER.md`.
2. Record UI-101 V11 and its lower-control crop as conditional and obtain Zihan's explicit approval/rejection; if rejected, ask UI for one targeted revision only. Do not start another generic Battle-art round.
3. Route CR and LK to receive the approved Battle composition and integrate the CR chain; route AN to verify only the resulting exact HEAD.
4. Route BE/MS on the Daily Login contract/gate and keep the system excluded from beta until complete.
5. Route VE to the current evidence matrix; separate native evidence from review references and do not claim PASS without native artifacts.
6. Keep FR/VS code-complete unless a new concrete runtime defect appears.
7. Never assign Battle/UI work to MT.

## Paste-ready CC7 prompt

> You are CC7 for Myriad of Dragons Unity. Use `docs/CC6_TO_CC7_HANDOVER_2026-09-01.md` and `docs/CC6_TO_CR7_BATTLE_HANDOVER_2026-09-02.md` as the authority snapshot. The owner-approved Battle direction is: non-linear 7→15 progression under Barracks ownership; 9 formation cards plus up to 6 reserve cards; 3 player-selected one-tap reinforcement hotkeys; Resource for formation only; shared Energy for spells and reinforcement; optional reinforcement at ticks 4/8; uniform seeded-random legal-lane selection with no rerolls; no END TURN/AUTO BATTLE/sacrifice/replacement. CR7/MS/BS must first publish a versioned spec/MOS amendment covering milestones, reserve generation, duplicate policy, migration, cost mapping, and seed/replay details; do not silently claim the old 10–20 rule is replaced. UI-101 V11 and its lower-control crop are the latest conditional Battle references and must be shown to Zihan before implementation; UI generates art references, while AD is the separate art-approval room. CR7 implements only after the contract and reference are approved; VE owns native evidence; MT is marketing-only. Keep VS/FR code-complete unless a concrete defect appears. Do not repeat completed screens, invent mechanics/assets/copy, or claim readiness from review PNGs or EditMode tests.

## Audit notes for CC7

- Historical reports `CC6-BATTLE-REINFORCEMENT-DOCK-CONTRACT-034`, `CC6-BATTLE-REINFORCEMENT-ECONOMY-BALANCE-036`, `CC6-BATTLE-REINFORCEMENT-CONFLICT-RECONCILIATION-039`, `CC6-DECK-CAP-CASTLE-ECONOMY-041`, and `CC6-DECK-PROGRESSION-AND-MIGRATION-043` remain evidence of prior proposals and are superseded where they conflict with the owner-approved direction above.
- The owner override is recorded in this handover and `CC6_TO_CR7_BATTLE_HANDOVER_2026-09-02.md`; it supersedes the earlier beta recommendation to retain Barracks 10–20.
- The owner override is directional approval, not yet a complete implementable specification. MOS §16 requires the old rule, new rule, rationale, affected systems, and save/data migration to be documented before the live rule is changed.
- UI-101 V11 and its lower-control crop are the latest conditional Battle references; they are not approved art and must not be treated as a production layout.
- The exact 7→15 milestone curve, reserve generation, duplicate handling, and save migration are intentionally not invented here. They are CR7/MS implementation deliverables under the approved direction.
- AD is a separate co-pilot/room from UI. AD owns art approval and hierarchy review; UI owns generation of references only. MT remains marketing-only.
- No native runtime readiness, beta readiness, or final art approval is claimed by this document.

**Document status:** coordination handover only; no production code or art assets changed by this document.
