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
    /// BATTLE PHONE COMPRESSION FIX - focused coverage for
    /// GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight and its effect on the real Formation/
    /// Combat canvas.
    ///
    /// The real, pre-existing failure this closes: CanvasOverflowAuditTests measures
    /// "GameBootstrap Canvas on phone 2400x1080: 1080 authored design-space units compress to 966
    /// on this profile (11% loss, match=0.5)" - a real 2400x1080 phone (20:9, wider than this
    /// game's 16:9 reference) has width to spare, but a flat match=0.5 still lets HEIGHT compress,
    /// because 0.5 does not use that slack. GameBootstrap.BuildCanvas now picks matchWidthOrHeight
    /// per the real device's own aspect ratio at boot (see that method's own doc comment) - 1
    /// (match height exactly) only for screens wider than the 16:9 reference, which for 2400x1080
    /// specifically gives scaleFactor = screenHeight/refHeight = 1080/1080 = 1.0 EXACTLY, i.e. zero
    /// compression; 0.5 unchanged for anything narrower-or-equal (tablet 2560x1600 is 16:10 &lt;
    /// 16:9; the authored 1920x1080 baseline itself).
    ///
    /// Tablet support is explicitly excluded from this beta's scope and this fix must not move
    /// tablet's own measured behavior in either direction - several tests below assert tablet is
    /// BIT-FOR-BIT unchanged, not just "not obviously broken".
    /// </summary>
    public class BattlePhoneCompressionLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float PhoneWidth = 2400f;
        private const float PhoneHeight = 1080f;
        private const float TabletWidth = 2560f;
        private const float TabletHeight = 1600f;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDPhoneCompression_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
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

        /// <summary>Sets up a real bootstrap, then overrides its live canvas to the REAL rendered
        /// state a target device profile would produce - both the scaler's own matchWidthOrHeight
        /// (what production would compute for that device) and the canvas's own effective
        /// design-space size (screenSize / scaleFactor, the same "effective design space" concept
        /// CanvasOverflowAuditTests itself uses), then forces a full layout rebuild. This is the
        /// same "inject directly, do not trust the live scaler" technique this file's own history
        /// already established was necessary in EditMode (no real Screen size to react to).</summary>
        private GameBootstrap SpawnAtDeviceProfile(string name, float screenWidth, float screenHeight)
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap(name);

            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo, "Setup: expected a Canvas after Initialize.");
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "Setup: expected the Battle canvas to carry a CanvasScaler.");

            float match = GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(screenWidth, screenHeight, RefWidth, RefHeight);
            scaler.matchWidthOrHeight = match;

            float factor = ScaleFactor(screenWidth, screenHeight, RefWidth, RefHeight, match);
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(screenWidth / factor, screenHeight / factor);

            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

            return bootstrap;
        }

        /// <summary>CanvasScaler's own ScaleWithScreenSize formula - the exact same arithmetic
        /// CanvasOverflowAuditTests uses (copied deliberately, not shared, for the same reason that
        /// file gives: auditing the SETTINGS against sizes the editor will never actually be at,
        /// without needing a live Canvas per profile).</summary>
        private static float ScaleFactor(float screenW, float screenH, float refW, float refH, float match)
        {
            float logWidth = Mathf.Log(screenW / refW, 2f);
            float logHeight = Mathf.Log(screenH / refH, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
        }

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

        private GameBootstrap SpawnFormationReadyAtDeviceProfile(string name, float screenWidth, float screenHeight)
        {
            var databaseGo = new GameObject(name + "_CardDatabase");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();
            var profile = new PlayerProfile { cardCollection = new List<string>(deckIds), activeDeckCardIds = new List<string>(deckIds) };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the confirmed deck to persist.");
            SaveSystem.ResetCurrentProfileForTests();

            return SpawnAtDeviceProfile(name, screenWidth, screenHeight);
        }

        // ---------- The pure decision function ----------

        [Test]
        public void ComputeBattleCanvasMatchWidthOrHeight_Phone_MatchesHeightExactly()
        {
            float match = GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(PhoneWidth, PhoneHeight, RefWidth, RefHeight);
            Assert.AreEqual(1f, match, "A phone wider than the 16:9 reference must match height (1), using its real width slack.");
        }

        [Test]
        public void ComputeBattleCanvasMatchWidthOrHeight_Tablet_StaysAtTheOriginalStaticValue()
        {
            // Regression guard: this fix must not touch tablet's own behavior in either direction.
            float match = GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(TabletWidth, TabletHeight, RefWidth, RefHeight);
            Assert.AreEqual(0.5f, match, "A tablet narrower than the 16:9 reference must keep the original static 0.5 - tablet is out of scope for this fix.");
        }

        [Test]
        public void ComputeBattleCanvasMatchWidthOrHeight_ExactReferenceAspect_StaysAtTheOriginalStaticValue()
        {
            // The authored 1920x1080 baseline itself - exactly the reference aspect, not "wider
            // than" it, so this must fall to the unchanged branch. At an exact aspect match the
            // scaleFactor is 1 regardless of which branch fires, so this also proves the locked
            // 1920x1080 layout is untouched by construction, not just by coincidence.
            float match = GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(RefWidth, RefHeight, RefWidth, RefHeight);
            Assert.AreEqual(0.5f, match);
        }

        [Test]
        public void ComputeBattleCanvasMatchWidthOrHeight_DegenerateInputs_FallBackSafely()
        {
            Assert.AreEqual(0.5f, GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(0f, 1080f, RefWidth, RefHeight));
            Assert.AreEqual(0.5f, GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(2400f, 0f, RefWidth, RefHeight));
            Assert.AreEqual(0.5f, GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(-100f, 1080f, RefWidth, RefHeight));
        }

        // ---------- Real arithmetic proof (same formula CanvasOverflowAuditTests uses) ----------

        [Test]
        public void PhoneProfile_WithTheNewMatchValue_HasZeroHeightCompression_AndNoWidthOverflow()
        {
            float match = GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(PhoneWidth, PhoneHeight, RefWidth, RefHeight);
            float factor = ScaleFactor(PhoneWidth, PhoneHeight, RefWidth, RefHeight, match);
            float renderedW = RefWidth * factor;
            float renderedH = RefHeight * factor;

            Assert.LessOrEqual(renderedH, PhoneHeight + 1f,
                "Real fix: rendered height must fit the phone's real height - CanvasOverflowAuditTests measured an 11% loss here before this fix.");
            Assert.LessOrEqual(renderedW, PhoneWidth + 1f, "Rendered width must still fit the phone's real width.");

            float effectiveDesignH = PhoneHeight / factor;
            Assert.AreEqual(RefHeight, effectiveDesignH, 0.5f,
                "Effective design-space height must equal the authored 1080 exactly (zero compression), not merely 'less compressed'.");
        }

        [Test]
        public void TabletProfile_StillMeasuresTheOriginalFiveePercentWidthLoss_Unchanged()
        {
            // Proves this fix left tablet's own already-measured-and-accepted figure untouched,
            // using the exact same formula CanvasOverflowAuditTests audits with.
            float match = GameBootstrap.ComputeBattleCanvasMatchWidthOrHeight(TabletWidth, TabletHeight, RefWidth, RefHeight);
            float factor = ScaleFactor(TabletWidth, TabletHeight, RefWidth, RefHeight, match);
            float renderedW = RefWidth * factor;

            Assert.Greater(renderedW, TabletWidth + 1f, "Setup: expected the same real width finding that predates this fix.");
            float effectiveDesignW = TabletWidth / factor;
            float lossPct = (RefWidth - effectiveDesignW) / RefWidth * 100f;
            Assert.AreEqual(5f, lossPct, 0.5f, "Tablet's own compression figure must stay exactly what it measured before this fix (~5%).");
        }

        // ---------- Real bootstrap, real regions, at the real phone profile ----------

        [Test]
        public void PhoneProfile_TopHudRendersAtItsFullAuthoredPixelHeight()
        {
            GameBootstrap bootstrap = SpawnAtDeviceProfile("Phone_TopHudHeight", PhoneWidth, PhoneHeight);
            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Transform topHud = root.Find("TopHud");
            Assert.IsNotNull(topHud, "Expected TopHud to exist.");

            Rect bounds = WorldBounds((RectTransform)topHud);
            // TopHudMax.y - TopHudMin.y = 0.995 - 0.885 = 0.11 of the authored 1080 = 118.8px.
            // At the old match=0.5, this would render ~11% shorter; the fix makes it exact.
            const float expectedAuthoredHeight = (0.995f - 0.885f) * RefHeight;
            Assert.AreEqual(expectedAuthoredHeight, bounds.height, 1.5f,
                $"TopHud must render at its full authored height ({expectedAuthoredHeight:F1}px) on phone now, not compressed.");
        }

        [Test]
        public void PhoneProfile_AllNamedRegions_StayInsideTheCanvas_AndNeverOverlapEachOther()
        {
            GameBootstrap bootstrap = SpawnFormationReadyAtDeviceProfile("Phone_RegionContainment", PhoneWidth, PhoneHeight);
            RectTransform root = bootstrap.BattlePresentationRootForTests;
            GameObject canvasGo = GameObject.Find("Canvas");
            Rect canvasBounds = WorldBounds(canvasGo.GetComponent<RectTransform>());

            string[] regionNames = { "TopHud", "ActivityRail", "SpellRail", "HandAndPlacementPanel", "PrimaryActionPanel" };
            var regionBounds = new Dictionary<string, Rect>();
            foreach (string name in regionNames)
            {
                Transform region = root.Find(name);
                Assert.IsNotNull(region, $"Expected region '{name}' to exist.");
                Rect bounds = WorldBounds((RectTransform)region);
                regionBounds[name] = bounds;

                Assert.GreaterOrEqual(bounds.xMin, canvasBounds.xMin - 1f, $"{name} must not spill off the left edge on phone.");
                Assert.LessOrEqual(bounds.xMax, canvasBounds.xMax + 1f, $"{name} must not spill off the right edge on phone.");
                Assert.GreaterOrEqual(bounds.yMin, canvasBounds.yMin - 1f, $"{name} must not spill off the bottom edge on phone.");
                Assert.LessOrEqual(bounds.yMax, canvasBounds.yMax + 1f, $"{name} must not spill off the top edge on phone.");
            }

            var names = regionBounds.Keys.ToList();
            for (int i = 0; i < names.Count; i++)
                for (int j = i + 1; j < names.Count; j++)
                    Assert.IsFalse(regionBounds[names[i]].Overlaps(regionBounds[names[j]]),
                        $"'{names[i]}' {regionBounds[names[i]]} must not overlap '{names[j]}' {regionBounds[names[j]]} on phone.");

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                RectTransform playerLane = bootstrap.PlayerLaneButtonRectForTests(lane);
                RectTransform enemyLane = bootstrap.EnemyLaneButtonRectForTests(lane);
                Assert.IsNotNull(playerLane, $"Expected a Player {lane} lane on phone.");
                Assert.IsNotNull(enemyLane, $"Expected an Enemy {lane} lane on phone.");
                Assert.IsFalse(regionBounds["ActivityRail"].Overlaps(WorldBounds(playerLane)));
                Assert.IsFalse(regionBounds["SpellRail"].Overlaps(WorldBounds(enemyLane)));
            }
        }

        // ---------- Input reachability on phone ----------

        [Test]
        public void PhoneProfile_StartBattle_IsReachable_NonZeroHitArea_Interactable()
        {
            GameBootstrap bootstrap = SpawnFormationReadyAtDeviceProfile("Phone_PrimaryActionReachable", PhoneWidth, PhoneHeight);
            bootstrap.AutoFormationForTests();

            Assert.IsTrue(bootstrap.PrimaryActionButtonActiveForTests, "Start Battle must be visible on phone once Formation is ready.");
            Assert.IsTrue(bootstrap.PrimaryActionButtonInteractableForTests, "Start Battle must be interactable on phone.");

            GameObject canvasGo = GameObject.Find("Canvas");
            Button startBattle = canvasGo.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.GetComponentInChildren<Text>(true)?.text == bootstrap.PrimaryActionLabelForTests
                                      && bootstrap.PrimaryActionLabelForTests == "START BATTLE");
            Assert.IsNotNull(startBattle, "STATE UNREACHED: could not find the real Start Battle button by its own label.");

            Rect rect = WorldBounds((RectTransform)startBattle.transform);
            Assert.Greater(rect.width, 0f, "Start Battle must have a non-zero hit width on phone.");
            Assert.Greater(rect.height, 0f, "Start Battle must have a non-zero hit height on phone.");
        }

        [Test]
        public void PhoneProfile_SpellButtons_AreReachable_NonZeroHitArea()
        {
            GameBootstrap bootstrap = SpawnAtDeviceProfile("Phone_SpellButtonsReachable", PhoneWidth, PhoneHeight);
            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Transform spellRail = root.Find("SpellRail");
            Assert.IsNotNull(spellRail);

            // CR-BATTLE-PRESENTATION-VISUAL-PASS-002, 2026-09-17: the rail now always builds
            // SpellLoadoutAutoEquip.MaxSlotCount (6) row GameObjects (up from a hardcoded 4), so a
            // level-10+/20+ Avatar's real 5th/6th equipped spell has a row to appear in - see that
            // constant's own doc comment. A fresh/default profile (this test's own setup) still
            // only equips the starter four, so only 4 of the 6 built rows are ever active; the
            // other 2 exist but stay inactive and contribute no layout space (proven separately by
            // BattleSpellRailSlotCountTests.FourSpellLoadout_RendersExactlyAsBefore_...). Hit-area
            // is only meaningful for a row that can actually receive a tap.
            Button[] spellButtons = spellRail.GetComponentsInChildren<Button>(true);
            Assert.AreEqual(SpellLoadoutAutoEquip.MaxSlotCount, spellButtons.Length,
                "Expected one spell-rail row per SpellLoadoutAutoEquip.MaxSlotCount to exist (active or not).");
            Button[] activeSpellButtons = spellButtons.Where(b => b.gameObject.activeInHierarchy).ToArray();
            Assert.AreEqual(4, activeSpellButtons.Length,
                "Expected exactly 4 ACTIVE spell buttons for this test's fresh/default (starter) loadout.");
            foreach (Button spell in activeSpellButtons)
            {
                Rect bounds = WorldBounds((RectTransform)spell.transform);
                Assert.Greater(bounds.width, 0f, "Each active spell button must keep a non-zero hit width on phone.");
                Assert.Greater(bounds.height, 0f, "Each active spell button must keep a non-zero hit height on phone.");
            }
        }

        [Test]
        public void PhoneProfile_HandDock_IsReachable_AndDoesNotOverlapThePrimaryAction()
        {
            GameBootstrap bootstrap = SpawnFormationReadyAtDeviceProfile("Phone_HandDockReachable", PhoneWidth, PhoneHeight);
            RectTransform root = bootstrap.BattlePresentationRootForTests;

            RectTransform handDock = bootstrap.HandDockRectForTests;
            Transform primaryAction = root.Find("PrimaryActionPanel");
            Assert.IsNotNull(handDock, "Expected the hand dock to exist on phone.");
            Assert.IsNotNull(primaryAction, "Expected PrimaryActionPanel to exist on phone.");

            Rect handBounds = WorldBounds(handDock);
            Rect actionBounds = WorldBounds((RectTransform)primaryAction);
            Assert.Greater(handBounds.width, 0f);
            Assert.Greater(handBounds.height, 0f);
            Assert.IsFalse(handBounds.Overlaps(actionBounds), "Hand dock must not overlap Start Battle on phone.");
        }

        // ---------- Preserve the locked 1920x1080 layout exactly ----------

        [Test]
        public void BaselineProfile_1920x1080_RemainsBitForBitUnchangedByThisFix()
        {
            // ComputeBattleCanvasMatchWidthOrHeight returns the same 0.5 at the exact reference
            // aspect as the old flat constant did - this proves the authored/locked layout is
            // untouched by the fix, not merely "probably fine".
            GameBootstrap bootstrap = SpawnFormationReadyAtDeviceProfile("Baseline_Unchanged", RefWidth, RefHeight);
            GameObject canvasGo = GameObject.Find("Canvas");
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, "The authored 1920x1080 canvas must keep its original match value.");

            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Transform topHud = root.Find("TopHud");
            Assert.IsNotNull(topHud);
            Rect bounds = WorldBounds((RectTransform)topHud);
            const float expectedAuthoredHeight = (0.995f - 0.885f) * RefHeight;
            Assert.AreEqual(expectedAuthoredHeight, bounds.height, 0.5f);
        }
    }
}
