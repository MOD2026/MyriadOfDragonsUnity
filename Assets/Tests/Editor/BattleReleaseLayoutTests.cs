using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BATTLE RELEASE LAYOUT PASS - structural/geometry regression coverage for the new
    /// BattlePresentationRoot consolidation. Reuses the same real (post-layout) world-bounds
    /// technique TutorialHandDockGeometryTests already proved out (force a bottom-up
    /// LayoutRebuilder pass against a real 1920x1080 canvas size, then compare
    /// RectTransform.GetWorldCorners() bounds) rather than inventing a second one - this file
    /// only extends that coverage to the whole-screen six-region contract instead of just the
    /// hand dock.
    /// </summary>
    public class BattleReleaseLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsLayout_" + System.Guid.NewGuid().ToString("N"));
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
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
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

            // Same two EditMode-only corrections TutorialHandDockGeometryTests already
            // established as necessary (Canvas.scaleFactor needs a real Screen size that does
            // not exist in headless EditMode, and a single root-level rebuild does not resolve
            // every nested LayoutGroup/ContentSizeFitter) - see that file's own comment for why.
            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                             .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            }

            return bootstrap;
        }

        /// <summary>Axis-aligned world-space bounds of a RectTransform, via its own four world
        /// corners - resolution/anchor-independent (same helper as TutorialHandDockGeometryTests).</summary>
        private static Rect WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = corners.Min(c => c.x);
            float xMax = corners.Max(c => c.x);
            float yMin = corners.Min(c => c.y);
            float yMax = corners.Max(c => c.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void Battle_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            // THE GAP THIS CLOSES: 10+ screens carry this guard and Battle never did - and Battle
            // is the screen I just made render real art for the first time, by wiring START BATTLE
            // and Reset/AUTO FORMATION onto the shared chrome skins.
            //
            // The failure mode is not theoretical: DailyLoginQuests started failing its own copy of
            // this test the moment the sprite-load fix landed, because DiamondOverlay only began
            // rendering then and landed on a button. Art that never drew cannot overlap anything;
            // art that suddenly draws can. Battle had no guard at the exact moment it gained art.
            CardDatabase database = SpawnDatabase();
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards
                .Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.ApplyDataToEmpire();
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_BattleArtOverControlBootstrap");
            Transform root = bootstrap.transform;

            GameObject canvasGo = null;
            foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (candidate.name == "Canvas") { canvasGo = candidate; break; }
            }

            Assert.IsNotNull(canvasGo, "Setup: expected the Battle canvas to exist after Initialize.");
            Transform canvas = canvasGo.transform;

            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new System.Collections.Generic.Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            var collisions = new System.Collections.Generic.List<string>();
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                Rect btn = BattleWorldRect(button.GetComponent<RectTransform>());
                if (btn.width <= 0f || btn.height <= 0f) continue;
                int buttonIndex = indexOf[button.transform];

                foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                {
                    // Only real ART can steal a tap visually - a null-sprite Image is a plain fill,
                    // and the whole point is that art which never rendered cannot overlap anything.
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (indexOf[img.transform] <= buttonIndex) continue;

                    Rect art = BattleWorldRect(img.rectTransform);
                    if (art.width <= 0f || art.height <= 0f) continue;
                    if (art.Overlaps(btn))
                    {
                        collisions.Add("'" + img.name + "' " + art + " overlaps '" + button.name +
                                       "' " + btn + " and draws AFTER it");
                    }
                }
            }

            CollectionAssert.IsEmpty(collisions,
                "Art draws on top of an interactive control, so a tap would land on art instead of " +
                "the button: " + string.Join("  |  ", collisions));
        }

        private static Rect BattleWorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c[0].x, xMax = c[0].x, yMin = c[0].y, yMax = c[0].y;
            for (int i = 1; i < 4; i++)
            {
                if (c[i].x < xMin) xMin = c[i].x;
                if (c[i].x > xMax) xMax = c[i].x;
                if (c[i].y < yMin) yMin = c[i].y;
                if (c[i].y > yMax) yMax = c[i].y;
            }

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void SingleBattlePresentationRoot_OwnsEveryNamedRegion()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_RootOwnershipBootstrap");

            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Assert.IsNotNull(root, "Setup: expected a single BattlePresentationRoot to exist.");

            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo);
            Assert.AreEqual(1, canvasGo.transform.childCount,
                "Canvas must have exactly one direct child - the BattlePresentationRoot. " +
                "No panel (header, board, rail, hand dock, action well, backdrop, or any modal) may sit outside it.");
            Assert.AreSame(root.gameObject, canvasGo.transform.GetChild(0).gameObject);

            foreach (string regionName in new[]
                     {
                         "TopHud", "ActivityRail", "SpellRail", "HandAndPlacementPanel", "PrimaryActionPanel",
                     })
            {
                Transform region = root.Find(regionName);
                Assert.IsNotNull(region, $"Expected named region '{regionName}' to exist as a descendant of BattlePresentationRoot.");
            }

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                RectTransform playerLane = bootstrap.PlayerLaneButtonRectForTests(lane);
                RectTransform enemyLane = bootstrap.EnemyLaneButtonRectForTests(lane);
                Assert.IsNotNull(playerLane, $"Expected a Player {lane} lane button to exist.");
                Assert.IsNotNull(enemyLane, $"Expected an Enemy {lane} lane button to exist.");
                Assert.IsTrue(playerLane.IsChildOf(root), $"Player {lane} lane button must be inside BattlePresentationRoot.");
                Assert.IsTrue(enemyLane.IsChildOf(root), $"Enemy {lane} lane button must be inside BattlePresentationRoot.");
            }
        }

        [Test]
        public void BackdropImages_NeverBlockRaycasts()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_BackdropRaycastBootstrap");

            // Scoped to this test's own BattlePresentationRoot, not a global GameObject.Find -
            // 18+ other presenters (Shop, DeckBuilder, Mail, etc.) also name a child "Background",
            // and a leftover from an earlier test in the same run could resolve first, making this
            // assertion order-dependent instead of deterministic (real repro, 2026-08-26: passed
            // clean alone/in small groups all night, failed only inside the full 1762-test suite).
            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Transform backgroundTransform = root.Find("Background");
            Assert.IsNotNull(backgroundTransform, "Setup: expected the arena backdrop GameObject to exist.");
            GameObject background = backgroundTransform.gameObject;
            Image backgroundImage = background.GetComponent<Image>();
            Assert.IsNotNull(backgroundImage);
            Assert.IsFalse(backgroundImage.raycastTarget,
                "The full-screen arena backdrop must never intercept a tap meant for a real control drawn above it.");

            // Non-campaign Initialize must draw the owner-approved battle-launch backdrop — a null
            // sprite is the flat-colour silent failure SharedChromeSpriteIntegrityTests exists for.
            Assert.IsNotNull(backgroundImage.sprite,
                "Battle Background Image.sprite is null — flat colour fallback, not the launch art.");
            Sprite expected = Resources.Load<Sprite>(GameBootstrap.BattleLaunchBackdropResourcePath);
            Assert.IsNotNull(expected,
                "Battle launch backdrop failed to Resources.Load — import/path broken.");
            Assert.AreSame(expected, backgroundImage.sprite,
                "Normal battle must render battle_launch_backdrop_landscape_v1, not a silent substitute.");

            // The dim wash (only built when the real backdrop sprite loads) is a sibling Image
            // under the same root with no sprite of its own - same requirement, if present.
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject == background || image.sprite != null) continue;
                if (image.gameObject.GetComponent<Button>() != null) continue;
                if (image.transform.parent != root) continue; // only the root's own direct children (backdrop/dim), not every plain-color Image screen-wide

                Assert.IsFalse(image.raycastTarget,
                    $"Decorative root-level Image '{image.gameObject.name}' must not block raycasts.");
            }
        }

        [Test]
        public void NamedRegions_NeverOverlapEachOtherOrEitherBoard()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_NoOverlapBootstrap");

            var regionBounds = new Dictionary<string, Rect>();
            foreach (string regionName in new[] { "TopHud", "ActivityRail", "SpellRail", "HandAndPlacementPanel", "PrimaryActionPanel" })
            {
                Transform region = bootstrap.BattlePresentationRootForTests.Find(regionName);
                Assert.IsNotNull(region, $"Setup: expected named region '{regionName}' to exist.");
                regionBounds[regionName] = WorldBounds((RectTransform)region);
            }

            string[] names = regionBounds.Keys.ToArray();
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = i + 1; j < names.Length; j++)
                {
                    Assert.IsFalse(regionBounds[names[i]].Overlaps(regionBounds[names[j]]),
                        $"Region '{names[i]}' ({regionBounds[names[i]]}) must never overlap region '{names[j]}' ({regionBounds[names[j]]}).");
                }
            }

            // Both boards, and the board rows against every other region - the same non-overlap
            // guarantee TutorialHandDockGeometryTests already proved for the Hand dock alone,
            // extended here to the header, rail, and action well too, and to the enemy board.
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Rect playerBounds = WorldBounds(bootstrap.PlayerLaneButtonRectForTests(lane));
                Rect enemyBounds = WorldBounds(bootstrap.EnemyLaneButtonRectForTests(lane));

                foreach (string regionName in names)
                {
                    Assert.IsFalse(regionBounds[regionName].Overlaps(playerBounds),
                        $"Region '{regionName}' must never overlap the Player {lane} lane ({playerBounds}).");
                    Assert.IsFalse(regionBounds[regionName].Overlaps(enemyBounds),
                        $"Region '{regionName}' must never overlap the Enemy {lane} lane ({enemyBounds}).");
                }

                foreach (Lane otherLane in System.Enum.GetValues(typeof(Lane)))
                {
                    Rect otherPlayerBounds = WorldBounds(bootstrap.PlayerLaneButtonRectForTests(otherLane));
                    Assert.IsFalse(enemyBounds.Overlaps(otherPlayerBounds),
                        $"The Enemy {lane} lane must never overlap the Player {otherLane} lane ({enemyBounds} vs {otherPlayerBounds}).");
                }
            }
        }

        [Test]
        public void AutoFormationAndStartBattle_RemainReachableAndReadable_WithinTheActionWell()
        {
            CardDatabase database = SpawnDatabase();
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards
                .Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.ApplyDataToEmpire();
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_ActionWellBootstrap");

            Assert.IsTrue(bootstrap.RecommendedLineupButtonActiveForTests, "Auto Formation must be reachable for a valid saved deck.");
            Assert.IsTrue(bootstrap.PrimaryActionButtonActiveForTests, "Start Battle must be reachable for a valid saved deck.");

            Transform actionWell = bootstrap.BattlePresentationRootForTests.Find("PrimaryActionPanel");
            Assert.IsNotNull(actionWell, "Setup: expected the action well region to exist.");
            Rect actionWellBounds = WorldBounds((RectTransform)actionWell);

            bootstrap.AutoFormationForTests();

            Assert.IsTrue(bootstrap.PrimaryActionButtonInteractableForTests, "Start Battle must be enabled (readable and usable) once Auto Formation has run.");
            Assert.AreEqual("START BATTLE", bootstrap.PrimaryActionLabelForTests);

            // "Reachable and readable" as real geometry: the same rebuild pass, re-run after
            // Auto Formation changed the board state, confirms the action well's own bounds are
            // unchanged (a fixed region, not something that shifts as the board fills) and still
            // a real, positive-area, on-screen rect - not collapsed or clipped to zero.
            GameObject canvasGo = GameObject.Find("Canvas");
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            Rect actionWellBoundsAfter = WorldBounds((RectTransform)actionWell);
            Assert.Greater(actionWellBoundsAfter.width, 0f);
            Assert.Greater(actionWellBoundsAfter.height, 0f);
            const float tolerance = 0.5f; // fixed-anchor region; allow only sub-pixel float noise between rebuild passes
            Assert.AreEqual(actionWellBounds.x, actionWellBoundsAfter.x, tolerance);
            Assert.AreEqual(actionWellBounds.y, actionWellBoundsAfter.y, tolerance);
            Assert.AreEqual(actionWellBounds.width, actionWellBoundsAfter.width, tolerance);
            Assert.AreEqual(actionWellBounds.height, actionWellBoundsAfter.height, tolerance,
                "The action well's own region bounds must stay fixed regardless of board/formation state.");
        }

        /// <summary>CR-BATTLE-PRESENTATION-VISUAL-PASS-002 follow-up, 2026-09-17 - the right edge
        /// of every right-anchored region (TopHud, ActivityRail, SpellRail, PrimaryActionPanel)
        /// used to sit only 28.8px from the real screen edge at 1920x1080, short of the 48px
        /// horizontal safe-area standard this project's newer approved packages use
        /// (Battle_State_Acceptance_GUI_Handoff.md, Beta_Presenter_Acceptance_GUI_CR_Handoff.md:
        /// "48 px left/right"). Proves the real, post-layout right margin of each region against
        /// the real canvas width rather than a hardcoded anchor fraction, so this stays meaningful
        /// if CanvasWidth or the region's own definition ever changes.</summary>
        [Test]
        public void RightAnchoredRegions_KeepAtLeastTheFortyEightPixelSafeAreaMargin()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_SafeAreaRightMarginBootstrap");
            RectTransform root = bootstrap.BattlePresentationRootForTests;

            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo, "Setup: expected the Battle canvas to exist after Initialize.");
            Rect canvasBounds = WorldBounds(canvasGo.GetComponent<RectTransform>());

            const float requiredMarginPx = 48f;
            foreach (string regionName in new[] { "TopHud", "ActivityRail", "SpellRail", "PrimaryActionPanel" })
            {
                Transform region = root.Find(regionName);
                Assert.IsNotNull(region, $"Setup: expected named region '{regionName}' to exist.");
                Rect bounds = WorldBounds((RectTransform)region);
                float rightMargin = canvasBounds.xMax - bounds.xMax;
                Assert.GreaterOrEqual(rightMargin, requiredMarginPx - 0.5f,
                    $"'{regionName}' has only {rightMargin:F1}px between its right edge and the screen's " +
                    $"real right edge - must be at least the {requiredMarginPx}px horizontal safe-area margin.");
            }
        }

        /// <summary>Regression for the SpellList row-overflow fix (CR, 2026-08-27): asserts the
        /// RELATIONSHIP (every spell row, plus the spacing between them, fits inside SpellList's
        /// own real height) rather than any magic number, per the project rule against hardcoding
        /// a value that can legitimately change (a longer spell name, a font-size tuning pass, a
        /// fifth spell) and start failing for a reason unrelated to the real regression this
        /// guards - rows spilling outside their own container, which in this exact region means
        /// the player's only combat-time input becoming unreadable or untappable.
        ///
        /// Checked at the worst real device profile GameBootstrap's own match=0.5 canvas produces
        /// (a 2400x1080 phone, which the reworded CanvasOverflowAuditTests reports compresses this
        /// canvas's design-space height to ~966 of its authored 1080 - an 11% loss, the case that
        /// first exposed this bug), not just at authored 1920x1080 - a fix that only holds at
        /// authored size is not a fix, since compression is exactly where this class of bug hides.</summary>
        [Test]
        public void SpellList_EveryRowPlusSpacing_FitsItsOwnRealHeight_UnderPhoneCompression()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_SpellListFitBootstrap");

            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo, "Setup: expected the Battle canvas to exist after Initialize.");
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            // Effective design-space size a 2400x1080 phone produces on this canvas's match=0.5
            // scaler: scaleFactor = 2^lerp(log2(2400/1920), log2(1080/1080), 0.5) ≈ 1.118,
            // (2400/1.118, 1080/1.118) ≈ (2147, 966). Injected directly (no ForceUpdateCanvases,
            // which would let the live scaler silently recompute from EditMode's own screen size
            // and discard this) - same technique proven on the Shop/Battle compression passes.
            canvasRect.sizeDelta = new Vector2(2147f, 966f);
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Transform spellRail = root.Find("SpellRail");
            Assert.IsNotNull(spellRail, "Setup: expected SpellRail to exist.");
            // CR-BATTLE-PRESENTATION-VISUAL-PASS-002, 2026-09-17: SpellList is now one level
            // deeper, inside a masked, scrollable "SpellRailViewport" - see that Initialize()
            // comment for why (6 rows, up from a hardcoded 4, no longer always fit the rail's
            // fixed area at the 22px font floor, so overflow now scrolls instead of spilling).
            Transform spellRailViewport = spellRail.Find("SpellRailViewport");
            Assert.IsNotNull(spellRailViewport, "Setup: expected SpellRail/SpellRailViewport to exist.");
            var scrollRect = spellRailViewport.GetComponent<ScrollRect>();
            Assert.IsNotNull(scrollRect, "Setup: expected SpellRailViewport to own a ScrollRect for the overflow case.");
            Assert.IsTrue(scrollRect.vertical, "The spell list must be able to scroll vertically once more rows are equipped than fit.");
            Assert.IsFalse(scrollRect.horizontal, "The spell list must never scroll horizontally - only vertical overflow is expected.");
            Transform spellList = spellRailViewport.Find("SpellList");
            Assert.IsNotNull(spellList, "Setup: expected SpellRailViewport/SpellList to exist.");
            Assert.AreSame(spellList.GetComponent<RectTransform>(), scrollRect.content,
                "ScrollRect.content must be the real SpellList that carries the rows, or scrolling would move nothing.");

            var spellLayout = spellList.GetComponent<VerticalLayoutGroup>();
            Assert.IsNotNull(spellLayout, "Setup: expected SpellList to own the spell rows' VerticalLayoutGroup.");

            // Only the ACTIVE rows matter for this fit check - a level-10+/20+ Avatar's 5th/6th
            // row is expected and fine to need scrolling now; a fresh/default loadout (this
            // test's own setup, always 4 active rows) must still fit without ever needing to
            // scroll, unchanged from before this pass.
            var rowHeights = new List<float>();
            foreach (Transform row in spellList)
            {
                if (!row.gameObject.activeSelf) continue;
                rowHeights.Add(((RectTransform)row).rect.height);
            }
            Assert.Greater(rowHeights.Count, 1, "Setup: expected more than one active spell row to make the spacing math meaningful.");

            float totalRowHeight = rowHeights.Sum();
            float totalSpacing = spellLayout.spacing * (rowHeights.Count - 1);
            float required = totalRowHeight + totalSpacing;
            float available = ((RectTransform)spellRailViewport).rect.height;

            Assert.LessOrEqual(required, available + 0.5f,
                $"SpellList's {rowHeights.Count} ACTIVE rows ({totalRowHeight:F1} units) plus spacing " +
                $"({totalSpacing:F1} units) = {required:F1} units, but the viewport only has " +
                $"{available:F1} units at phone compression - a default/starter loadout must never " +
                "need to scroll; if this now fails, either row count or row height grew for the " +
                "common case, not just the 5/6-slot overflow case this pass added scrolling for.");
        }

        private CardDatabase SpawnDatabase()
        {
            var go = new GameObject("Layout_CardDatabase");
            _spawned.Add(go);
            CardDatabase database = go.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            return database;
        }
    }
}
