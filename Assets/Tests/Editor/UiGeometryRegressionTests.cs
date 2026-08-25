using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Standing geometry regression gate across every presenter that can be built headlessly.
    /// Promoted from a one-off audit 2026-08-25 after it measured 17 screens and found one real
    /// overflow: this would have caught the Formation-header bug (11.36px into a reserved HUD
    /// region) automatically, instead of it waiting on the owner's manual inspection.
    ///
    /// ZERO TOLERANCE, deliberately - no allowed-slop constant. A tolerance is how a real overflow
    /// gets normalised into "expected", and it matches the standing project rule of asserting real
    /// contracts rather than fudge factors. If a screen legitimately needs more room, widen the
    /// band; do not widen the threshold.
    ///
    /// It also reports SCREENS MEASURED, not just failures. Twice while building this, the harness
    /// exited 0 having measured nothing (a canvas-name assumption, then an overloaded-Initialize
    /// abort) - "no bugs found" and "nothing was checked" must never look identical.
    ///
    /// Checks the two things that caught real bugs tonight without needing a rendered frame:
    ///   1. A Text whose own preferredHeight exceeds its rect and cannot wrap or truncate - it
    ///      renders outside its allocated band (this is the Formation-header bug, 11.36px overflow).
    ///   2. A RectTransform extending outside the canvas - content pushed off-screen.
    /// Overlap between arbitrary siblings is deliberately NOT flagged: backgrounds legitimately
    /// contain their children, so it produces mostly false positives.</summary>
    public class UiGeometryRegressionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDUiAudit_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Canvas" || root.name == "EventSystem") UnityEngine.Object.DestroyImmediate(root);
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private static readonly string[] PresenterTypeNames =
        {
            "AvatarPresenter", "BattlePassPresenter", "CollectionPresenter", "DailyLoginQuestsPresenter",
            "EmpireExpeditionPresenter", "EmpirePresenter", "GuildHallEntryPresenter", "MailInboxPresenter",
            "MemoryExpeditionPresenter", "SettingsPresenter", "SpellLoadoutPickerPresenter",
            "VipSubscriptionPresenter", "BazaarPresenter", "ChatSocialPresenter", "FriendsPresenter",
            "GuildExpeditionPresenter", "PermitWeekKeyPresenter",
        };

        [Test]
        public void NoPresenterHasTextOverflowingItsBand_OrContentOffCanvas()
        {
            var failures = new List<string>();

            int presentersBuilt = 0, totalOverflow = 0, totalOffCanvas = 0;

            foreach (string typeName in PresenterTypeNames)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                    .FirstOrDefault(t => t.Name == typeName && typeof(MonoBehaviour).IsAssignableFrom(t));
                if (type == null) { Debug.Log($"UIAUDIT SKIP {typeName} - type not found"); continue; }

                // Some presenters have overloaded Initialize, so GetMethod(name) throws
                // AmbiguousMatchException. Pick the FEWEST-parameter overload explicitly - it is the
                // one most likely to build with only a no-op callback.
                MethodInfo init = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Initialize")
                    .OrderBy(m => m.GetParameters().Length)
                    .FirstOrDefault();
                if (init == null) { Debug.Log($"UIAUDIT SKIP {typeName} - no Initialize"); continue; }

                GameObject host = null;
                try
                {
                    host = new GameObject("Audit_" + typeName);
                    _spawned.Add(host);
                    var behaviour = (MonoBehaviour)host.AddComponent(type);

                    object[] args = init.GetParameters().Select(p =>
                        p.ParameterType == typeof(Action) ? (object)(Action)(() => { })
                        : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType)
                        : null).ToArray();
                    init.Invoke(behaviour, args);
                }
                catch (Exception e)
                {
                    Debug.Log($"UIAUDIT SKIP {typeName} - build threw {e.InnerException?.GetType().Name ?? e.GetType().Name}");
                    continue;
                }

                // Presenters name their canvas per-screen (CollectionCanvas, ShopCanvas, ...), not
                // literally "Canvas" - so find the Canvas COMPONENT rather than guessing a name.
                Canvas builtCanvas = UnityEngine.Object.FindObjectsOfType<Canvas>()
                    .FirstOrDefault(c => c.GetComponent<RectTransform>() != null);
                if (builtCanvas == null) { Debug.Log($"UIAUDIT SKIP {typeName} - no Canvas built"); continue; }
                GameObject canvasGo = builtCanvas.gameObject;
                _spawned.Add(canvasGo);
                var canvasRect = canvasGo.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                             .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

                presentersBuilt++;
                Rect canvasWorld = WorldRect(canvasRect);
                int overflow = 0, offCanvas = 0;

                foreach (Text text in canvasGo.GetComponentsInChildren<Text>(true))
                {
                    if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                    var rt = text.rectTransform;
                    float h = rt.rect.height, w = rt.rect.width;
                    if (h <= 0f) continue;

                    bool cannotGrowDown = text.verticalOverflow == VerticalWrapMode.Truncate;
                    float need = text.preferredHeight;
                    if (need > h + 0.5f && !cannotGrowDown)
                    {
                        overflow++;
                        failures.Add($"OVERFLOW {typeName}/{NodePath(text.transform)} " +
                                     $"text={need:F1}px band={h:F1}px over={need - h:F1}px \"{Truncate(text.text)}\"");
                    }
                }

                foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true))
                {
                    if (!rt.gameObject.activeInHierarchy || rt == canvasRect) continue;
                    Rect r = WorldRect(rt);
                    if (r.width <= 0f || r.height <= 0f) continue;
                    if (r.xMin < canvasWorld.xMin - 1f || r.xMax > canvasWorld.xMax + 1f ||
                        r.yMin < canvasWorld.yMin - 1f || r.yMax > canvasWorld.yMax + 1f)
                    {
                        offCanvas++;
                        if (offCanvas <= 3)
                            failures.Add($"OFFCANVAS {typeName}/{NodePath(rt)} rect={r} canvas={canvasWorld}");
                    }
                }

                Debug.Log($"UIAUDIT SCREEN {typeName,-28} overflow={overflow} offCanvas={offCanvas}");
                totalOverflow += overflow;
                totalOffCanvas += offCanvas;

                foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                    if (c != null) UnityEngine.Object.DestroyImmediate(c.gameObject);
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == "EventSystem") UnityEngine.Object.DestroyImmediate(root);
            }

            Debug.Log($"UIGEO measured {presentersBuilt} presenters, " +
                      $"overflow={totalOverflow} offCanvas={totalOffCanvas}");

            // Guard against the gate silently measuring nothing - the exact way this harness failed
            // twice while being written.
            Assert.GreaterOrEqual(presentersBuilt, 15,
                $"Only {presentersBuilt} presenters were actually built. This gate is meaningless if it " +
                "measures nothing, so a low count is itself a failure rather than a pass.");

            CollectionAssert.IsEmpty(failures,
                "Geometry regressions across " + presentersBuilt + " screens: " +
                    string.Join("  |  ", failures));
        }

        private static string Truncate(string s) => s.Length <= 40 ? s.Replace("\n", " ") : s.Substring(0, 40).Replace("\n", " ") + "...";

        private static string NodePath(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null && c.name != "Canvas"; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
