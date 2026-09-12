using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VS-UI-SOFT-LANDING-STARTUP-001. The first thing GameBootstrapLoader.Bootstrap() puts on screen
/// - before Home, not instead of it. A single procedural card: a status line that reads the real
/// local save-file signal (SaveSystem.Exists, read-only - never touched before this decides what
/// to say) to tell a first-run device from a returning one, and one Continue button that always
/// routes into the existing, completely unmodified Home flow.
///
/// Deliberately does NOT build Sign In / Create Account / Try Again. UnityAuthenticationSocialService
/// exposes a real BootstrapIdentityAsync gateway, but nothing anywhere binds it to a screen today -
/// wiring a live Unity Cloud network call into the game's very first frame is a materially bigger,
/// riskier change (latency, retry/error UI, offline handling) than this slice's "smallest safe
/// startup flow" scope covers, and gating existing users' path to Home behind an unproven network
/// call would risk this task's own "existing users still reach Home" requirement. Left for its own
/// dispatch once that gateway has a first real caller to design around.
///
/// Does NOT build a separate avatar-selection screen either - none exists anywhere in the project
/// (verified: no AvatarSelect/CreateAvatar presenter, no name/portrait/faction picker). Home's own
/// existing HomeFeed already carries the real first-run state (a "WELCOME ... Start Tutorial" card,
/// driven by the player's tutorial step) - that IS the existing equivalent, and it is reached
/// automatically once Continue lands the player on Home. Nothing here invents a name, portrait,
/// faction, reward, or lore line beyond that.
/// </summary>
public class StartupSoftLandingPresenter : MonoBehaviour
{
    public const string CanvasName = "StartupSoftLandingCanvas";

    private GameObject _canvasObj;
    private bool _isFirstRun;

    /// <summary>Exposed for tests: read-only access to the canvas Initialize() builds.</summary>
    public GameObject CanvasObjectForTests => _canvasObj;

    /// <summary>Exposed for tests: the first-run/returning read Initialize() made.</summary>
    public bool IsFirstRunForTests => _isFirstRun;

    void Awake() => Initialize();

    /// <summary>Exposed for tests: same entry point Awake() uses, callable without a real
    /// RuntimeInitializeOnLoad boot.</summary>
    public void Initialize()
    {
        // Read-only, and read BEFORE anything else in this method (or SaveManager.Load(), which
        // Home's own Start() calls) can lazily create the profile file - SaveSystem.CurrentProfile
        // auto-creates on first touch, so this is the one signal that must run first to still mean
        // anything.
        _isFirstRun = !SaveSystem.Exists;
        BuildStartupUI();
    }

    private void BuildStartupUI()
    {
        TeardownUI();

        Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920f, 1080f));
        _canvasObj = canvas.gameObject;

        // Startup's own APPROVED_PRODUCTION asset (docs/REVAMP_V2_APPROVAL_REGISTRY.md's
        // `01_home_first_login.png` row, sha256 7DFCA63D...F5C5A3, Zihan owner decision
        // 2026-09-02), packaged by WH at this path (a4a15e21) - verified byte-for-byte via
        // certutil against the registry hash before wiring this. Replaces an earlier home_v2
        // substitution that bound a DIFFERENT approved asset (Home's own 02_home.png) under
        // Startup's name - not an exact-path match, corrected here now the real asset exists on
        // this branch. Non-raycastable, same VS-REVAMPV2-009 rule as Empire/Home: decoration
        // spanning the whole canvas must never intercept the Continue tap.
        Image backing = UISharedFoundation.CreateFullscreenBackground(
            _canvasObj.transform, "UI/RevampV2Approved/Startup/startup_first_login_v1", new Color(0.06f, 0.07f, 0.10f, 1f));
        backing.raycastTarget = false;

        Text title = UISharedFoundation.CreateText(_canvasObj.transform, "Title", "MYRIAD OF DRAGONS",
            UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true, new Vector2(1200f, 96f));
        title.fontSize = 48;
        title.fontStyle = FontStyle.Bold;
        SetNorm(title.rectTransform, 0.1f, 0.56f, 0.9f, 0.68f);

        // The only text that differs by state, and both lines are pure UI copy - no invented
        // account facts, no name, no lore.
        string subtitle = _isFirstRun
            ? "Welcome. Tap Continue to enter your Empire."
            : "Welcome back. Tap Continue to return to your Empire.";
        Text subtitleText = UISharedFoundation.CreateText(_canvasObj.transform, "Subtitle", subtitle,
            UITextRole.Body, TextAnchor.MiddleCenter, new Color(0.72f, 0.75f, 0.8f), true, new Vector2(1000f, 48f));
        subtitleText.fontSize = 24;
        SetNorm(subtitleText.rectTransform, 0.15f, 0.47f, 0.85f, 0.55f);

        GameObject continueBtn = new GameObject("Btn_Continue", typeof(RectTransform), typeof(Image), typeof(Button));
        continueBtn.transform.SetParent(_canvasObj.transform, false);
        Image continueImg = continueBtn.GetComponent<Image>();
        Button continueButton = continueBtn.GetComponent<Button>();
        HomeV3UiLibrary.ApplyPrimaryActionButton(continueButton, continueImg);
        SetNorm(continueBtn.GetComponent<RectTransform>(), 0.40f, 0.30f, 0.60f, 0.38f);
        continueButton.onClick.AddListener(OnContinuePressed);

        Text continueLabel = UISharedFoundation.CreateText(continueBtn.transform, "Text", "CONTINUE",
            UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(220f, 40f));
        continueLabel.fontSize = 24;
        continueLabel.fontStyle = FontStyle.Bold;
    }

    /// <summary>Exposed for tests: exercises exactly what Btn_Continue's onClick does, without
    /// needing a simulated UI click.</summary>
    public void PressContinueForTests() => OnContinuePressed();

    private void OnContinuePressed()
    {
        // Both first-run and returning route here - Home already carries its own real first-run
        // state (the WELCOME/Start Tutorial feed card) and nothing here duplicates or overrides
        // it. Same construction GameBootstrapLoader.Bootstrap() used to perform directly.
        TeardownUI();
        gameObject.AddComponent<HomePagePresenter>();
        // DestroyImmediate, not Destroy: this runs from Initialize()'s call chain (Awake() at real
        // boot, or a direct test call), and Destroy() logs an EditMode error / is deferred rather
        // than applied, same rule Initialize()-reachable code follows project-wide.
        DestroyImmediate(this);
    }

    private void TeardownUI()
    {
        if (_canvasObj != null) DestroyImmediate(_canvasObj);
        _canvasObj = null;
    }

    // CreateScreenCanvas builds a standalone, root-level GameObject (not parented under this
    // component's host) - destroying the host alone leaves it orphaned in the scene, exactly the
    // real leak Background_BindsTheExactApprovedStartupAsset (the first test here that builds and
    // inspects without ever pressing Continue) surfaced against a later test's GameObject.Find.
    // Matches the established pattern (e.g. EmpirePresenter.OnDestroy) project-wide.
    private void OnDestroy() => TeardownUI();

    private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
