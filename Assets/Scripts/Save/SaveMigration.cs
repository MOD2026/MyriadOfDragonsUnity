using System.Collections.Generic;
using MyriadOfDragons.Economy;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// Repairs a loaded <see cref="PlayerProfile"/> so the rest of the game can trust it.
    ///
    /// Adapted 2026-08-07 to operate on PlayerProfile directly - the profile is now the
    /// serialised type itself (see PlayerProfile.cs), there is no separate SaveData wrapper and
    /// no saveVersion field, so there is nothing to Upgrade() between schema versions yet. What
    /// remains is exactly the "structurally valid but nonsensical" repair job: a hand-edited or
    /// truncated file can carry a level of 0 or a negative currency balance, and JsonUtility
    /// leaves a List null for any key absent from the JSON - both are a NullReferenceException or
    /// an unplayable match on the boot path if nothing catches them first.
    /// </summary>
    public static class SaveMigration
    {
        public static void Normalize(PlayerProfile profile)
        {
            if (profile == null) return;

            profile.seenChapters ??= new List<string>();
            profile.unlockedStageIds ??= new List<string>();
            profile.activeDeckCardIds ??= new List<string>();
            profile.cardCollection ??= new List<string>();
            profile.inventoryAssets ??= new List<TradeableAssetInstance>();
            if (string.IsNullOrEmpty(profile.playerName)) profile.playerName = "Sovereign";

            profile.avatarLevel = AtLeastOne(profile.avatarLevel);
            profile.castleLevel = AtLeastOne(profile.castleLevel);
            profile.barracksLevel = AtLeastOne(profile.barracksLevel);
            profile.gateLevel = AtLeastOne(profile.gateLevel);
            profile.level = AtLeastOne(profile.level);

            profile.gold = AtLeastZero(profile.gold);
            profile.gems = AtLeastZero(profile.gems);
            profile.eventMedals = AtLeastZero(profile.eventMedals);
            profile.guildContribution = AtLeastZero(profile.guildContribution);
            profile.dragonRelics = AtLeastZero(profile.dragonRelics);
            profile.maxStamina = AtLeastZero(profile.maxStamina);

            // Clamped to its OWN max, not just >= 0 - a hand-edited or corrupted file claiming
            // more current stamina than its own maximum would let a consumer render a >100% bar
            // or treat it as spendable beyond what maxStamina should ever allow.
            profile.stamina = Clamp(profile.stamina, 0, profile.maxStamina);

            profile.totalMatches = AtLeastZero(profile.totalMatches);
            profile.totalWins = AtLeastZero(profile.totalWins);
            profile.winStreak = AtLeastZero(profile.winStreak);

            // Wins can never exceed matches played - a file where they do divides every
            // downstream win-rate calculation into nonsense.
            if (profile.totalWins > profile.totalMatches) profile.totalMatches = profile.totalWins;
        }

        private static int AtLeastOne(int value) => value < 1 ? 1 : value;

        private static int AtLeastZero(int value) => value < 0 ? 0 : value;

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
