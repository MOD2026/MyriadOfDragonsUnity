using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-BETA-SMOKE-COVERAGE-017. Reviewed the closed-beta smoke path (Startup -> Home -> WELCOME
    /// -> Start Tutorial -> Formation -> Battle -> Victory/Defeat -> Retry/Return) against existing
    /// coverage: MvpOnboardingSpineTests drives Tutorial->Home->DeckConfirm->Campaign win end to
    /// end; HomePageTutorialRewardGuardTests already clicks the real WELCOME card's Start Tutorial
    /// button (Button.onClick.Invoke(), not a direct method call) and proves it reaches
    /// StartApprovedTutorialBattle; GameBootstrapStateCoverageTests covers Formation; Victory/
    /// Defeat/Retry/Return are ResultOverlayOutcomeCoverageTests' explicit scope. All of that
    /// remains untouched here.
    ///
    /// The one real gap found: StartupSoftLandingPresenterTests.cs only ever calls
    /// PressContinueForTests() (== OnContinuePressed() directly) - it proves the Continue LOGIC
    /// works, never that the real Btn_Continue control a player actually taps is genuinely
    /// tappable (raycastable, correctly targeted, nonzero hit area). This is the single highest-
    /// stakes control in the whole journey to leave unverified this way: if its wiring ever broke,
    /// a real player would be stuck on the very first screen with no way to reach Home at all - a
    /// dead end nothing else in the existing suite would catch, since every other test enters the
    /// game past this screen via a direct test hook, never a real tap on it.
    /// </summary>
    public class StartupContinueHitAreaCoverageTests
    {
        private GameObject _spawned;

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null) Object.DestroyImmediate(_spawned);
            _spawned = null;
        }

        private Button ContinueButton()
        {
            var go = new GameObject("StartupContinueHitAreaHost");
            _spawned = go;
            var presenter = go.AddComponent<StartupSoftLandingPresenter>();
            presenter.Initialize();

            GameObject canvasObj = presenter.CanvasObjectForTests;
            Assert.IsNotNull(canvasObj, "Setup: StartupSoftLandingPresenter built no canvas.");
            Transform continueBtnTransform = canvasObj.transform.Find("Btn_Continue");
            Assert.IsNotNull(continueBtnTransform, "STATE UNREACHED: Btn_Continue was not found under the real startup canvas.");
            Button button = continueBtnTransform.GetComponent<Button>();
            Assert.IsNotNull(button, "STATE UNREACHED: Btn_Continue has no Button component.");
            return button;
        }

        [Test]
        public void ContinueButton_ImageIsRaycastable_AndIsTheButtonsTargetGraphic()
        {
            Button button = ContinueButton();
            Image image = button.GetComponent<Image>();

            Assert.IsNotNull(image, "Btn_Continue must carry the Image its Button targets.");
            Assert.IsTrue(image.raycastTarget, "Btn_Continue's Image must be raycastable, or the very first screen is visible but untappable.");
            Assert.AreSame(image, button.targetGraphic, "Btn_Continue's Button must target its own Image, not a missing/wrong graphic.");
        }

        [Test]
        public void ContinueButton_HasANonZeroValidHitRectangle_AndIsInteractable()
        {
            Button button = ContinueButton();
            var rect = (RectTransform)button.transform;

            Assert.Greater(rect.rect.width, 0f, "Btn_Continue hit area must have nonzero width.");
            Assert.Greater(rect.rect.height, 0f, "Btn_Continue hit area must have nonzero height.");
            Assert.IsTrue(button.interactable, "Btn_Continue must be interactable to receive a real first tap.");
        }

        [Test]
        public void ContinueButton_RealClick_ReachesTheSameOutcomeAsTheDirectContinueSeam()
        {
            // Compares two identically-fresh presenters rather than asserting a specific
            // downstream side effect, so this stays a relationship assertion (the real tap must
            // reach the same code path PressContinueForTests already exercises) instead of
            // re-testing Continue's own logic, which StartupSoftLandingPresenterTests.cs already
            // owns.
            var goViaClick = new GameObject("StartupContinueHitAreaHost_ViaClick");
            var presenterViaClick = goViaClick.AddComponent<StartupSoftLandingPresenter>();
            presenterViaClick.Initialize();
            Transform continueBtnTransform = presenterViaClick.CanvasObjectForTests.transform.Find("Btn_Continue");
            Button buttonViaClick = continueBtnTransform.GetComponent<Button>();

            var goViaSeam = new GameObject("StartupContinueHitAreaHost_ViaSeam");
            var presenterViaSeam = goViaSeam.AddComponent<StartupSoftLandingPresenter>();
            presenterViaSeam.Initialize();

            try
            {
                buttonViaClick.onClick.Invoke();
                presenterViaSeam.PressContinueForTests();

                // OnContinuePressed tears down the startup canvas and builds the real, unmodified
                // HomePagePresenter on the same host - both paths must reach that same real state.
                Assert.IsNotNull(goViaClick.GetComponent<HomePagePresenter>(),
                    "A real click on Btn_Continue must construct the real HomePagePresenter, same as the direct seam.");
                Assert.IsNotNull(goViaSeam.GetComponent<HomePagePresenter>(),
                    "Setup: the direct seam must also reach HomePagePresenter.");
            }
            finally
            {
                Object.DestroyImmediate(goViaClick);
                Object.DestroyImmediate(goViaSeam);
            }
        }
    }
}
