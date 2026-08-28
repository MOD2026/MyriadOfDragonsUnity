using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// A/B evidence for VS-UI-CAMPAIGN-BACKBTN-SCRIM-001: should the two removed `backBtnObj`
    /// gradient scrims stay removed, or be restored?
    ///
    /// METHOD - why no production file is edited to produce the variants. The A/B states are
    /// synthesised HERE by calling the SAME helper with the EXACT arguments the working-tree WIP
    /// deleted (offset (80,20), size 156x38, 0.95 opacity, one per direction), against the real
    /// button the real presenter built. That measures precisely what restoring the lines would
    /// produce, while `CampaignMapPresenter.cs` is never opened for writing - so the other three
    /// coupled hunks of that file's WIP cannot be disturbed and there is nothing to revert
    /// afterwards. A patch-and-unpatch cycle on a shared dirty file was the alternative, and it is
    /// exactly the workflow that caused INCIDENT-RESET-001.
    ///
    /// The third variant (offset Vector2.zero, same size) is not a restore of what was deleted -
    /// it is the "is there a CORRECT restore?" control, since AddLocalGradientScrim point-anchors
    /// at (0.5,0.5) so its offset is centre-relative, not corner-relative.
    /// </summary>
    public class CampaignBackButtonScrimAbTests
    {
        // The exact literals the WIP deleted at CampaignMapPresenter.cs:2313-2321.
        private static readonly Vector2 DeletedOffset = new Vector2(80f, 20f);
        private static readonly Vector2 DeletedSize = new Vector2(156f, 38f);
        private const float DeletedOpacity = 0.95f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDCampaignBackScrimAb_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            var profile = new PlayerProfile
            {
                gold = 50_000, gems = 5_000, stamina = 100, maxStamina = 100, avatarLevel = 10,
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-3" },
            };
            SaveSystem.Save(profile);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private GameObject OpenCampaign()
        {
            var host = new GameObject("CampaignBackScrimAbHost");
            _spawned.Add(host);
            var map = host.AddComponent<CampaignMapPresenter>();
            map.Initialize(onBackToHome: null, onLaunchBattle: null);
            GameObject canvas = GameObject.Find("CampaignMapCanvas");
            Assert.IsNotNull(canvas, "Campaign map built no canvas.");
            _spawned.Add(canvas);
            foreach (RectTransform rt in canvas.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
            return canvas;
        }

        private static RectTransform BackButton(GameObject canvas)
        {
            Button back = canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Btn_Back");
            Assert.IsNotNull(back, "Campaign header has no Btn_Back.");
            return back.GetComponent<RectTransform>();
        }

        private static RectTransform[] ScrimsUnder(Transform root)
        {
            return root.GetComponentsInChildren<RectTransform>(true)
                .Where(r => r.name == "GradientScrim").ToArray();
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static bool Contains(Rect outer, Rect inner, float tolerance = 0.01f)
        {
            return inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance &&
                   inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;
        }

        /// <summary>State 1 - the working tree as it stands: both scrims removed. Establishes that
        /// the button carries no scrim at all right now, which is the baseline the two variants are
        /// measured against.</summary>
        [Test]
        public void Baseline_BothScrimsRemoved_BackButtonCarriesNoScrim()
        {
            GameObject canvas = OpenCampaign();
            RectTransform back = BackButton(canvas);
            RectTransform[] scrims = ScrimsUnder(back);

            Debug.Log($"[BackScrimAB] baseline: Btn_Back rect={WorldRect(back)} scrimCount={scrims.Length}");
            Assert.AreEqual(0, scrims.Length,
                "Baseline expects the WIP state (both back-button scrims removed); found some, so the file changed.");
        }

        /// <summary>State 2 and 3 - each deleted scrim restored INDIVIDUALLY, exactly as written.
        /// Both are measured in the same test because they are the same call with only the
        /// gradient direction differing, and the containment result is a property of the
        /// offset/size pair they share.</summary>
        [Test]
        public void RestoringEitherDeletedScrim_AsWritten_EscapesTheBackButton()
        {
            GameObject canvas = OpenCampaign();
            RectTransform back = BackButton(canvas);
            Rect btn = WorldRect(back);

            var report = new StringBuilder();
            var results = new List<(string label, bool contained, Rect rect)>();

            foreach (UISharedFoundation.GradientDirection dir in new[]
                     {
                         UISharedFoundation.GradientDirection.TopToBottom,
                         UISharedFoundation.GradientDirection.BottomToTop,
                     })
            {
                GameObject probeHost = UnityEngine.Object.Instantiate(back.gameObject, back.parent);
                _spawned.Add(probeHost);
                probeHost.name = "BackScrimProbe_" + dir;
                foreach (RectTransform stale in ScrimsUnder(probeHost.transform))
                    UnityEngine.Object.DestroyImmediate(stale.gameObject);

                Image scrim = UISharedFoundation.AddLocalGradientScrim(
                    probeHost.transform, DeletedOffset, DeletedSize, dir, DeletedOpacity);
                Canvas.ForceUpdateCanvases();

                Rect s = WorldRect(scrim.rectTransform);
                bool contained = Contains(btn, s);
                results.Add((dir.ToString(), contained, s));
                report.AppendLine($"  restore-{dir}: scrim={s} contained={contained}");
            }

            // Control: the same size, centred - what a CORRECT restore would look like.
            GameObject centredHost = UnityEngine.Object.Instantiate(back.gameObject, back.parent);
            _spawned.Add(centredHost);
            centredHost.name = "BackScrimProbe_Centred";
            foreach (RectTransform stale in ScrimsUnder(centredHost.transform))
                UnityEngine.Object.DestroyImmediate(stale.gameObject);
            Image centred = UISharedFoundation.AddLocalGradientScrim(
                centredHost.transform, Vector2.zero, DeletedSize,
                UISharedFoundation.GradientDirection.TopToBottom, DeletedOpacity);
            Canvas.ForceUpdateCanvases();
            Rect centredRect = WorldRect(centred.rectTransform);
            bool centredContained = Contains(btn, centredRect);
            report.AppendLine($"  control-centred: scrim={centredRect} contained={centredContained}");

            Debug.Log($"[BackScrimAB] Btn_Back rect={btn}\n{report}");

            Assert.IsFalse(results[0].contained,
                "TopToBottom scrim restored as written should NOT fit - offset (80,20) is centre-relative.");
            Assert.IsFalse(results[1].contained,
                "BottomToTop scrim restored as written should NOT fit - same offset defect.");
            Assert.IsTrue(centredContained,
                "A centred scrim of the same size SHOULD fit inside the button - proves size is fine and only the offset is wrong.");
        }
    }
}
