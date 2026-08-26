using System.Collections.Generic;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CHECK 0 - global canvas overflow audit (spec locked at 6c2696d).
    ///
    /// NEVER VALIDATE GEOMETRY INSIDE A CONTAINER YOU HAVE NOT VALIDATED. That principle is worth
    /// more than the check itself, and it is why this file exists separately from the big
    /// validation run: a `matchWidthOrHeight` mistake sat in this project since day one producing
    /// overlapping HUDs while every geometry test passed, because they all measured positions
    /// inside a canvas whose mapping to the physical screen was never checked.
    ///
    /// The arithmetic is CanvasScaler's own, for ScaleWithScreenSize:
    ///     scaleFactor = lerp(screenW/refW, screenH/refH, matchWidthOrHeight)
    ///     rendered    = reference * scaleFactor
    /// If the rendered size exceeds the screen on either axis, content near that edge is pushed
    /// off-display no matter how correct its anchors are.
    ///
    /// Deliberately NOT a Unity-dependent test. It is pure arithmetic over the real scaler settings,
    /// so it runs fast, needs no rendered frame, and cannot be defeated by a screen failing to
    /// build - the three ways a check like this normally ends up quietly skipped.
    /// </summary>
    public class CanvasOverflowAuditTests
    {
        /// <summary>Device profiles the audit runs against. The tablet is NOT optional - tablets
        /// are confirmed in scope, and a match value that fits every phone can still crop a tablet,
        /// which is precisely the residual risk the two-canvas decision was weighed against.</summary>
        private static readonly (string Name, float Width, float Height)[] DeviceProfiles =
        {
            ("phone 2400x1080", 2400f, 1080f),
            ("tablet 2560x1600", 2560f, 1600f),
            ("baseline 1920x1080", 1920f, 1080f),
        };

        /// <summary>CanvasScaler's own formula. Copied deliberately rather than invoked: calling
        /// Unity's implementation would require a live Canvas per profile, and the point is to
        /// audit the SETTINGS against sizes the editor will never actually be at.</summary>
        private static float ScaleFactor(float screenW, float screenH, float refW, float refH, float match)
        {
            float logWidth = Mathf.Log(screenW / refW, 2f);
            float logHeight = Mathf.Log(screenH / refH, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
        }

        [Test]
        public void EveryScaledCanvas_FitsEveryTargetDevice_OnBothAxes()
        {
            var findings = new List<string>();
            var audited = new List<string>();

            foreach (CanvasScaler scaler in Resources.FindObjectsOfTypeAll<CanvasScaler>())
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;

                float refW = scaler.referenceResolution.x;
                float refH = scaler.referenceResolution.y;
                if (refW <= 0f || refH <= 0f) continue;

                audited.Add(scaler.name);

                foreach ((string device, float screenW, float screenH) in DeviceProfiles)
                {
                    float factor = ScaleFactor(screenW, screenH, refW, refH, scaler.matchWidthOrHeight);
                    float renderedW = refW * factor;
                    float renderedH = refH * factor;

                    // Sub-pixel slack. A rendered size a fraction over the screen is rounding, not
                    // a cropped HUD, and failing on it would train readers to ignore this check.
                    const float Slack = 1f;

                    if (renderedW > screenW + Slack)
                    {
                        findings.Add(scaler.name + " on " + device + ": renders " +
                                     Mathf.RoundToInt(renderedW) + "px wide into a " +
                                     Mathf.RoundToInt(screenW) + "px screen (overflowX " +
                                     Mathf.RoundToInt(renderedW - screenW) + "px, match=" +
                                     scaler.matchWidthOrHeight + ", ref " + refW + "x" + refH + ").");
                    }

                    if (renderedH > screenH + Slack)
                    {
                        findings.Add(scaler.name + " on " + device + ": renders " +
                                     Mathf.RoundToInt(renderedH) + "px tall into a " +
                                     Mathf.RoundToInt(screenH) + "px screen (overflowY " +
                                     Mathf.RoundToInt(renderedH - screenH) + "px, match=" +
                                     scaler.matchWidthOrHeight + ", ref " + refW + "x" + refH + ").");
                    }
                }
            }

            // An empty sweep is NOT a pass. If no scaler was found, this test proves nothing and
            // must say so - a silently vacuous audit is worse than none, because it reports green.
            Assert.IsNotEmpty(audited,
                "No ScaleWithScreenSize canvas was found to audit. That is not a pass - it means " +
                "the sweep found nothing to measure and the check is vacuous. Build a screen first, " +
                "or point this at the real scaler configuration.");

            Assert.IsEmpty(findings,
                "Canvas overflow (" + findings.Count + " across " + audited.Count + " scalers):\n  - " +
                string.Join("\n  - ", findings));
        }

        [Test]
        public void TheAuditArithmetic_MatchesCanvasScaler_OnAKnownOverflowCase()
        {
            // Guards the check itself. matchWidthOrHeight=0 means "match width", so a 1920x1080
            // reference on a 2560x1600 tablet scales by 2560/1920 = 1.333 and renders 1440px tall
            // into 1600px - fits. The same canvas with match=1 scales by 1600/1080 = 1.481 and
            // renders 2844px wide into 2560px - a 284px overflow, which is the crop this audit
            // exists to catch.
            //
            // Without this, a sign error or a swapped axis would make the audit silently green and
            // nobody would notice, because green is what everyone expects to see.
            float matchWidth = ScaleFactor(2560f, 1600f, 1920f, 1080f, 0f);
            Assert.AreEqual(1920f * matchWidth, 2560f, 1f, "match=0 must fit the width exactly.");

            float matchHeight = ScaleFactor(2560f, 1600f, 1920f, 1080f, 1f);
            Assert.AreEqual(1080f * matchHeight, 1600f, 1f, "match=1 must fit the height exactly.");
            Assert.Greater(1920f * matchHeight, 2560f + 1f,
                "match=1 on this tablet must overflow the width - if it does not, the arithmetic is wrong.");
        }
    }
}
