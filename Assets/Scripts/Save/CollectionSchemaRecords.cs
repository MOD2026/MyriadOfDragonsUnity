using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Save
{
    /// <summary>Per-card ownership and progression (Collection schema V1).</summary>
    [Serializable]
    public class CardProgressionRecord
    {
        public string cardId = string.Empty;
        public int copyCount = 1;
        public int cardLevel = 1;
        public int evolutionStep = 0;
        public int trainingXp = 0;
    }

    /// <summary>Non-currency burn materials from duplicate overflow.</summary>
    [Serializable]
    public class CollectionMaterialWallet
    {
        public int genericSacrificeCredits = 0;
        public int forgeCredits = 0;
        public List<RarityMaterialBalance> dustByRarity = new List<RarityMaterialBalance>();
    }

    [Serializable]
    public class RarityMaterialBalance
    {
        public int rarity = 1;
        public int dust = 0;
    }
}
