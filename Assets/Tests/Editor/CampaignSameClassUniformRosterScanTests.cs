using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MyriadOfDragons.Cards;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Block R — data-only scan of all Campaign Ch1–10 enemy rosters for the Stage 1-2 pattern:
    /// 3 cards with the same class on all three, and/or identical ATK/HP lines.
    /// No balance/combat retunes — documentation + stability asserts only.
    /// </summary>
    public class CampaignSameClassUniformRosterScanTests
    {
        private GameObject _databaseGo;

        [TearDown]
        public void TearDown()
        {
            if (_databaseGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_databaseGo);
                _databaseGo = null;
            }
        }

        /// <summary>Full ordered campaign chain (513 stages): 1-1..1-12, 2-1..2-21, 3-1..18-30.</summary>
        public static IEnumerable<string> AllCampaignStageIds()
        {
            for (int i = 1; i <= 12; i++) yield return $"1-{i}";
            for (int i = 1; i <= 21; i++) yield return $"2-{i}";
            for (int chapter = 3; chapter <= 18; chapter++)
            {
                for (int i = 1; i <= 30; i++)
                    yield return $"{chapter}-{i}";
            }
        }

        public readonly struct RosterMatch
        {
            public readonly string StageId;
            public readonly string[] CardIds;
            public readonly string Classes;
            public readonly string AtkHpLines;
            public readonly int TotalAtk;
            public readonly int TotalHp;
            public readonly bool SameClass;
            public readonly bool UniformStats;
            public readonly string MatchKind;

            public RosterMatch(
                string stageId,
                string[] cardIds,
                string classes,
                string atkHpLines,
                int totalAtk,
                int totalHp,
                bool sameClass,
                bool uniformStats)
            {
                StageId = stageId;
                CardIds = cardIds;
                Classes = classes;
                AtkHpLines = atkHpLines;
                TotalAtk = totalAtk;
                TotalHp = totalHp;
                SameClass = sameClass;
                UniformStats = uniformStats;
                if (sameClass && uniformStats) MatchKind = "same-class+uniform-stats";
                else if (sameClass) MatchKind = "same-class";
                else MatchKind = "uniform-stats";
            }
        }

        public static List<RosterMatch> ScanMatches(CardDatabase database)
        {
            var matches = new List<RosterMatch>();
            foreach (string stageId in AllCampaignStageIds())
            {
                CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
                Assert.NotNull(stage, $"Missing stage {stageId} in campaign catalog.");
                Assert.NotNull(stage.enemyDeckCardIds, $"Stage {stageId} has null enemyDeckCardIds.");

                // Pattern scope: 3-card enemy rosters only (Campaign AF formation size).
                if (stage.enemyDeckCardIds.Length != 3) continue;

                Card[] cards = stage.enemyDeckCardIds.Select(id =>
                {
                    Card card = database.GetCard(id);
                    Assert.NotNull(card, $"Stage {stageId}: missing card id '{id}'.");
                    return card;
                }).ToArray();

                bool sameClass = cards.All(c => c.Class == cards[0].Class);
                bool uniformStats = cards.All(c => c.Attack == cards[0].Attack && c.Health == cards[0].Health);
                if (!sameClass && !uniformStats) continue;

                matches.Add(new RosterMatch(
                    stageId,
                    (string[])stage.enemyDeckCardIds.Clone(),
                    string.Join("/", cards.Select(c => c.Class.ToString())),
                    string.Join(", ", cards.Select(c => $"{c.Attack}/{c.Health}")),
                    cards.Sum(c => c.Attack),
                    cards.Sum(c => c.Health),
                    sameClass,
                    uniformStats));
            }

            return matches;
        }

        public static string BuildMarkdownTable(IReadOnlyList<RosterMatch> matches, int scannedStageCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Block R — Same-class / uniform-stat campaign roster scan");
            sb.AppendLine();
            sb.AppendLine("**Scope:** data-only. No deck retunes, combat math, economy, Save, or Permits.");
            sb.AppendLine("**Pattern:** enemy roster of exactly 3 cards with (a) same `CardClass` on all three, and/or (b) identical ATK/HP lines (Stage 1-2 pattern).");
            sb.AppendLine($"**Catalog scanned:** {scannedStageCount} stages (Ch1–10 full chain).");
            sb.AppendLine($"**Matches:** {matches.Count}.");
            sb.AppendLine();
            sb.AppendLine("| stageId | match | classes | ATK/HP each | totals (ATK/HP) | card ids |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (RosterMatch m in matches.OrderBy(x => ParseStageKey(x.StageId)))
            {
                sb.Append("| ").Append(m.StageId)
                    .Append(" | ").Append(m.MatchKind)
                    .Append(" | ").Append(m.Classes)
                    .Append(" | ").Append(m.AtkHpLines)
                    .Append(" | ").Append(m.TotalAtk).Append(" / ").Append(m.TotalHp)
                    .Append(" | ").Append(string.Join(", ", m.CardIds))
                    .AppendLine(" |");
            }

            sb.AppendLine();
            sb.AppendLine("## Counts by kind");
            sb.AppendLine();
            sb.AppendLine($"| kind | count |");
            sb.AppendLine($"|---|---|");
            sb.AppendLine($"| same-class+uniform-stats | {matches.Count(m => m.SameClass && m.UniformStats)} |");
            sb.AppendLine($"| same-class only | {matches.Count(m => m.SameClass && !m.UniformStats)} |");
            sb.AppendLine($"| uniform-stats only | {matches.Count(m => !m.SameClass && m.UniformStats)} |");
            sb.AppendLine($"| **any match** | **{matches.Count}** |");
            sb.AppendLine();
            sb.AppendLine("Generated by `CampaignSameClassUniformRosterScanTests` (Block R).");
            return sb.ToString();
        }

        private static (int chapter, int stage) ParseStageKey(string stageId)
        {
            string[] parts = stageId.Split('-');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        [Test]
        public void CampaignCatalog_SameClassOrUniformStat_ThreeCardRosters_AreDocumentedAndStable()
        {
            _databaseGo = new GameObject("CardDatabase_SameClassUniformScan");
            CardDatabase database = _databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            string[] allIds = AllCampaignStageIds().ToArray();
            Assert.AreEqual(513, allIds.Length, "Setup: Ch1–18 full chain must stay 513 stages.");

            List<RosterMatch> matches = ScanMatches(database);
            Assert.GreaterOrEqual(matches.Count, 1,
                "At least Stage 1-2 must match the same-class / uniform-stat pattern.");
            Assert.IsTrue(matches.Any(m => m.StageId == "1-2"),
                "Stage 1-2 (warrior×3, 3/2×3) must remain in the match list.");

            RosterMatch stage12 = matches.First(m => m.StageId == "1-2");
            Assert.IsTrue(stage12.SameClass && stage12.UniformStats,
                "1-2 must stay both same-class and uniform-stats.");

            // Stable ordered id list — detect silent roster churn without retuning this block.
            string[] orderedIds = matches.Select(m => m.StageId).OrderBy(ParseStageKey).ToArray();
            CollectionAssert.Contains(orderedIds, "1-2");

            string markdown = BuildMarkdownTable(matches, allIds.Length);
            string docsPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "docs", "BLOCK_R_SAME_CLASS_UNIFORM_ROSTER_SCAN.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(docsPath));
            File.WriteAllText(docsPath, markdown, Encoding.UTF8);
            Debug.Log($"[BlockR] Wrote {docsPath}\n{markdown}");

            var byKind = matches.GroupBy(m => m.MatchKind)
                .ToDictionary(g => g.Key, g => g.Count());
            Assert.Pass(
                $"Block R: {matches.Count}/{allIds.Length} stages match. " +
                $"ids=[{string.Join(", ", orderedIds)}]. " +
                $"kinds={{same+uniform={byKind.GetValueOrDefault("same-class+uniform-stats")}, " +
                $"same-only={byKind.GetValueOrDefault("same-class")}, " +
                $"stats-only={byKind.GetValueOrDefault("uniform-stats")}}}.");
        }
    }
}
