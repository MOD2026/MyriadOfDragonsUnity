using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Asserts the three owner-approved zero-chrome landscape backdrops actually LOAD at the
    /// Resources paths presenters will ask for, and survive Unity import at 1920x1080.
    ///
    /// Same failure class SharedChromeSpriteIntegrityTests guards: a null sprite renders as a
    /// tinted quad and every green suite still passes. A maxTextureSize below source resolution
    /// silently softens a full-bleed backdrop on a 1920x1080 canvas.
    ///
    /// SoloCircuit is imported here so VS can wire it; this file does NOT assert that
    /// SoloCircuitPresenter draws it (VS owns that presenter).
    /// </summary>
    public class ZeroChromeBackdropIntegrityTests
    {
        private static readonly (string path, string label)[] Backdrops =
        {
            ("UI/BattleLaunchV1/battle_launch_backdrop_landscape_v1", "GameBootstrap / battle launch"),
            ("UI/TacticalPuzzleV1/tactical_puzzle_backdrop_landscape_v1", "TacticalPuzzle"),
            ("UI/SoloCircuitV1/solo_circuit_backdrop_landscape_v1", "SoloCircuit (asset-ready; VS wires)"),
        };

        [Test]
        public void EveryZeroChromeBackdrop_ActuallyLoads()
        {
            foreach ((string path, string label) in Backdrops)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite,
                    "'" + path + "' (" + label + ") failed to load. Any screen that draws this " +
                    "falls back to flat colour — the exact silent degradation the chrome task exists to close.");
            }
        }

        [Test]
        public void EveryZeroChromeBackdrop_SurvivesImportAt1920x1080()
        {
            // maxTextureSize:2048 keeps 1920x1080 intact; a lower platform override would soft-scale.
            foreach ((string path, string label) in Backdrops)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null) continue; // reported by load test

                Assert.AreEqual(1920, sprite.texture.width,
                    "'" + path + "' (" + label + ") width is " + sprite.texture.width +
                    " after import — expected 1920. Check TextureImporter maxTextureSize.");
                Assert.AreEqual(1080, sprite.texture.height,
                    "'" + path + "' (" + label + ") height is " + sprite.texture.height +
                    " after import — expected 1080. Check TextureImporter maxTextureSize.");
            }
        }
    }
}
