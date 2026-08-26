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
        private string _scratchSaveDir;

        /// <summary>Sub-pixel slop. Rect maths on scaled canvases lands fractions off exact, and a
        /// 0.5px "overlap" is not a defect anyone can see - a tolerance this small still catches
        /// every real collision while keeping the report free of noise that would train readers to
        /// ignore it.</summary>
        private const float Tolerance = 1f;

        [SetUp]
        public void SetUp()
        {
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
            var graphNodes = new List<string>();
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
                    canvasObj = screen.Build(NewHost);
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
                InspectScreen(screen, canvasObj, findings, graphNodes, dot);
                CleanupAfterScreen();
            }

            dot.AppendLine("}");
            WriteGraphArtifacts(graphNodes, dot.ToString());

            Assert.Greater(screensBuilt, 0,
                "No screen built at all - the validation run itself is broken, which is NOT a pass.");

            Assert.IsEmpty(findings,
                "UI Verification Gate v1 section 2 violations (" + findings.Count + " across " +
                screensBuilt + " screens):\n  - " + string.Join("\n  - ", findings));
        }

        private void CleanupAfterScreen()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (UnityEngine.Object o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void InspectScreen(
            UiScreenEntry screen, GameObject canvasObj, List<string> findings,
            List<string> graphNodes, StringBuilder dot)
        {
            Transform root = canvasObj.transform;
            var canvasRect = canvasObj.GetComponent<RectTransform>();
            Rect screenBounds = canvasRect != null ? WorldRect(canvasRect) : new Rect();

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
                Rect r = rt != null ? WorldRect(rt) : new Rect();

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

                if (canvasRect != null && !Contains(screenBounds, r))
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

                    Rect ra = WorldRect((RectTransform)a.transform);
                    Rect rb = WorldRect((RectTransform)b.transform);
                    if (ra.width <= 0f || rb.width <= 0f) continue;

                    if (Overlaps(ra, rb))
                    {
                        findings.Add(screen.Name + ": interactive " + Q(a.name) + " " + Describe(ra) +
                                     " overlaps interactive " + Q(b.name) + " " + Describe(rb) +
                                     " - one steals the other's taps.");
                    }
                }
            }

            // --- text exceeding its container --------------------------------------------
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                Rect box = WorldRect(text.rectTransform);
                if (box.width <= Tolerance || box.height <= Tolerance) continue;

                // Only Overflow can escape the rect. Wrap/Truncate are the app deliberately
                // choosing to contain the text, so measuring those would report intent as a defect.
                Vector2 size = text.rectTransform.rect.size;
                if (text.horizontalOverflow == HorizontalWrapMode.Overflow &&
                    text.preferredWidth > size.x + Tolerance)
                {
                    findings.Add(screen.Name + ": text " + Q(text.name) + " needs " +
                                 F(text.preferredWidth) + "px but its container is " + F(size.x) +
                                 "px wide and it is set to Overflow - it spills outside its own control.");
                }

                if (text.verticalOverflow == VerticalWrapMode.Overflow &&
                    text.preferredHeight > size.y + Tolerance)
                {
                    findings.Add(screen.Name + ": text " + Q(text.name) + " needs " +
                                 F(text.preferredHeight) + "px of height but its container is " +
                                 F(size.y) + "px tall and it is set to Overflow.");
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

            // --- visible-action limit ------------------------------------------------------
            int limit = UiScreenRegistry.ActionLimitFor(screen.Surface);
            if (interactive.Count > limit)
            {
                findings.Add(screen.Name + ": " + interactive.Count + " visible interactive controls, over the " +
                             screen.Surface + " limit of " + limit + ".");
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
        private static void WriteGraphArtifacts(List<string> nodes, string dot)
        {
            string outDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsContactSheetOutput");
            Directory.CreateDirectory(outDir);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"generatedBy\": \"UiValidationRunTests\",");
            json.AppendLine("  \"edgeResolution\": \"UNRESOLVED - controls are wired via AddListener, so onClick carries no persistent calls and a destination cannot be read statically. Nodes and controls below are real; the absence of edges is a limitation of this run, NOT evidence that these screens have no navigation.\",");
            json.AppendLine("  \"nodes\": [");
            json.AppendLine(string.Join(",\n", nodes));
            json.AppendLine("  ],");
            json.AppendLine("  \"edges\": []");
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

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c[0].x, xMax = c[0].x, yMin = c[0].y, yMax = c[0].y;
            for (int i = 1; i < 4; i++)
            {
                if (c[i].x < xMin) xMin = c[i].x;
                if (c[i].x > xMax) xMax = c[i].x;
                if (c[i].y < yMin) yMin = c[i].y;
                if (c[i].y > yMax) yMax = c[i].y;
            }

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
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
