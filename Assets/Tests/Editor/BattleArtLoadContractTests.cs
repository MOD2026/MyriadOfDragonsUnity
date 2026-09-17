using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Every sprite the battle screen loads must actually LOAD.
    ///
    /// WHY THIS EXISTS. "The path is spelled right" and "the asset loads" have come apart four
    /// separate times in this project, and each time the file was present and the path correct:
    ///   - the combat .opus audio: Unity assigned a generic importer, Resources.Load returned null
    ///     for all six cues, and renaming to .ogg did not help because the CODEC was the blocker
    ///   - the Empire structure tiles: the renders existed and were wired into the detail popup,
    ///     and nothing loaded them into the entry strip
    ///   - the puzzle result-modal art: a reserved role that was never applied to any view
    ///   - the AvatarStrike stinger: a one-word typo in a binding map, which produced SILENCE
    ///     indistinguishable from "no audio shipped yet" because the sink is null-tolerant by design
    ///
    /// A test that checks a path STRING cannot fail on any of those. A test that checks the file is
    /// on disk cannot fail on the first one either: a PNG imported as a Texture rather than a Sprite
    /// sits in the directory looking perfectly correct and returns null here.
    ///
    /// GameBootstrap degrades gracefully when art is missing - deliberately, so a missing sprite is
    /// a plain-colour fallback rather than an exception mid-battle. That is the right behaviour and
    /// it is exactly why this test is needed: graceful degradation makes a broken asset invisible.
    /// </summary>
    public class BattleArtLoadContractTests
    {
        /// <summary>
        /// Every Resources path GameBootstrap loads a Sprite from, as of 2026-08-26.
        ///
        /// A SNAPSHOT, and knowingly so: these are inline string literals scattered through a large
        /// file rather than a single map, so this list cannot enumerate itself. Adding a new
        /// Resources.Load&lt;Sprite&gt; call without adding it here leaves that one path unguarded -
        /// the drift is real, and it is still better than testing none of them. If this file ever
        /// gets a path map, point the test at the map instead.
        /// </summary>
        private static readonly string[] BattleSpritePaths =
        {
            "UI/Bars/Health_Empty",
            "UI/Frames/Avatar_Circle_Frame",
            "UI/Frames/NineSlice/Popup_Frame",
            "UI/Icons/Tutorial_Arrow",
            "UI/Portraits/Paladin",
            "UI/Slots/Empty_Slot",
            "UI/StatusIcons/Burn",
            "UI/StatusIcons/Lightning",
            "UI/StatusIcons/Rage",
            "UI/StatusIcons/Regeneration",
            "UI/VFX/Fire_Explosion",
            "UI/VFX/Heal_Ring",
            "UI/VFX/Holy_Beam",
            "UI/VFX/Ice_Explosion",
            "UI/VFX/Lightning_Strike",
            "UI/VFX/Magic_Circle",
            "UI/VFX/Poison_Cloud",
            "UI/VFX/Resurrection_Glow",
            "UI/VFX/Shadow_Explosion",
            "UI/VFX/Shield_Bubble",
        };

        [Test]
        public void EverySpriteTheBattleScreenLoads_ResolvesAsARealSprite()
        {
            var broken = new List<string>();
            foreach (string path in BattleSpritePaths)
            {
                if (Resources.Load<Sprite>(path) == null) broken.Add(path);
            }

            CollectionAssert.IsEmpty(broken,
                "These paths return null from Resources.Load<Sprite>. The file may be present but " +
                "imported as a Texture rather than a Sprite, which is indistinguishable from a " +
                "working asset in a directory listing: " + string.Join(", ", broken));
        }

        [Test]
        public void TheCheckIsRealAndNotVacuous()
        {
            // Guards the test above from passing because the list is empty or the loader silently
            // succeeds on anything. A path that certainly does not exist MUST come back null - if
            // this ever fails, Resources.Load is not answering the question the other test asks.
            Assert.IsNotEmpty(BattleSpritePaths, "The guarded path list must not be empty.");
            Assert.IsNull(Resources.Load<Sprite>("UI/DefinitelyNotARealSprite_VsGuard"),
                "A nonexistent path must load as null, or the load check proves nothing.");
        }
    }
}
