using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BATTLE-UI-PRESENTATION-INTEGRATION-001, 2026-09-16 - the approved Battle spell-animation
    /// asset package (593c6ae2, docs/BATTLE_SPELL_VFX_PACKAGE_HANDOFF.md) gave every one of the
    /// 36 real catalog spells a real cast-impact sprite (GameBootstrap.SpellEffectSprite), proven
    /// by BattleSpellVfxManifestTests. It did not touch the SEPARATE always-visible spell-rail
    /// icon resolver (SpellIconSprite) - that still only covered the original 4 SpellEffect
    /// values, so the other 10 (17 of 36 spells) rendered NO icon at all on the live, permanently
    /// -visible action bar, not just during the momentary cast flash. This file proves the fix:
    /// every SpellEffect now resolves to a real icon, reusing the same already-approved UI/VFX/*
    /// family assets the cast-impact VFX already uses for the 10 that previously had none - no
    /// new art, no new approval, and the original 4 StatusIcons-based icons are proven unchanged.
    /// </summary>
    public class SpellIconSpriteResolverTests
    {
        [TestCase(SpellEffect.LaneDamage, "UI/StatusIcons/Burn")]
        [TestCase(SpellEffect.LaneHeal, "UI/StatusIcons/Regeneration")]
        [TestCase(SpellEffect.LaneAttackBuff, "UI/StatusIcons/Rage")]
        [TestCase(SpellEffect.AvatarStrike, "UI/StatusIcons/Lightning")]
        public void OriginalFourEffects_StillResolveTheirExistingUnchangedStatusIcon(SpellEffect effect, string expectedPath)
        {
            Sprite expected = Resources.Load<Sprite>(expectedPath);
            Assert.IsNotNull(expected, $"Setup: '{expectedPath}' must itself be a real, loadable sprite.");
            Assert.AreEqual(expected, GameBootstrap.SpellIconSpriteForTests(effect),
                $"{effect}'s already-shipped StatusIcons rail icon must not change.");
        }

        [TestCase(SpellEffect.LaneShield)]
        [TestCase(SpellEffect.Cleanse)]
        [TestCase(SpellEffect.Dispel)]
        [TestCase(SpellEffect.Vulnerability)]
        [TestCase(SpellEffect.AllLaneAttackBuff)]
        [TestCase(SpellEffect.CrossLaneDamage)]
        [TestCase(SpellEffect.AllLaneDamage)]
        [TestCase(SpellEffect.DrawCards)]
        [TestCase(SpellEffect.Reposition)]
        [TestCase(SpellEffect.Silence)]
        public void PreviouslyIconlessEffects_NowResolveARealNonNullSprite(SpellEffect effect)
        {
            Sprite icon = GameBootstrap.SpellIconSpriteForTests(effect);
            Assert.IsNotNull(icon,
                $"{effect} previously resolved to null here, leaving its spell-rail button with no icon at all " +
                "in the live Battle UI - it must now resolve to a real sprite.");
        }

        [Test]
        public void EveryRealSpellEffectValue_ResolvesToANonNullRailIcon()
        {
            foreach (SpellEffect effect in System.Enum.GetValues(typeof(SpellEffect)))
            {
                Assert.IsNotNull(GameBootstrap.SpellIconSpriteForTests(effect),
                    $"SpellEffect.{effect} must resolve to a real spell-rail icon - no silently-blank rail button.");
            }
        }
    }

    /// <summary>
    /// End-to-end proof through the real rendered rail (RefreshPhaseControls's own
    /// _spellIcons[i].sprite/.enabled assignment), not just the pure resolver above.
    /// </summary>
    public class BattleSpellRailIconBindingTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDSpellRailIcon_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }
            return bootstrap;
        }

        private static BattleController StartSurvivableMatchWithSpellbook(GameBootstrap bootstrap, params string[] equippedSpellIds)
        {
            BattleController controller = bootstrap.Battle;
            var data = new CardData
            {
                id = "spell_rail_icon_card", name = "Spell Rail Icon Card", art_file = "x.png",
                element = "Andras", type = "warrior", rarity = 1,
            };
            Card card = Card.FromData(data);
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                equippedSpellIds: equippedSpellIds);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            return controller;
        }

        [TestCase("ember_guard", SpellEffect.LaneShield)]
        [TestCase("cleansing_root", SpellEffect.Cleanse)]
        [TestCase("volcanic_prison", SpellEffect.Silence)]
        public void RealBattle_PreviouslyIconlessSpell_ShowsARealEnabledIconOnTheLiveRail(
            string spellId, SpellEffect expectedEffect)
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellRailIcon_" + spellId);
            BattleController controller = StartSurvivableMatchWithSpellbook(bootstrap, spellId);
            Assert.AreEqual(1, controller.Spellbook.Count, "Setup: expected exactly the one equipped spell.");
            Assert.AreEqual(expectedEffect, controller.Spellbook[0].Effect, "Setup: expected the equipped id to resolve to the effect under test.");

            bootstrap.RefreshAllForTests();

            (Sprite sprite, bool enabled) = bootstrap.SpellRailIconStateForTests(0);
            Assert.IsNotNull(sprite, $"{spellId} ({expectedEffect})'s live rail button must show a real icon sprite.");
            Assert.IsTrue(enabled, $"{spellId} ({expectedEffect})'s live rail icon Image must be enabled, not left disabled from a null sprite.");
        }
    }
}
