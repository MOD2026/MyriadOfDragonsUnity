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

        public CampaignStageData(string id, string title, string enemy, string portrait, string desc, int gold, int gems, bool unlocked = false)
        {
            this.stageId = id;
            this.title = title;
            this.enemyName = enemy;
            this.enemyPortraitPath = portrait;
            this.description = desc;
            this.goldReward = gold;
            this.gemReward = gems;
            this.isUnlocked = unlocked;
        }
    }
}