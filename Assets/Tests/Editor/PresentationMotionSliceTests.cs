using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class PresentationMotionSliceTests
    {
        [SetUp]
        public void SetUp() => MotionPolicy.ReduceMotion = false;

        [TearDown]
        public void TearDown() => MotionPolicy.ReduceMotion = false;

        [Test]
        public void PurchaseSuccessReveal_OnlyAppearsAsConfirmedPanel()
        {
            var parent = new GameObject("PurchaseSuccessParent", typeof(RectTransform));
            try
            {
                GameObject reveal = PurchaseSuccessRevealPresenter.Show(parent.transform, "Gold Vault");
                Assert.IsNotNull(reveal);
                Assert.AreEqual(PurchaseSuccessRevealPresenter.Headline,
                    reveal.transform.Find("Headline").GetComponent<Text>().text);
                Assert.AreEqual("Gold Vault", reveal.transform.Find("Item").GetComponent<Text>().text);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void PurchaseSuccessReveal_ReducedMotion_IsImmediatelyStatic()
        {
            MotionPolicy.ReduceMotion = true;
            var parent = new GameObject("ReducedMotionPurchaseParent", typeof(RectTransform));
            try
            {
                GameObject reveal = PurchaseSuccessRevealPresenter.Show(parent.transform, "Energy Potion");
                CanvasGroup group = reveal.GetComponent<CanvasGroup>();
                Assert.AreEqual(1f, group.alpha);
                Assert.AreEqual(Vector3.one, reveal.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
