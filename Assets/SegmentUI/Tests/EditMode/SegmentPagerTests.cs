using NUnit.Framework;
using UnityEngine;

namespace SegmentUI.Tests
{
    public class SegmentPagerTests
    {
        GameObject root;
        SegmentPager pager;
        RectTransform viewport, content;

        [SetUp] public void Setup()
        {
            root = new GameObject("Pager", typeof(RectTransform), typeof(SegmentPager));
            viewport = (RectTransform)root.transform;
            viewport.sizeDelta = new Vector2(400, 600);
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            pager = root.GetComponent<SegmentPager>();
            pager.Configure(viewport, content);
            pager.SetPageCount(4);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [Test] public void PositionUsesViewportWidthAndClampsToValidPages()
        {
            pager.SetPosition(1.5f);
            Assert.That(content.anchoredPosition.x, Is.EqualTo(-600).Within(.01));
            pager.SetPosition(100);
            Assert.That(pager.Position, Is.EqualTo(3));
        }
        [Test] public void NewTargetInterruptsFromCurrentVisualPosition()
        {
            pager.MoveTo(3, true);
            pager.Tick(.08f);
            float current = pager.Position;
            Assert.That(current, Is.GreaterThan(0).And.LessThan(3));
            pager.MoveTo(0, true);
            Assert.That(pager.Position, Is.EqualTo(current));
            pager.Tick(1);
            Assert.That(pager.Position, Is.Zero);
            Assert.That(pager.IsSettling, Is.False);
        }
        [Test] public void StopInvalidatesPendingCompletion()
        {
            pager.MoveTo(3, true);
            pager.Tick(.05f);
            pager.Stop();
            float held = pager.Position;
            pager.Tick(1);
            Assert.That(pager.Position, Is.EqualTo(held));
            Assert.That(pager.IsSettling, Is.False);
        }
    }
}
