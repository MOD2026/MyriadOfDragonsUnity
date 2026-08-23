namespace MyriadOfDragons.Save
{
    /// <summary>Training Ground sink — burn XP feeds card level (Phase 1).</summary>
    public static class CollectionTrainingRules
    {
        public static int XpCostForNextLevel(int currentLevel) =>
            UnityEngine.Mathf.Max(1, currentLevel) * 100;

        /// <summary>Spends training XP for as many level-ups as affordable; returns count gained.
        /// Hard-stops at <see cref="CollectionSchemaRules.MaxCardLevel"/> (bible lock).</summary>
        public static int ApplyLevelUps(CardProgressionRecord record)
        {
            if (record == null) return 0;

            int gained = 0;
            while (record.cardLevel < CollectionSchemaRules.MaxCardLevel
                   && record.trainingXp >= XpCostForNextLevel(record.cardLevel))
            {
                record.trainingXp -= XpCostForNextLevel(record.cardLevel);
                record.cardLevel++;
                gained++;
            }

            return gained;
        }
    }
}
