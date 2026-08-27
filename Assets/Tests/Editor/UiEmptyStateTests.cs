using System;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Guards the two rules in the locked empty-state design that a later caller is most likely to
    /// break by trying to be helpful: no action on a Waiting/Completed state, and collapse rather
    /// than filler.
    /// </summary>
    public class UiEmptyStateTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("EmptyStateHost", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        private RectTransform NewRegion()
        {
            var go = new GameObject("Region", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(900f, 500f);
            return rt;
        }

        [Test]
        public void AWaitingEmptyState_RefusesAnAction_RatherThanRenderingADisabledButton()
        {
            // "A disabled button is not a solution" - it is the same dead end wearing a control's
            // clothes. Throwing surfaces the mistake at the call site; silently dropping the action
            // would leave the caller believing a button exists.
            Assert.Throws<ArgumentException>(() => UIEmptyState.Build(
                NewRegion(), EmptyStateKind.Waiting, "No messages",
                "Mail from the Empire will arrive here.",
                statusLine: "Last checked just now",
                actionLabel: "Refresh",
                onAction: () => { }));
        }

        [Test]
        public void ACompletedEmptyState_RefusesAnAction_SoAllCaughtUpNeverShipsADisabledClaim()
        {
            Assert.Throws<ArgumentException>(() => UIEmptyState.Build(
                NewRegion(), EmptyStateKind.Completed, "All caught up",
                "Every quest is claimed.",
                statusLine: "Resets in 4h 12m",
                actionLabel: "Claim",
                onAction: () => { }));
        }

        [Test]
        public void AWaitingEmptyState_StillCarriesItsStatusLine_SoItIsNotABareApology()
        {
            RectTransform region = NewRegion();
            UIEmptyState.Build(region, EmptyStateKind.Waiting, "No messages",
                "Mail from the Empire will arrive here.", statusLine: "Last checked just now");

            Text status = null;
            foreach (Text t in region.GetComponentsInChildren<Text>(true))
            {
                if (t.name == "EmptyState_Status") status = t;
            }

            Assert.IsNotNull(status, "A Waiting state that cannot offer an action must still say when this will matter.");
            Assert.AreEqual("Last checked just now", status.text);
        }

        [Test]
        public void AnActionableEmptyState_BuildsItsAction_AndTheActionActuallyInvokes()
        {
            RectTransform region = NewRegion();
            bool fired = false;

            UIEmptyState.Build(region, EmptyStateKind.Actionable, "No friends yet",
                "Add a commander to compare progress.", actionLabel: "Add Friends",
                onAction: () => fired = true);

            Button action = null;
            foreach (Button b in region.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "EmptyState_Action") action = b;
            }

            Assert.IsNotNull(action, "An Actionable empty state without its action is just a dead end.");
            action.onClick.Invoke();
            Assert.IsTrue(fired, "The action rendered but was wired to nothing - a button that does nothing is worse than no button.");
        }

        [Test]
        public void EveryEmptyStateLabel_MeetsTheHardTypeFloor()
        {
            RectTransform region = NewRegion();
            UIEmptyState.Build(region, EmptyStateKind.Actionable, "No friends yet",
                "Add a commander to compare progress.", statusLine: "Synced 2m ago",
                actionLabel: "Add Friends", onAction: () => { });

            foreach (Text t in region.GetComponentsInChildren<Text>(true))
            {
                Assert.GreaterOrEqual(t.fontSize, UIDesignTokens.AbsoluteMinFontSize,
                    "'" + t.name + "' renders at " + t.fontSize + "px, under the hard floor.");
            }
        }

        [Test]
        public void Build_WithIllustration_InstantiatesImageWithCorrectSprite()
        {
            RectTransform region = NewRegion();
            UIEmptyState.Build(region, EmptyStateKind.Waiting, "No messages",
                "Mail from the Empire will arrive here.", statusLine: "Last checked just now",
                illustrationPath: UIEmptyState.IllustrationNoMail);

            Image illu = null;
            foreach (Image img in region.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "EmptyState_Illustration") illu = img;
            }

            Assert.IsNotNull(illu, "EmptyState_Illustration GameObject should be created when illustrationPath is passed.");
            Assert.IsNotNull(illu.sprite, "EmptyState_Illustration sprite should load properly.");
        }

        [Test]
        public void Collapse_ZeroesTheRegion_SoNeighboursReflowInsteadOfLeavingAGap()
        {
            RectTransform region = NewRegion();
            UIEmptyState.Collapse(region);

            Assert.IsFalse(region.gameObject.activeSelf);

            var layout = region.GetComponent<LayoutElement>();
            Assert.IsNotNull(layout, "Collapse must add a LayoutElement - deactivating alone still reserves the slot in a layout group.");
            Assert.IsTrue(layout.ignoreLayout);
            Assert.AreEqual(0f, layout.preferredHeight);
        }
    }
}
