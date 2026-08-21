namespace MyriadOfDragons.UI
{
    public class CampaignStageData
    {
        public string stageId;
        public string title;
        public string enemyName;
        public string enemyPortraitPath;
        public string description;
        public int goldReward;
        public int gemReward;
        public bool isUnlocked;

        /// <summary>Campaign-stage battle-configuration contract: this stage's data-defined enemy
        /// deck, verified real CardDatabase ids only - null/empty for a stage with no configured
        /// battle (GameBootstrap.TryResolveCampaignEnemyDeck then refuses to launch it rather than
        /// falling back to a random deck). Optional and additive: a stage that never sets this
        /// behaves exactly as before this contract existed.</summary>
        public string[] enemyDeckCardIds;

        public CampaignStageData(string id, string title, string enemy, string portrait, string desc, int gold, int gems, bool unlocked = false, string[] enemyDeckCardIds = null)
        {
            this.stageId = id;
            this.title = title;
            this.enemyName = enemy;
            this.enemyPortraitPath = portrait;
            this.description = desc;
            this.goldReward = gold;
            this.gemReward = gems;
            this.isUnlocked = unlocked;
            this.enemyDeckCardIds = enemyDeckCardIds;
        }
    }
}