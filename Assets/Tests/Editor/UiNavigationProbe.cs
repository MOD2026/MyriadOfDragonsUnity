using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Records which navigation callback a control actually fired.
    ///
    /// THIS IS WHY EDGES WERE UNRESOLVABLE, and the fix is smaller than it looked. The capture
    /// harness builds every screen with `onBack: null`, `onLaunchBattle: null` and so on - correct
    /// for capture, since a screen must not depend on a live app to be photographed. But a null
    /// callback is a destination that does not exist, so invoking a control travelled nowhere and
    /// there was nothing to observe. It was never really about `AddListener`.
    ///
    /// Passing a RECORDING callback instead makes the wiring measurable: press the control, see
    /// which named callback fires. That is a measured fact about the real presenter, not a
    /// declaration in a table that can drift from the code.
    ///
    /// It deliberately records the CALLBACK NAME (`onBackToHome`), not a screen name. Which screen
    /// `onBackToHome` leads to is decided by whoever wires the presenter, not by the presenter -
    /// so claiming a destination here would be inventing knowledge this layer does not have.
    /// Joining callback to screen is a separate step against the real wiring.
    /// </summary>
    public sealed class UiNavigationProbe
    {
        private readonly List<string> _fired = new List<string>();

        public IReadOnlyList<string> Fired => _fired;
        public bool AnyFired => _fired.Count > 0;

        /// <summary>Returns a callback that records itself as fired. Named rather than anonymous so
        /// the recorded edge says something a reader can act on.</summary>
        public Action On(string callbackName)
        {
            return () => _fired.Add(callbackName);
        }

        public void Clear() => _fired.Clear();
    }
}
