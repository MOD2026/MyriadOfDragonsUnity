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

        GameObject backing = new GameObject("Backing", typeof(RectTransform), typeof(Image));
        backing.transform.SetParent(_canvasObj.transform, false);
        UISharedFoundation.StretchFull(backing.GetComponent<RectTransform>());
        backing.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 1f);

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

    private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
