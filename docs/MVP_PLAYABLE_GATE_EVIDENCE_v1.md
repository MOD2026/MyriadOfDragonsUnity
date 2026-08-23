# MVP Playable Gate — EditMode Evidence v1

**READY FOR CC — Yes / No:** Approve these existing EditMode suites as the automated evidence map for `MVP_PLAYABLE_GATE_v1`?

**Rule:** Greenlight requires these suites to be green in the current worktree plus the spine’s manual smoke route. `MVP_FIRST_SESSION_SCRIPT_v1` remains an optional owner run after CC greenlight.

| MVP greenlight row | Primary EditMode suites | What the suites prove |
|---|---|---|
| Tutorial complete → Home | `TutorialFoundationTests`, `TutorialGuidedSequenceTests`, `TutorialEncounterWinTests`, `HomePageReturnToCityTests`, `HomePageTutorialRewardGuardTests` | Tutorial starts, follows its approved sequence, resolves, and returns safely to Home without incorrect reward/progression mutation. |
| Campaign 1-1 launchable | `CampaignMvpProgressionTests`, `CampaignLaunchFeedbackContractTests`, `CampaignInputContractTests`, `CampaignStaminaEntryContractTests`, `CampaignStageBattleConfigurationTests` | Campaign entry input works; launch has player-facing failure states; valid stage/deck/stamina handoff reaches Battle with stage configuration. |
| Shop buys one pack | `ShopCardPersistenceTests`, `ShopCurrencyIntegrityTests`, `ShopToDeckIntegrationTests` | Real wallet spend, card grant, save/reload persistence, and downstream ownership/deck availability. |
| Collection burn/evolve reachable | `CollectionPackReceiptTests`, `CollectionBurnRulesTests`, `CollectionBurnTests`, `CollectionEvolutionTests`, `CollectionForgeDustUiTests` | Receipt/order safety, locked burn outcomes, evolution prerequisites, and player-facing Forge/Dust requirement surfaces. |
| Deck legal for battle | `DeckBuilderCollectionOwnershipTests`, `DeckPersistenceTests`, `NormalBattleEntryContractTests`, `NormalBattleSavedDeckIntegrationTests`, `NormalBattleAutoFormationTests` | Owned cards can form/save a legal deck; normal Battle rejects invalid decks and deals the confirmed saved deck; formation enters combat. |

## CC run rule

- Run this grouped evidence set only after the current implementation slice is complete; do not call the MVP green merely because one isolated suite passes.
- A failure in a named suite blocks the matching row until diagnosed and re-run green.
- This is functional evidence, not a final visual-polish approval.

## Human-session status

`MVP_FIRST_SESSION_SCRIPT_v1` is **optional owner validation after CC declares the spine green**. It is not a prerequisite for automated functional evidence.
