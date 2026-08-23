using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Block W.1 shared helper: any EditMode profile that launches a Chapter 2+ stage via
    /// LaunchCampaignStageForTests / TryLaunchCampaignStage must carry a Gate level high enough
    /// for that chapter, now that Gate is a real (necessary, not sufficient) launch check - see
    /// HomePagePresenter.TryLaunchCampaignStage. One shared lookup so every fixture stays in sync
    /// with PlayerEmpireData.MinimumGateLevelForChapter instead of hand-copying the number.
    /// </summary>
    public static class GateTestSupport
    {
        /// <summary>Sets profile.gateLevel to exactly the minimum required for
        /// <paramref name="chapter"/>, never lowering an already-sufficient value.</summary>
        public static void EnsureGateAllowsChapter(PlayerProfile profile, int chapter)
        {
            if (profile == null) return;
            int required = PlayerEmpireData.MinimumGateLevelForChapter(chapter);
            if (profile.gateLevel < required)
                profile.gateLevel = required;
        }
    }
}
