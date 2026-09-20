using System;
using UnityEngine;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// The approved Battle Result icons (UI Beta pack, Battle_Result_Atlas_Runtime): five decorative
    /// result icons, each shipped as a 256x256 single-sprite slice of the approved atlas in a normal
    /// and a reduced-motion variant with identical rects, pivots, PPU and alpha bounds. Pure lookup
    /// rules - which icon a result state uses and where its slice lives - so they can be tested
    /// without a scene. The sprites are decorative feedback only: never a control, never a blocker.
    ///
    /// The slice files are used rather than the 1280x256 atlas files: the project's shared
    /// CardArtImportSettings postprocessor forces every texture under Resources/UI to a single
    /// sprite, which flattens a multiple-sprite atlas. The slices are exactly the atlas cells.
    /// The cell data below is the pack's contract; tests assert the imported slices match it.
    /// </summary>
    public static class BattleResultIconSet
    {
        public const string Victory = "BattleResult_Victory";
        public const string Defeat = "BattleResult_Defeat";
        public const string Retry = "BattleResult_Retry";
        public const string Replay = "BattleResult_Replay";
        public const string ResolvedConfirmed = "BattleResult_ResolvedConfirmed";

        public const int CellSize = 256;
        public const float PixelsPerUnit = 100f;

        private const string NamePrefix = "BattleResult_";
        private const string NormalFolder = "UI/Battle/Interaction/";
        private const string ReducedMotionFolder = "UI/Battle/Interaction/ReducedMotion/";

        /// <summary>One icon: pack sprite name, its cell rect in the source atlas, and inclusive alpha
        /// bounds inside the 256x256 cell (top-left convention, as measured from the supplied RGBA).</summary>
        public readonly struct Cell
        {
            public readonly string Name;
            public readonly int X, Y, Width, Height;
            public readonly int AlphaMinX, AlphaMinY, AlphaMaxX, AlphaMaxY;

            public Cell(string name, int x, int alphaMinX, int alphaMinY, int alphaMaxX, int alphaMaxY)
            {
                Name = name;
                X = x;
                Y = 0;
                Width = CellSize;
                Height = CellSize;
                AlphaMinX = alphaMinX;
                AlphaMinY = alphaMinY;
                AlphaMaxX = alphaMaxX;
                AlphaMaxY = alphaMaxY;
            }
        }

        public static readonly Cell[] Cells =
        {
            new Cell(Victory, 0, 70, 70, 186, 186),
            new Cell(Defeat, 256, 57, 92, 183, 165),
            new Cell(Retry, 512, 81, 79, 173, 175),
            new Cell(Replay, 768, 92, 61, 182, 195),
            new Cell(ResolvedConfirmed, 1024, 67, 67, 190, 190),
        };

        /// <summary>Resources path (no extension) of an icon's slice for the given motion mode.</summary>
        public static string SlicePath(string spriteName, bool reduceMotion)
        {
            string state = spriteName.StartsWith(NamePrefix, StringComparison.Ordinal) ? spriteName.Substring(NamePrefix.Length) : spriteName;
            return reduceMotion
                ? $"{ReducedMotionFolder}BattleResult_ReducedMotion_Static_{state}_256x256"
                : $"{NormalFolder}BattleResult_Normal_{state}_256x256";
        }

        /// <summary>The outcome badge shown on the result panel (decorative, not a control).</summary>
        public static string OutcomeBadgeName(bool playerWon) => playerWon ? Victory : Defeat;

        /// <summary>The icon on the existing play-again control: Replay after a win, Retry after a loss.</summary>
        public static string ActionIconName(bool playerWon) => playerWon ? Replay : Retry;

        /// <summary>The icon on the existing Return to Empire control.</summary>
        public static string ReturnIconName => ResolvedConfirmed;

        /// <summary>Resolves an icon for the current motion mode. Reduced Motion uses the
        /// reduced-motion slice (an immediate swap - identical geometry, no animation); if that slice
        /// is missing the other variant is tried, so a bad import degrades to the same icon rather
        /// than to nothing. Null only when neither exists. `load` is injected so tests can force a
        /// missing asset.</summary>
        public static Sprite Resolve(string spriteName, bool reduceMotion, Func<string, Sprite> load) =>
            load(SlicePath(spriteName, reduceMotion)) ?? load(SlicePath(spriteName, !reduceMotion));
    }
}
