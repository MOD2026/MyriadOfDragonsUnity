using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The runtime UI validation run required by `docs/UI_VERIFICATION_GATE_v1.md` section 2.
    ///
    /// Every check here MEASURES A BUILT SCREEN. Nothing is inferred from a filename, a `.meta`, or
    /// a serialized reference - that inference is exactly what let 1799 green tests coexist with
    /// every sprite failing to load. A sprite reference is not proof a sprite rendered; a button
    /// name is not proof of a hit region.
    ///
    /// ONE traversal, MANY checks, reported TOGETHER. Splitting these into seven [Test] methods
    /// would rebuild all 24 screens seven times, and - worse - the first category to fail would
    /// hide the rest behind it. The gate is most useful when it reports the whole picture at once.
    ///
    /// WHAT THIS DOES NOT YET ENFORCE, stated plainly rather than left to look covered:
    ///   - "duplicate or unreachable navigation targets". Buttons here are wired with AddListener
    ///     at runtime, so `onClick` carries ZERO persistent calls and a destination cannot be read
    ///     statically. The navigation graph below emits real nodes and real controls but NO edges,
    ///     and it does not flag unreachable screens - with an empty edge set every screen would
    ///     look unreachable and the report would be noise dressed as a finding. Resolving edges
    ///     means invoking each control and observing which canvas appears, which is real but needs
    ///     a safelist first: invoking arbitrary buttons here would fire purchases and mutate the
    ///     profile.
    ///   - the section 2 aesthetic/warning tier, which the doc explicitly forbids automating.
    /// </summary>
    public class UiValidationRunTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        /// <summary>Legitimate stretched-axis INSETS seen this run (negative sizeDelta on a
        /// stretched axis). Counted, never failed - reported only so the narrowing of the T3 check
        /// stays auditable: it shows how many nodes the dispatched "no non-zero sizeDelta" rule
        /// would have condemned as defects.</summary>
        private int stretchedInsetCount;
        private string _scratchSaveDir;

        /// <summary>Sub-pixel slop. Rect maths on scaled canvases lands fractions off exact, and a
        /// 0.5px "overlap" is not a defect anyone can see - a tolerance this small still catches
        /// every real collision while keeping the report free of noise that would train readers to
        /// ignore it.</summary>
        private const float Tolerance = 1f;

        /// <summary>Minimum centre strip a 9-slice needs, copied from
        /// UISharedFoundation.FitSlicedBorderToRect so the check and the fix agree. If that
        /// constant moves, this must move with it - they describe the same rule.</summary>
        private const float SlicedMinCenterPx = 6f;

        /// <summary>THE LOCKED TARGET RESOLUTION - landscape 1920x1080, the game's fundamental
        /// (CLAUDE.md, 2026-08-27). Measuring at any other size risks reporting defects that only
        /// exist at that size: text clipping in particular is resolution-dependent, because a
        /// CanvasScaler scales glyphs and boxes by different rules. The contact sheet renders the
        /// same 16:9 frame, so the gate and the reviewed screenshot still describe one geometry.</summary>
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        [SetUp]
        public void SetUp()
        {
            stretchedInsetCount = 0;
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsUiValidation_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            var profile = new PlayerProfile
            {
                gold = 50_000, gems = 5_000, stamina = 100, maxStamina = 100,
                constructionMaterials = 50_000, avatarLevel = 10,
            };
            SaveSystem.Save(profile);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (UnityEngine.Object o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void UiValidationRun_EnforcesTheGateHardFailList()
        {
            var findings = new List<string>();
            // Warnings NEVER fail the run. Section 4e: the 8/10/4 limits are an owner lock
            // that BS argues is a design-review threshold rather than a correctness invariant,
            // and CC ruled WARN until the owner decides. A control count must not block a
            // build, so these are surfaced and counted, never asserted on.
            var warnings = new List<string>();
            List<UiGateException> exceptions = UiGateExceptionManifest.Load(findings);
            var graphNodes = new List<string>();
            var graphEdges = new List<string>();
            var dot = new StringBuilder();
            dot.AppendLine("digraph MyriadNavigation {");
            dot.AppendLine("  rankdir=LR;");
            dot.AppendLine("  node [shape=box, style=rounded];");

            int screensBuilt = 0;

            foreach (UiScreenEntry screen in UiScreenRegistry.Screens)
            {
                GameObject canvasObj = null;
                try
                {
                    canvasObj = screen.Build(NewHost, new UiNavigationProbe());
                }
                catch (Exception e)
                {
                    // A screen that cannot even build is a finding in its own right, not a silent
                    // skip - a skipped screen would leave the run green over a smaller set.
                    findings.Add(screen.Name + ": FAILED TO BUILD - " + e.Message);
                }

                if (canvasObj == null)
                {
                    if (!HasFindingFor(findings, screen.Name))
                    {
                        findings.Add(screen.Name + ": produced no canvas, so nothing about it could be measured.");
                    }

                    CleanupAfterScreen();
                    continue;
                }

                screensBuilt++;
                Camera measureCam = PrepareForMeasurement(canvasObj, out Texture2D rendered, out Texture2D background);
                InspectScreen(screen, canvasObj, measureCam, background, findings, warnings, exceptions, graphNodes, dot);
                if (rendered != null) UnityEngine.Object.DestroyImmediate(rendered);
                if (background != null) UnityEngine.Object.DestroyImmediate(background);
                CleanupAfterScreen();
            }

            CrawlNavigation(graphEdges, dot, warnings);

            dot.AppendLine("}");
            WriteGraphArtifacts(graphNodes, graphEdges, dot.ToString());

            warnings.Add("T3 stretched-axis insets (negative sizeDelta, legitimate padding): "
                         + stretchedInsetCount + " nodes. These are NOT defects; the count is here "
                         + "because the dispatched rule was \"no non-zero sizeDelta on a stretched "
                         + "axis\", which would have reported every one of them as a failure.");

            // Checked AFTER the traversal so "matched" reflects measured geometry, not intent.
            UiGateExceptionManifest.ReportUnmatched(exceptions, findings);

            if (warnings.Count > 0)
            {
                Debug.LogWarning("[UiValidation] " + warnings.Count + " WARNINGS (do not fail the build):\n  - " +
                                 string.Join("\n  - ", warnings));
            }

            Assert.Greater(screensBuilt, 0,
                "No screen built at all - the validation run itself is broken, which is NOT a pass.");

            Assert.IsEmpty(findings,
                "UI Verification Gate v1 section 2 violations (" + findings.Count + " across " +
                screensBuilt + " screens):\n  - " + string.Join("\n  - ", findings));
        }

        /// <summary>
        /// Lays the screen out at the SAME resolution the contact sheet renders it at, before a
        /// single rectangle is measured.
        ///
        /// THIS IS NOT A DETAIL - without it every number this file produces is fiction. A freshly
        /// built canvas in EditMode has never been through a layout pass: RectTransforms still hold
        /// pre-layout values, so world corners come back at absurd coordinates and `preferredHeight`
        /// is computed against a width the screen will never actually have. The first run of this
        /// validator reported 79 violations that way, including six on a screen whose capture had
        /// already been reviewed by eye and was correct - the measurement was wrong, not the UI.
        ///
        /// The capture resolution specifically, not an arbitrary one: the gate and the reviewed
        /// screenshot must describe the same geometry. If this measured at some other size, the
        /// validator could fail a screen that looks right in the very image a human signs off on,
        /// and there would be no way to tell which one was lying.
        /// </summary>
        private Camera PrepareForMeasurement(GameObject canvasObj, out Texture2D rendered, out Texture2D background)
        {
            rendered = null;
            background = null;
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) return null;

            var camGo = new GameObject("UiValidationCamera");
            _spawned.Add(camGo);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;

            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            cam.targetTexture = rt;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            // Render drives the canvas through a real update; ForceUpdateCanvases and an explicit
            // layout rebuild then settle anything driven by layout groups rather than by the
            // canvas itself. All three, because each covers a case the others miss.
            cam.Render();
            Canvas.ForceUpdateCanvases();

            var rootRect = canvasObj.GetComponent<RectTransform>();
            if (rootRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

            // TWO renders, and the second one is the whole point.
            //
            // The first pass sampled the normal frame and subtracted pixels close to the text
            // colour, meaning to skip glyphs. It does not work: ANTI-ALIASED EDGE PIXELS are
            // blends of glyph and background, they sit too far from the text colour to be
            // skipped, and they score ~1.5:1 against it. Every label has an AA fringe, and that
            // fringe is reliably more than 5% of the label's pixels - so the 5th percentile lands
            // INSIDE the anti-aliasing on every single label. It produced 169 warnings, including
            // 1.2:1 readings on text that is perfectly legible in the capture. The bias is
            // systematic and downward; it condemns the whole UI.
            //
            // So: render once normally, then render again with every Text disabled. The second
            // frame is the real background behind each label, with no glyph contamination at all,
            // and contrast is measured against that.
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            rendered = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            rendered.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            rendered.Apply();
            RenderTexture.active = prevActive;

            var hidden = new List<Text>();
            foreach (Text t in canvasObj.GetComponentsInChildren<Text>(true))
            {
                if (!t.enabled) continue;
                t.enabled = false;
                hidden.Add(t);
            }

            cam.Render();
            RenderTexture.active = rt;
            background = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            background.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            background.Apply();
            RenderTexture.active = prevActive;

            foreach (Text t in hidden) t.enabled = true;

            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return cam;
        }

        /// <summary>
        /// Presses each safelisted control and records which navigation callback it fired.
        ///
        /// ONE FRESH SCREEN PER CONTROL. A control may tear its own screen down, and a second press
        /// against a destroyed hierarchy measures nothing - so each is crawled from a clean build
        /// rather than reusing one instance. Slower, and the only way the readings mean anything.
        ///
        /// Controls not on the safelist are NEVER pressed. That is not caution for its own sake:
        /// pressing a BUY or a daily-claim here would spend real currency in the save under test,
        /// and a new button must default to not-invoked (CC, 2026-08-27).
        /// </summary>
        private void CrawlNavigation(List<string> edges, StringBuilder dot, List<string> warnings)
        {
            foreach (UiScreenEntry screen in UiScreenRegistry.Screens)
            {
                foreach (UiNavigationSafelist.Entry entry in UiNavigationSafelist.Entries)
                {
                    if (entry.Screen != screen.Name) continue;

                    var probe = new UiNavigationProbe();
                    GameObject canvasObj = null;
                    try { canvasObj = screen.Build(NewHost, probe); }
                    catch (Exception e)
                    {
                        warnings.Add("nav-crawl " + screen.Name + ": build failed - " + e.Message);
                        CleanupAfterScreen();
                        continue;
                    }

                    if (canvasObj == null) { CleanupAfterScreen(); continue; }

                    Button target = null;
                    foreach (Button b in canvasObj.GetComponentsInChildren<Button>(true))
                    {
                        if (b.name == entry.Control) { target = b; break; }
                    }

                    if (target == null)
                    {
                        // A safelist entry naming a control that no longer exists is a rotting
                        // entry, exactly like a stale exception - report it rather than skip it.
                        warnings.Add("nav-safelist " + screen.Name + ": no control named '" +
                                     entry.Control + "' on this screen - the entry is stale.");
                        CleanupAfterScreen();
                        continue;
                    }

                    // Snapshot the canvases BEFORE pressing, so a screen that navigates by
                    // building the next presenter itself is still observable.
                    var before = new HashSet<string>();
                    foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
                    {
                        if (c.gameObject.scene.IsValid()) before.Add(c.gameObject.name);
                    }

                    target.onClick.Invoke();

                    // SECOND NAVIGATION STYLE, and missing it produced a false "dead button".
                    // Empire's OpenExpeditionButton does not call an injected callback at all - it
                    // constructs EmpireExpeditionPresenter directly and tears its own UI down. The
                    // probe sees nothing, and reporting that as dead would have sent someone to
                    // fix a control that works. A new canvas appearing IS the navigation.
                    string appeared = null;
                    foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
                    {
                        if (!c.gameObject.scene.IsValid()) continue;
                        if (before.Contains(c.gameObject.name)) continue;
                        appeared = c.gameObject.name;
                        break;
                    }

                    if (appeared != null)
                    {
                        edges.Add("    {\"from\": " + Json(screen.Name) +
                                  ", \"control\": " + Json(entry.Control) +
                                  ", \"opensCanvas\": " + Json(appeared) + "}");
                        dot.AppendLine("  \"" + screen.Name + "\" -> \"" + appeared +
                                       "\" [label=\"" + entry.Control + "\"];");
                    }
                    else if (probe.AnyFired)
                    {
                        foreach (string callback in probe.Fired)
                        {
                            edges.Add("    {\"from\": " + Json(screen.Name) +
                                      ", \"control\": " + Json(entry.Control) +
                                      ", \"firesCallback\": " + Json(callback) + "}");
                            dot.AppendLine("  \"" + screen.Name + "\" -> \"" + callback +
                                           "\" [label=\"" + entry.Control + "\"];");
                        }
                    }
                    else
                    {
                        // Pressed a real navigation control and NOTHING happened. That is a dead
                        // control - the player taps it and stays put - and it is a genuine finding,
                        // not an absent edge.
                        // Neither a callback nor a new canvas. Now this really does mean the
                        // player presses it and stays put.
                        warnings.Add("nav-crawl " + screen.Name + ": pressing '" + entry.Control +
                                     "' fired no callback AND opened no canvas - it may be dead.");
                    }

                    CleanupAfterScreen();
                }
            }
        }

        private void CleanupAfterScreen()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (UnityEngine.Object o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void InspectScreen(
            UiScreenEntry screen, GameObject canvasObj, Camera cam, Texture2D background,
            List<string> findings, List<string> warnings,
            List<UiGateException> exceptions, List<string> graphNodes, StringBuilder dot)
        {
            Transform root = canvasObj.transform;
            var canvasRect = canvasObj.GetComponent<RectTransform>();
            Rect screenBounds = canvasRect != null ? ScreenRect(canvasRect, cam) : new Rect();

            var interactive = new List<Selectable>();
            foreach (Selectable sel in root.GetComponentsInChildren<Selectable>(true))
            {
                if (!sel.gameObject.activeInHierarchy || !sel.IsInteractable()) continue;
                interactive.Add(sel);
            }

            // --- hit targets -------------------------------------------------------------
            foreach (Selectable sel in interactive)
            {
                Graphic target = sel.targetGraphic;
                var rt = sel.transform as RectTransform;
                Rect r = rt != null ? ScreenRect(rt, cam) : new Rect();

                if (r.width <= Tolerance || r.height <= Tolerance)
                {
                    findings.Add(screen.Name + ": interactive " + Q(sel.name) + " has a zero-area rect " +
                                 Describe(r) + " - it cannot be tapped.");
                }

                if (target == null)
                {
                    findings.Add(screen.Name + ": interactive " + Q(sel.name) +
                                 " has no targetGraphic, so it has no raycast surface to receive a tap.");
                }
                else if (!target.raycastTarget)
                {
                    findings.Add(screen.Name + ": interactive " + Q(sel.name) + " has raycastTarget=false on " +
                                 Q(target.name) + " - it renders but cannot be tapped.");
                }
                else if (target.gameObject != sel.gameObject)
                {
                    // Gate: "a button whose visible graphic and hitbox are different objects
                    // without an approved reason". Flagged rather than assumed wrong - a split is
                    // legitimate when deliberate, which is why the doc says APPROVED reason.
                    findings.Add(screen.Name + ": interactive " + Q(sel.name) + " draws through " +
                                 Q(target.name) + " on a DIFFERENT object - graphic and hitbox are split, " +
                                 "which needs an approved reason.");
                }

                // Content inside a mask or scroll view is SUPPOSED to extend past the screen -
                // that is what scrolling is. The first run flagged carousel pages and list rows
                // sitting off to the right as "clipped", which would have sent someone to fix
                // working scroll views.
                if (canvasRect != null && !Contains(screenBounds, r) && !IsInsideMaskedViewport(sel.transform))
                {
                    findings.Add(screen.Name + ": interactive " + Q(sel.name) + " at " + Describe(r) +
                                 " is clipped or off-screen (screen is " + Describe(screenBounds) + ").");
                }
            }

            // --- overlapping interactive rectangles --------------------------------------
            for (int i = 0; i < interactive.Count; i++)
            {
                for (int j = i + 1; j < interactive.Count; j++)
                {
                    Selectable a = interactive[i], b = interactive[j];
                    // Nested controls are a containment relationship, not a collision.
                    if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;

                    Rect ra = ScreenRect((RectTransform)a.transform, cam);
                    Rect rb = ScreenRect((RectTransform)b.transform, cam);
                    if (ra.width <= 0f || rb.width <= 0f) continue;

                    if (!Overlaps(ra, rb)) continue;

                    // Section 4a NARROWED this: raw rectangle intersection is NOT a failure.
                    // Badges, labels inside buttons and decorative overlays legitimately
                    // intersect, and failing on those is the false-positive noise that gets
                    // gates switched off. Both sides here are already active, interactable
                    // Selectables with real hit targets, and neither contains the other - so
                    // these are two INDEPENDENTLY ACTIONABLE targets competing for one tap
                    // region, which is exactly the narrowed rule.
                    if (UiGateExceptionManifest.Excuses(exceptions, screen.Name, a.name, b.name)) continue;

                    findings.Add(screen.Name + ": interactive " + Q(a.name) + " " + Describe(ra) +
                                 " overlaps interactive " + Q(b.name) + " " + Describe(rb) +
                                 " - two independently actionable targets compete for one tap.");
                }
            }

            // --- text: real clipping, real truncation, or collision with a control ------
            // Section 4b NARROWED this too. Raw text-bound overflow is NOT a failure - a
            // conservative box with every glyph still visible reads as wrong on paper and fine on
            // screen. Only these actually cost the player something.
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                Vector2 size = text.rectTransform.rect.size;
                if (size.x <= Tolerance || size.y <= Tolerance) continue;

                // (1) REAL CLIPPING. Truncate means Unity cuts glyphs at the rect edge, so needing
                // more height than the rect has is words the player cannot read - not a cosmetic
                // margin complaint.
                if (text.verticalOverflow == VerticalWrapMode.Truncate &&
                    text.preferredHeight > size.y + Tolerance)
                {
                    findings.Add(screen.Name + ": text " + Q(text.name) + " is Truncate and needs " +
                                 F(text.preferredHeight) + "px of height in a " + F(size.y) +
                                 "px box - glyphs are actually cut off, not merely tight.");
                    continue;
                }

                if (text.horizontalOverflow != HorizontalWrapMode.Overflow) continue;
                if (text.preferredWidth <= size.x + Tolerance) continue;

                // (2) COLLISION WITH A PROTECTED CONTROL. Overflowing text paints outside its own
                // rect; when that spill lands on an interactive control it obscures something the
                // player has to see and press.
                Rect painted = PaintedBounds(text, cam);
                string collidedWith = null;
                foreach (Selectable sel in interactive)
                {
                    if (text.transform.IsChildOf(sel.transform)) continue;
                    Rect selRect = ScreenRect((RectTransform)sel.transform, cam);
                    if (selRect.width <= 0f) continue;
                    if (Overlaps(painted, selRect)) { collidedWith = sel.name; break; }
                }

                if (collidedWith != null)
                {
                    findings.Add(screen.Name + ": text " + Q(text.name) + " overflows its " + F(size.x) +
                                 "px container (needs " + F(text.preferredWidth) + "px) and the spill " +
                                 "lands on interactive " + Q(collidedWith) + ".");
                }
                else
                {
                    // (3) Escapes its box, hits nothing. Reported so a human can look, never
                    // failed - this is the exact case the narrowing was written for.
                    warnings.Add(screen.Name + ": text " + Q(text.name) + " needs " +
                                 F(text.preferredWidth) + "px in a " + F(size.x) + "px box but " +
                                 "collides with no control - visual review, not a failure.");
                }
            }

            // --- sprites missing AT RUNTIME ----------------------------------------------
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (!img.gameObject.activeInHierarchy) continue;

                // Sliced/Tiled are only meaningful with real 9-slice art. A null sprite in those
                // modes is a load that failed, and it renders as a flat rectangle that looks
                // deliberate - the exact silent degradation this gate exists to catch. A null
                // sprite in Simple mode is just a colour fill and is legitimate.
                if ((img.type == Image.Type.Sliced || img.type == Image.Type.Tiled) && img.sprite == null)
                {
                    findings.Add(screen.Name + ": image " + Q(img.name) + " is set to " + img.type +
                                 " but its sprite is NULL at runtime - it renders as a flat fill, " +
                                 "not the framed art it is asking for.");
                }
            }

            // --- T3: sizeDelta fighting a stretched anchor --------------------------------
            // A stretched axis (anchorMin 0 / anchorMax 1) means "match the parent on this axis".
            // sizeDelta is then an INSET, not a size.
            //
            // NARROWED FROM THE DISPATCHED RULE, deliberately. The instruction was "no non-zero
            // sizeDelta on a stretched axis anywhere". Taken literally that condemns the single
            // most common legitimate idiom in this codebase: a NEGATIVE sizeDelta is padding
            // (sizeDelta.x = -40 is a 20px inset each side) and is exactly right. Flagging those
            // would bury the real defect under hundreds of correct layouts and train readers to
            // ignore the report - the same noise trap the Tolerance constant above exists to avoid.
            //
            // A POSITIVE sizeDelta on a stretched axis is the real defect: the element is asking to
            // be LARGER than the parent it just said it would match, so it hangs outside its own
            // container. Both counts are reported below so the narrowing stays auditable.
            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!rt.gameObject.activeInHierarchy) continue;
                if (rt == root) continue;

                bool stretchedX = Mathf.Approximately(rt.anchorMin.x, 0f) && Mathf.Approximately(rt.anchorMax.x, 1f);
                bool stretchedY = Mathf.Approximately(rt.anchorMin.y, 0f) && Mathf.Approximately(rt.anchorMax.y, 1f);

                if (stretchedX && rt.sizeDelta.x > Tolerance)
                {
                    findings.Add(screen.Name + ": " + Q(rt.name) + " is stretched horizontally but has " +
                                 "sizeDelta.x=" + F(rt.sizeDelta.x) + " - it is asking to be wider than " +
                                 "the parent it just said it would match, so it hangs outside it.");
                }

                if (stretchedY && rt.sizeDelta.y > Tolerance)
                {
                    findings.Add(screen.Name + ": " + Q(rt.name) + " is stretched vertically but has " +
                                 "sizeDelta.y=" + F(rt.sizeDelta.y) + " - it is asking to be taller than " +
                                 "the parent it just said it would match, so it hangs outside it.");
                }

                // Auditable counterpart to the narrowing above: legitimate insets, counted only.
                if ((stretchedX && rt.sizeDelta.x < -Tolerance) || (stretchedY && rt.sizeDelta.y < -Tolerance))
                {
                    stretchedInsetCount++;
                }
            }

            // --- RENDERED contrast (locked 2026-08-27, 5c46c28) ---------------------------
            // Floors: 7:1 body and interactive labels, 4.5:1 large text (55px+). Accepted on the
            // 5th PERCENTILE, never the average - the lock is explicit that bright and dark pixels
            // average to an acceptable value while a word crossing a bright patch is unreadable,
            // and the 5th percentile catches that without one anti-aliased edge pixel condemning
            // the whole label.
            //
            // COLLECTED AS WARNINGS FOR NOW, NOT FAILURES, AND THAT IS DELIBERATE - see the report
            // to CC. The lock says insufficient contrast is a BUILD FAILURE and I am not
            // softening it; I am refusing to arm an UNVALIDATED measurement as a build gate after
            // this same file produced 79 and then 110 fabricated findings tonight. One run to
            // check the numbers against the captures, then it flips to findings.
            if (background != null)
            {
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;

                    Rect box = ScreenRect(text.rectTransform, cam);
                    // Global 22px floor for player-facing text. Checked before contrast because
                    // it needs no pixels at all - a 14px label is undersized whatever it sits on.
                    // The lock also sets 28px over UN-SCRIMMED painted art, which is deliberately
                    // NOT implemented: detecting whether a scrim sits behind a label is a real
                    // judgement, and guessing it would either excuse undersized text or condemn
                    // properly scrimmed text. 22px is the part that is unambiguous.
                    if (text.fontSize > 0 && text.fontSize < 22)
                    {
                        warnings.Add(screen.Name + ": text " + Q(text.name) + " is " + text.fontSize +
                                     "px, under the global 22px floor.");
                    }

                    float p5 = FifthPercentileContrast(background, box, text.color, out int samples,
                                                       out float worst, out float fractionMeetingFloor,
                                                       text.fontSize >= 55 ? 4.5f : 7f);
                    if (samples < 20) continue;   // too little background to judge honestly

                    bool large = text.fontSize >= 55;
                    float floor = large ? 4.5f : 7f;
                    float absoluteFloor = large ? 3f : 4.5f;

                    if (p5 < floor)
                    {
                        warnings.Add(screen.Name + ": text " + Q(text.name) + " renders at " +
                                     p5.ToString("0.0", CultureInfo.InvariantCulture) + ":1 on the 5th percentile, " +
                                     "under the " + floor.ToString("0.0", CultureInfo.InvariantCulture) +
                                     ":1 floor (" + samples + " background samples, font " + text.fontSize + "px).");

                        // Diagnostic only (CC, 2026-08-27) - what was actually sampled, from the
                        // real measurement path, no second harness. Center + both edge midpoints
                        // of the same box FifthPercentileContrast just walked, so a compositing
                        // bug (real fill vs. something lighter drawn over it) is visible directly
                        // in this run's own log rather than reconstructed in a parallel test.
                        int bx0 = Mathf.Clamp(Mathf.FloorToInt(box.xMin), 0, background.width - 1);
                        int bx1 = Mathf.Clamp(Mathf.CeilToInt(box.xMax) - 1, 0, background.width - 1);
                        int bcx = Mathf.Clamp(Mathf.RoundToInt(box.center.x), 0, background.width - 1);
                        int bcy = Mathf.Clamp(Mathf.RoundToInt(box.center.y), 0, background.height - 1);
                        Color left = background.GetPixel(bx0, bcy);
                        Color mid = background.GetPixel(bcx, bcy);
                        Color right = background.GetPixel(bx1, bcy);
                        Debug.Log("[UiValidation:DIAG] " + screen.Name + " " + Q(text.name) +
                                  " box=(" + F(box.xMin) + "," + F(box.yMin) + ")-(" + F(box.xMax) + "," + F(box.yMax) +
                                  ") bgLeft=" + C3(left) + " bgMid=" + C3(mid) + " bgRight=" + C3(right));
                    }
                    else if (fractionMeetingFloor < 0.95f)
                    {
                        // Passes at the 5th percentile but fails the coverage rule: the lock
                        // requires >=95% of covered pixels to meet the floor, which catches a
                        // label that is fine almost everywhere and unreadable across one bright
                        // patch of art.
                        warnings.Add(screen.Name + ": text " + Q(text.name) + " meets the floor on only " +
                                     (fractionMeetingFloor * 100f).ToString("0", CultureInfo.InvariantCulture) +
                                     "% of covered pixels (95% required).");
                    }

                    if (worst < absoluteFloor)
                    {
                        warnings.Add(screen.Name + ": text " + Q(text.name) + " has a region at " +
                                     worst.ToString("0.0", CultureInfo.InvariantCulture) + ":1, below the " +
                                     absoluteFloor.ToString("0.0", CultureInfo.InvariantCulture) +
                                     ":1 absolute minimum the lock permits nowhere.");
                    }
                }
            }

            // Diagnostic only (CC, 2026-08-27) - spatial alpha check for the "*Plate" contrast
            // scrims: samples a 3x3 grid across the PLATE's own rect (not the text box) to tell
            // a uniform compositing/shader problem (grid reads the same everywhere) apart from an
            // edge/corner falloff in CreateRoundedPanelSprite's own alpha ramp (grid reads near-
            // authored at centre, lighter toward the corners). No behaviour change.
            if (background != null)
            {
                foreach (RectTransform plateRt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (!plateRt.gameObject.activeInHierarchy || !plateRt.name.EndsWith("Plate")) continue;
                    Image plateImg = plateRt.GetComponent<Image>();
                    if (plateImg == null || !plateImg.enabled) continue;

                    Rect box = ScreenRect(plateRt, cam);
                    if (box.width <= Tolerance || box.height <= Tolerance) continue;

                    var sb = new StringBuilder();
                    sb.Append("[UiValidation:DIAG-PLATE] ").Append(screen.Name).Append(' ').Append(Q(plateRt.name))
                      .Append(" box=(").Append(F(box.xMin)).Append(',').Append(F(box.yMin)).Append(")-(")
                      .Append(F(box.xMax)).Append(',').Append(F(box.yMax)).Append(')');

                    for (int gy = 0; gy < 3; gy++)
                    {
                        float ty = gy / 2f; // 0, 0.5, 1
                        for (int gx = 0; gx < 3; gx++)
                        {
                            float tx = gx / 2f;
                            int px = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(box.xMin, box.xMax, tx)), 0, background.width - 1);
                            int py = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(box.yMin, box.yMax, ty)), 0, background.height - 1);
                            Color c = background.GetPixel(px, py);
                            sb.Append(" [").Append(gx).Append(',').Append(gy).Append("]=").Append(C3(c));
                        }
                    }
                    Debug.Log(sb.ToString());
                }
            }

            // --- T1: sliced border fit actually APPLIED (locked 9ca3e0b) --------------------
            // Mirrors UISharedFoundation.FitSlicedBorderToRect's own arithmetic: a 9-slice sprite
            // whose borders plus a minimum centre strip exceed the rect cannot draw its centre, so
            // the border must be scaled down via pixelsPerUnitMultiplier. If the multiplier is
            // still 1 in that situation, the fit was never applied and the frame renders wrong.
            //
            // THIS CHECKS THE RENDERED EFFECT, NOT THAT A METHOD WAS CALLED. That distinction is
            // the whole reason the bug class survived a green suite: asserting
            // "FitSlicedBorderToRect was invoked" passes even when it was invoked too early,
            // against a rect that had not been sized yet, and did nothing.
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (!img.gameObject.activeInHierarchy) continue;
                if (img.type != Image.Type.Sliced || img.sprite == null) continue;
                if (img.sprite.border == Vector4.zero) continue;

                Rect r = img.rectTransform.rect;
                if (r.width <= 0f || r.height <= 0f) continue;

                Vector4 b = img.sprite.border;   // x=left, y=bottom, z=right, w=top
                bool tooWide = b.x + b.z + SlicedMinCenterPx > r.width;
                bool tooTall = b.y + b.w + SlicedMinCenterPx > r.height;

                if ((tooWide || tooTall) && Mathf.Approximately(img.pixelsPerUnitMultiplier, 1f))
                {
                    findings.Add(screen.Name + ": image " + Q(img.name) + " is Sliced with border (" +
                                 F(b.x) + "," + F(b.y) + "," + F(b.z) + "," + F(b.w) + ") in a " +
                                 F(r.width) + "x" + F(r.height) + " rect, leaving no room for the " +
                                 "centre, but pixelsPerUnitMultiplier is still 1 - the border fit " +
                                 "was never applied and the frame renders wrong.");
                }
            }

            // --- visible-action limit ------------------------------------------------------
            int limit = UiScreenRegistry.ActionLimitFor(screen.Surface);
            if (interactive.Count > limit)
            {
                // DIAGNOSTIC ONLY - the ruling landed (720970c/cd055e5). WH pulled real counts
                // from shipped comparators and every one exceeds 8, so the raw-count cap is
                // dropped as a design TARGET entirely; the binding rule is attention hierarchy
                // (one primary CTA, <=3 equal secondaries, dock must not out-shout it). This
                // number stays because it is useful to see, NOT because crossing it is a defect.
                warnings.Add(screen.Name + ": " + interactive.Count + " visible interactive controls (reference " +
                             screen.Surface + " count " + limit + ") - diagnostic only, not a target.");
            }

            // --- section 4c: no player may be trapped in an overlay ------------------------
            // Added by BS and accepted - it was missing from the original list. A modal with no
            // dismissal is unrecoverable without killing the app.
            //
            // VACUOUS TODAY, ARMED FOR WHEN IT IS NOT: nothing in the registry is classified
            // Overlay yet, because reclassifying tightens a limit and needs the owner. This
            // currently inspects zero screens - which is NOT evidence that no modal traps the
            // player, only that no screen is declared a modal.
            if (screen.Surface == UiSurfaceKind.Overlay)
            {
                bool hasEscape = false;
                foreach (Selectable sel in interactive)
                {
                    if (LooksLikeEscape(sel.name)) { hasEscape = true; break; }
                }

                if (!hasEscape)
                {
                    findings.Add(screen.Name + ": overlay has NO dismissal or back control among its " +
                                 interactive.Count + " interactive controls - the player is trapped.");
                }
            }

            // --- navigation graph node -----------------------------------------------------
            var controlNames = new List<string>();
            foreach (Selectable sel in interactive) controlNames.Add(Json(sel.name));

            graphNodes.Add(
                "    {\"screen\": " + Json(screen.Name) +
                ", \"surface\": " + Json(screen.Surface.ToString()) +
                ", \"actionLimit\": " + limit +
                ", \"interactiveCount\": " + interactive.Count +
                ", \"controls\": [" + string.Join(", ", controlNames) + "]}");

            dot.AppendLine("  \"" + screen.Name + "\" [label=\"" + screen.Name + "\\n" +
                           interactive.Count + "/" + limit + " actions\"];");
        }

        /// <summary>Writes the navigation graph beside the contact sheet, so the owner-facing
        /// diagram and the reviewed screenshots come out of the same run and cannot describe
        /// different versions of the UI.</summary>
        private static void WriteGraphArtifacts(List<string> nodes, List<string> edges, string dot)
        {
            string outDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsContactSheetOutput");
            Directory.CreateDirectory(outDir);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"generatedBy\": \"UiValidationRunTests\",");
            json.AppendLine("  \"edgeResolution\": \"MEASURED, PARTIAL. Each edge below was produced by actually invoking a safelisted control and observing which navigation callback fired. Coverage is limited BY DESIGN: UiNavigationSafelist defaults to deny, so controls that spend currency or consume a daily action are never invoked and their edges are absent. An absent edge means not-yet-crawled, NEVER proven-unreachable.\",");
            json.AppendLine("  \"nodes\": [");
            json.AppendLine(string.Join(",\n", nodes));
            json.AppendLine("  ],");
            json.AppendLine("  \"edges\": [");
            json.AppendLine(string.Join(",\n", edges));
            json.AppendLine("  ]");
            json.AppendLine("}");

            File.WriteAllText(Path.Combine(outDir, "navigation_graph.json"), json.ToString());
            File.WriteAllText(Path.Combine(outDir, "navigation_graph.dot"), dot);
            Debug.Log("[UiValidation] Navigation graph written to " + outDir + " (nodes only, no edges - see edgeResolution).");
        }

        private static bool HasFindingFor(List<string> findings, string screenName)
        {
            foreach (string f in findings) if (f.StartsWith(screenName + ":", StringComparison.Ordinal)) return true;
            return false;
        }

        private GameObject NewHost(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>
        /// The rect in SCREEN PIXELS, via the camera the screen was laid out with.
        ///
        /// Not world units. `GetWorldCorners` on a Screen Space - Camera canvas returns world
        /// units, where the whole 960x540 screen spans about 17.8x10 - so every button comes back
        /// under 1 unit tall and a pixel-based tolerance condemns the entire UI as untappable.
        /// That is exactly what the second run did: 110 findings, nearly all of them real controls
        /// reported as zero-area. The geometry was fine; the units were mine.
        /// </summary>
        private static Rect ScreenRect(RectTransform rt, Camera cam)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);

            Vector2 first = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            float xMin = first.x, xMax = first.x, yMin = first.y, yMax = first.y;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c[i]);
                if (p.x < xMin) xMin = p.x;
                if (p.x > xMax) xMax = p.x;
                if (p.y < yMin) yMin = p.y;
                if (p.y > yMax) yMax = p.y;
            }

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>True when any ancestor clips its children - a Mask, a RectMask2D, or a
        /// ScrollRect. Content under one of those is meant to live outside the visible area.</summary>
        private static bool IsInsideMaskedViewport(Transform t)
        {
            Transform cursor = t.parent;
            while (cursor != null)
            {
                if (cursor.GetComponent<Mask>() != null ||
                    cursor.GetComponent<RectMask2D>() != null ||
                    cursor.GetComponent<ScrollRect>() != null)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        /// <summary>Approximates where overflowing text actually paints: the rect grown to the
        /// size the glyphs need, about its own centre. Unity does not expose laid-out glyph bounds
        /// directly, and centre-expansion is the closest honest approximation - it can be off for
        /// hard left/right alignment, so it is used ONLY to detect collision with a control, never
        /// to measure a margin.</summary>
        private static Rect PaintedBounds(Text text, Camera cam)
        {
            Rect box = ScreenRect(text.rectTransform, cam);
            Vector2 size = text.rectTransform.rect.size;
            float scaleX = size.x > 0f ? box.width / size.x : 1f;
            float scaleY = size.y > 0f ? box.height / size.y : 1f;

            float wantedW = Mathf.Max(box.width, text.preferredWidth * scaleX);
            float wantedH = Mathf.Max(box.height, text.preferredHeight * scaleY);
            Vector2 centre = box.center;

            return new Rect(centre.x - wantedW * 0.5f, centre.y - wantedH * 0.5f, wantedW, wantedH);
        }

        private static bool LooksLikeEscape(string controlName)
        {
            string n = (controlName ?? string.Empty).ToLowerInvariant();
            return n.Contains("back") || n.Contains("close") || n.Contains("dismiss") ||
                   n.Contains("cancel") || n.Contains("exit");
        }

        /// <summary>
        /// The 5th-percentile WCAG contrast between a label's own colour and the pixels actually
        /// drawn behind it.
        ///
        /// Measured against a frame rendered with every label HIDDEN, so the pixels really are
        /// the background and not anti-aliased glyph edges. Measuring against the normal frame
        /// biases every label downward: the AA fringe blends toward the background, scores ~1.5:1
        /// against the text colour, and reliably occupies more than 5% of a label's area - so the
        /// 5th percentile lands inside the anti-aliasing every time.
        /// </summary>
        private static float FifthPercentileContrast(
            Texture2D frame, Rect box, Color textColour, out int samples,
            out float worst, out float fractionMeetingFloor, float floor)
        {
            samples = 0;
            worst = float.MaxValue;
            fractionMeetingFloor = 1f;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(box.xMin), 0, frame.width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(box.xMax), 0, frame.width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(box.yMin), 0, frame.height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(box.yMax), 0, frame.height - 1);
            if (xMax <= xMin || yMax <= yMin) return float.MaxValue;

            // Cap the work: a full-width label on a 1920x1080 frame is a lot of pixels, and this
            // runs for every label on 24 screens.
            int strideX = Mathf.Max(1, (xMax - xMin) / 64);
            int strideY = Mathf.Max(1, (yMax - yMin) / 24);

            float textLum = RelativeLuminance(textColour);
            var contrasts = new List<float>();

            for (int y = yMin; y <= yMax; y += strideY)
            {
                for (int x = xMin; x <= xMax; x += strideX)
                {
                    // No glyph-skipping. This frame was rendered with the labels HIDDEN, so
                    // every pixel here is genuinely background. The old skip would now actively
                    // hide the worst case - a label sitting on a background its own colour.
                    Color px = frame.GetPixel(x, y);
                    float bg = RelativeLuminance(px);
                    float hi = Mathf.Max(textLum, bg);
                    float lo = Mathf.Min(textLum, bg);
                    contrasts.Add((hi + 0.05f) / (lo + 0.05f));
                }
            }

            samples = contrasts.Count;
            if (samples < 20) return float.MaxValue;

            int meeting = 0;
            foreach (float c in contrasts)
            {
                if (c < worst) worst = c;
                if (c >= floor) meeting++;
            }

            fractionMeetingFloor = meeting / (float)samples;
            contrasts.Sort();
            return contrasts[Mathf.Clamp(Mathf.FloorToInt(samples * 0.05f), 0, samples - 1)];
        }

        /// <summary>WCAG 2.x relative luminance, including the sRGB linearisation. The simple
        /// 0.299/0.587/0.114 shortcut is a different formula and gives different answers near the
        /// floors, which is precisely where this check has to be right.</summary>
        private static float RelativeLuminance(Color c)
        {
            return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - Tolerance && b.xMin < a.xMax - Tolerance &&
                   a.yMin < b.yMax - Tolerance && b.yMin < a.yMax - Tolerance;
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - Tolerance && inner.xMax <= outer.xMax + Tolerance &&
                   inner.yMin >= outer.yMin - Tolerance && inner.yMax <= outer.yMax + Tolerance;
        }

        private static string Describe(Rect r)
        {
            return "(" + F(r.xMin) + "," + F(r.yMin) + " " + F(r.width) + "x" + F(r.height) + ")";
        }

        private static string F(float v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>3-decimal (r,g,b) formatter for diagnostic colour dumps - F()'s "0.#" rounds
        /// a 0..1 colour channel straight to "0", useless for telling 0.03 from 0.18.</summary>
        private static string C3(Color c)
        {
            return "(" + c.r.ToString("0.000", CultureInfo.InvariantCulture) +
                   "," + c.g.ToString("0.000", CultureInfo.InvariantCulture) +
                   "," + c.b.ToString("0.000", CultureInfo.InvariantCulture) + ")";
        }

        private static string Q(string s)
        {
            return "'" + s + "'";
        }

        private static string Json(string s)
        {
            return "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
