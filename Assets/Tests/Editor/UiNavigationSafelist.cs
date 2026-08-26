using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Which controls the navigation crawler is permitted to INVOKE.
    ///
    /// DEFAULT IS DENY, and that default is the whole safety property. CC's instruction is explicit:
    /// a new button must default to "not invoked", because a control that silently becomes
    /// invokable is how a crawler ends up spending a player's currency, consuming a claim, or
    /// firing a purchase callback. Adding a button to a screen must never widen what this crawler
    /// touches - widening requires someone editing THIS file and saying why.
    ///
    /// The entries below are limited to controls that navigate and nothing else. Every one is named
    /// exactly; there are no prefixes and no wildcards, because "starts with Btn_" would silently
    /// enrol tomorrow's Btn_BuyGems.
    ///
    /// WHAT IS DELIBERATELY NOT HERE, and must stay out:
    ///   - anything that spends or grants currency (Btn_Buy, Btn_Purchase, Btn_Claim, Btn_Roll)
    ///   - anything that consumes a limited daily/weekly action (Btn_Play, PLAY on a trial)
    ///   - anything that mutates the profile as a side effect of being pressed
    /// If you are unsure which category a control is in, leave it out. An unmapped edge is a gap in
    /// a diagram; a wrongly-invoked purchase is a real defect in someone's save.
    /// </summary>
    public static class UiNavigationSafelist
    {
        /// <summary>One permitted control: the screen it lives on, its exact object name, and why
        /// invoking it is safe. The reason is required - an entry nobody can justify is an entry
        /// that should not exist.</summary>
        public readonly struct Entry
        {
            public readonly string Screen;
            public readonly string Control;
            public readonly string WhySafe;

            public Entry(string screen, string control, string whySafe)
            {
                Screen = screen;
                Control = control;
                WhySafe = whySafe;
            }
        }

        private const string BackReason =
            "Back/close only. Invokes the screen's onBack/onClose callback and mutates no profile state.";

        public static readonly IReadOnlyList<Entry> Entries = new List<Entry>
        {
            new Entry("CampaignMap", "Btn_Back", BackReason),
            new Entry("Shop", "Btn_Back", BackReason),
            new Entry("Collection", "BackButton", BackReason),
            new Entry("Empire", "Btn_Back", BackReason),
            new Entry("Avatar", "Btn_Back", BackReason),
            new Entry("Settings", "Btn_Back", BackReason),
            new Entry("EmpireExpedition", "Btn_Back", BackReason),
            new Entry("GuildHallEntry", "Btn_Back", BackReason),
            new Entry("GuildExpedition", "Btn_Back", BackReason),
            new Entry("TacticalPuzzle", "Btn_ExitPuzzles", BackReason),
            new Entry("BattlePass", "Btn_Back", BackReason),
            new Entry("DailyLoginQuests", "Btn_Back", BackReason),
            new Entry("MailInbox", "Btn_Back", BackReason),
            new Entry("Friends", "Btn_Back", BackReason),
            new Entry("VipSubscription", "Btn_Back", BackReason),
            new Entry("PermitWeekKey", "Btn_Back", BackReason),
            new Entry("SpellLoadoutPicker", "Btn_Back", BackReason),
            new Entry("ChatSocial", "Btn_Back", BackReason),
            new Entry("Bazaar", "Btn_Back", BackReason),
            new Entry("MemoryExpedition", "Btn_Back", BackReason),
            new Entry("SoloCircuit", "Btn_Back", BackReason),
            new Entry("DeckBuilder", "Btn_Back_Rail", BackReason),

            // Collection's second control is a real forward navigation and spends nothing.
            new Entry("Collection", "OpenDeckBuilderButton",
                "Forward navigation to the deck builder. Opens a screen; grants and spends nothing."),
            new Entry("Empire", "OpenExpeditionButton",
                "Forward navigation to Empire Expedition. Opens a screen; the expedition is only " +
                "started by a separate control inside it."),
        };

        public static bool IsSafeToInvoke(string screen, string control)
        {
            foreach (Entry e in Entries)
            {
                if (string.Equals(e.Screen, screen, StringComparison.Ordinal) &&
                    string.Equals(e.Control, control, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;   // default deny - see the class comment, this is load-bearing
        }
    }
}
