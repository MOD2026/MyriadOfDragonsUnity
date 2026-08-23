using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Block N — thin evidence index for <c>docs/MVP_PLAYABLE_GATE_v1.md</c>.
    /// Documents which existing EditMode suites greenlight each gate row. No new combat.
    /// </summary>
    public class MvpPlayableGateEvidenceTests
    {
        [Test]
        public void DocumentsExistingSuites_ForMvpPlayableGateV1Rows()
        {
            // Tutorial → Home: MvpOnboardingSpineTests, HomePageReturnToCityTests,
            //   HomePageTutorialRewardGuardTests (incl. Home feature Start Tutorial invite).
            // Campaign 1-1: MvpOnboardingSpineTests (launch+win), CampaignLaunchFeedbackContractTests,
            //   CampaignAfMirroredAiSpellSmokeTests (AI-on resolve), Chapter1CampaignPlayabilityTests
            //   (AF taught-path spells-off win).
            // Shop Soft: PackOpenOverlayTests (Collection next-step).
            // Collection Soft: CollectionForgeDustUiTests / evolution burn honesty suites.
            // Legal battle deck: MvpOnboardingSpineTests (Deck Builder confirm before 1-1),
            //   DeckBuilderFirstOpenSoftTests (first-open Soft guidance), DeckPersistenceTests /
            //   NormalBattleEntryContractTests (saved-deck gate).
            // Empire Soft: EmpireConstructionHomeTests (Castle Resource/HP payoff).
            Assert.Pass(
                "Evidence map only — see class doc comment and docs/MVP_PLAYABLE_GATE_v1.md.");
        }
    }
}
