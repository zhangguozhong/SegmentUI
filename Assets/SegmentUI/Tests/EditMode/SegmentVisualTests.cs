using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Tests
{
    public class RecordingSegmentIndicator : SegmentIndicator
    {
        public SegmentIndicatorFrame Frame;
        public override void ApplyFrame(SegmentIndicatorFrame frame) { gameObject.SetActive(true); Frame = frame; }
    }
    public class SegmentLayoutCounter : MonoBehaviour, ILayoutController
    {
        public int Calls;
        public void SetLayoutHorizontal() { Calls++; }
        public void SetLayoutVertical() { Calls++; }
    }
    public class SegmentVisualTests
    {
        GameObject root;
        SegmentBar bar;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Bar", typeof(RectTransform), typeof(SegmentBar));
            ((RectTransform)root.transform).sizeDelta = new Vector2(600, 100);
            bar = root.GetComponent<SegmentBar>();
            var items = Child("Items");
            items.sizeDelta = new Vector2(600, 100);
            var template = Child("Template").gameObject.AddComponent<SegmentItemView>();
            template.Button = template.gameObject.AddComponent<Button>();
            template.gameObject.SetActive(false);
            bar.Configure(items, template, Child("Background"), Child("Foreground"));
        }
        RectTransform Child(string name) { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(root.transform, false); return r; }
        SegmentPageDescriptor Page(string id, bool enabled = true) => new SegmentPageDescriptor { Id = id, Item = new SegmentItemData { Title = id, IsEnabled = enabled } };
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }
        [Test] public void RebindingDoesNotMultiplyClickListenersAndDisabledItemsDoNotClick()
        {
            var pages = new[] { Page("a"), Page("b", false), Page("c") };
            bar.Bind(pages); bar.Bind(pages);
            int clicks = 0; bar.ItemClicked += _ => clicks++;
            bar.GetItem("a").Button.onClick.Invoke();
            bar.GetItem("b").Button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
        }
        [Test] public void PreviewUsesEnabledIdMappingAndDoesNotCommitSelection()
        {
            bar.Bind(new[] { Page("a"), Page("disabled", false), Page("c") });
            bar.Render(.75f, new[] { "a", "c" }, "a", SegmentSelectionSource.Gesture);
            Assert.That(bar.GetItem("a").SelectedWeight, Is.EqualTo(.25f));
            Assert.That(bar.GetItem("c").SelectedWeight, Is.EqualTo(.75f));
            Assert.That(bar.GetItem("disabled").SelectedWeight, Is.Zero);
            Assert.That(bar.GetItem("a").IsCommitted, Is.True);
            Assert.That(bar.GetItem("c").IsCommitted, Is.False);
        }
        [Test] public void NarrowEqualLayoutFallsBackToScrollableMinimumHitWidths()
        {
            bar.Bind(new[] { Page("a"), Page("b"), Page("c"), Page("d"), Page("e"), Page("f"), Page("g") });
            bar.Render(0, new[] { "a" }, "a", SegmentSelectionSource.Code);
            Assert.That(bar.GetItem("a").GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(96));
            Assert.That(bar.ItemsRoot.rect.width, Is.GreaterThanOrEqualTo(672));
        }
        [Test] public void ExistingItemsUseIdMappingAndAreNotDestroyedOnRemoval()
        {
            var first = new GameObject("First", typeof(RectTransform), typeof(Button), typeof(SegmentItemView)).GetComponent<SegmentItemView>();
            first.transform.SetParent(bar.ItemsRoot, false); first.Button = first.GetComponent<Button>();
            var second = new GameObject("Second", typeof(RectTransform), typeof(Button), typeof(SegmentItemView)).GetComponent<SegmentItemView>();
            second.transform.SetParent(bar.ItemsRoot, false); second.Button = second.GetComponent<Button>();
            bar.UseExistingItems = true;
            bar.ExistingItems = new[] { new SegmentExistingItem { PageId = "b", View = first }, new SegmentExistingItem { PageId = "a", View = second } };
            bar.Bind(new[] { Page("a"), Page("b") });
            Assert.That(bar.GetItem("a"), Is.SameAs(second));
            bar.Bind(new[] { Page("b") });
            Assert.That(second != null, Is.True);
            Assert.That(second.gameObject.activeSelf, Is.False);
            Assert.That(second.PageId, Is.Null);
        }
        [Test] public void InvalidMappingDoesNotDestroyExistingBindings()
        {
            bar.Bind(new[] { Page("a") });
            var original = bar.GetItem("a");
            Assert.Throws<System.ArgumentException>(() => bar.Bind(new[] { Page("a"), Page("a") }));
            Assert.That(bar.GetItem("a"), Is.SameAs(original));
        }
        [Test] public void IndicatorCoordinatesConvertAcrossDifferentLayerOrigins()
        {
            var source = Child("Source"); source.anchoredPosition = new Vector2(100, 20); source.sizeDelta = new Vector2(80, 100);
            var layer = Child("Layer"); layer.anchoredPosition = new Vector2(-30, 10);
            var rect = SegmentIndicator.RectInLayer(source, layer);
            Assert.That(rect.center.x, Is.EqualTo(130).Within(.01));
            Assert.That(rect.center.y, Is.EqualTo(10).Within(.01));
            Assert.That(rect.width, Is.EqualTo(80).Within(.01));
        }
        [Test] public void InsufficientTitleHeightDisablesInteractionInsteadOfOverflowingHitArea()
        {
            ((RectTransform)root.transform).sizeDelta = new Vector2(600, 60);
            bar.Bind(new[] { Page("a") });
            bar.Render(0, new[] { "a" }, "a", SegmentSelectionSource.Code);
            Assert.That(bar.GetItem("a").Button.interactable, Is.False);
        }
        [Test] public void UnusedExistingMappingsAreHiddenOnFirstBind()
        {
            var unused = new GameObject("Unused", typeof(RectTransform), typeof(Button), typeof(SegmentItemView)).GetComponent<SegmentItemView>();
            unused.transform.SetParent(bar.ItemsRoot, false); unused.Button = unused.GetComponent<Button>();
            bar.UseExistingItems = true;
            bar.ExistingItems = new[] { new SegmentExistingItem { PageId = "unused", View = unused } };
            bar.Bind(new SegmentPageDescriptor[0]);
            Assert.That(unused.gameObject.activeSelf, Is.False);
        }
        [Test] public void UndersizedCustomButtonDisablesInteraction()
        {
            var button = new GameObject("SmallButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(bar.DefaultTemplate.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(50, 50);
            bar.DefaultTemplate.Button = button;
            bar.Bind(new[] { Page("a") });
            bar.Render(0, new[] { "a" }, "a", SegmentSelectionSource.Code);
            Assert.That(bar.GetItem("a").Button.interactable, Is.False);
        }
        [Test] public void IndicatorReceivesAuthoritativeTransitionTargetAndVersion()
        {
            var template = Child("RecordingIndicator").gameObject.AddComponent<RecordingSegmentIndicator>();
            bar.Bind(new[] { Page("a"), Page("b"), Page("c") });
            bar.SetIndicators(new SegmentIndicator[] { template });
            bar.TargetPageId = "c"; bar.TransitionVersion = 42;
            bar.Render(.1f, new[] { "a", "b", "c" }, "a", SegmentSelectionSource.Click);
            var instance = bar.ForegroundLayer.GetComponentInChildren<RecordingSegmentIndicator>(true);
            Assert.That(instance.Frame.TargetPageId, Is.EqualTo("c"));
            Assert.That(instance.Frame.TransitionVersion, Is.EqualTo(42));
        }
        [Test] public void AutoCenterFollowsNewestTargetAndCancelsBackToCommittedItem()
        {
            bar.Bind(new[] { Page("a"), Page("b"), Page("c"), Page("d"), Page("e"), Page("f"), Page("g") });
            var ids = new[] { "a", "b", "c", "d", "e", "f", "g" };
            bar.TargetPageId = "g"; bar.Render(0, ids, "a", SegmentSelectionSource.Click);
            Assert.That(bar.ItemsRoot.anchoredPosition.x, Is.LessThan(0));
            bar.TargetPageId = "b"; bar.Render(.2f, ids, "a", SegmentSelectionSource.Click);
            Assert.That(bar.ItemsRoot.anchoredPosition.x, Is.Zero.Within(.01));
            bar.TargetPageId = null; bar.Render(0, ids, "a", SegmentSelectionSource.Code);
            Assert.That(bar.ItemsRoot.anchoredPosition.x, Is.Zero.Within(.01));
        }
        [Test] public void AutomaticTitleScrollerParticipatesInSegmentArbitration()
        {
            Assert.That(bar.TitleScroll, Is.InstanceOf<SegmentScrollRect>());
            Assert.That(((SegmentScrollRect)bar.TitleScroll).IsTitleBar, Is.True);
        }
        [Test] public void OrdinaryTitleScrollerIsRejectedWithinSegmentView()
        {
            root.AddComponent<SegmentView>();
            var ordinary = Child("OrdinaryScroll").gameObject.AddComponent<ScrollRect>();
            bar.TitleScroll = ordinary;
            Assert.Throws<System.ArgumentException>(() => bar.ValidateConfiguration(new[] { Page("a") }));
        }
        [Test] public void DefaultIndicatorsInstallOnceAndExplicitEmptyStaysEmpty()
        {
            var template = Child("DefaultIndicator").gameObject.AddComponent<RecordingSegmentIndicator>();
            bar.DefaultIndicators = new SegmentIndicator[] { template };
            bar.Bind(new[] { Page("a") });
            Assert.That(bar.ForegroundLayer.GetComponentsInChildren<RecordingSegmentIndicator>(true).Length, Is.EqualTo(1));
            bar.Bind(new[] { Page("a") });
            Assert.That(bar.ForegroundLayer.GetComponentsInChildren<RecordingSegmentIndicator>(true).Length, Is.EqualTo(1));
            bar.SetIndicators(new SegmentIndicator[0]);
            bar.Bind(new[] { Page("a") });
            Assert.That(bar.ForegroundLayer.GetComponentsInChildren<RecordingSegmentIndicator>(true), Is.Empty);
        }
        [Test] public void ExplicitEmptyBeforeFirstBindOverridesDefaults()
        {
            var template = Child("DefaultIndicator").gameObject.AddComponent<RecordingSegmentIndicator>();
            bar.DefaultIndicators = new SegmentIndicator[] { template };
            bar.SetIndicators(new SegmentIndicator[0]);
            bar.Bind(new[] { Page("a") });
            Assert.That(bar.ForegroundLayer.GetComponentsInChildren<RecordingSegmentIndicator>(true), Is.Empty);
        }
        [Test] public void InvalidIndicatorReplacementPreservesPreviousInstances()
        {
            var template = Child("OldIndicator").gameObject.AddComponent<RecordingSegmentIndicator>();
            bar.SetIndicators(new SegmentIndicator[] { template });
            var old = bar.ForegroundLayer.GetComponentInChildren<RecordingSegmentIndicator>(true);
            var invalid = Child("InvalidIndicator").gameObject.AddComponent<RecordingSegmentIndicator>();
            invalid.Layer = SegmentIndicatorLayer.Background;
            bar.BackgroundLayer = null;
            Assert.Throws<System.ArgumentException>(() => bar.SetIndicators(new SegmentIndicator[] { template, invalid }));
            Assert.That(old != null, Is.True);
            Assert.That(bar.ForegroundLayer.GetComponentsInChildren<RecordingSegmentIndicator>(true).Length, Is.EqualTo(1));
        }
        [Test] public void UnchangedVisualFramesDoNotForceAnotherLayoutRebuild()
        {
            var counter = bar.ItemsRoot.gameObject.AddComponent<SegmentLayoutCounter>();
            bar.Bind(new[] { Page("a") });
            bar.Render(0, new[] { "a" }, "a", SegmentSelectionSource.Code);
            int calls = counter.Calls;
            bar.Render(0, new[] { "a" }, "a", SegmentSelectionSource.Code);
            Assert.That(counter.Calls, Is.EqualTo(calls));
        }
        void SetRightToLeft(bool value)
        {
            var property = typeof(SegmentBar).GetProperty("RightToLeft");
            Assert.That(property, Is.Not.Null, "SegmentBar must expose direction without reordering logical IDs.");
            property.SetValue(bar, value);
        }
        [TestCase(SegmentBarLayout.Equal)]
        [TestCase(SegmentBarLayout.ContentWidth)]
        public void RightToLeftReversesPhysicalTitlesAndKeepsLogicalPreview(SegmentBarLayout mode)
        {
            bar.LayoutMode = mode;
            SetRightToLeft(true);
            bar.Bind(new[] { Page("a"), Page("disabled", false), Page("c") });
            var indicator = Child("RTLIndicator").gameObject.AddComponent<RecordingSegmentIndicator>();
            bar.SetIndicators(new SegmentIndicator[] { indicator });
            bar.Render(.25f, new[] { "a", "c" }, "a", SegmentSelectionSource.Gesture);
            var frame = bar.ForegroundLayer.GetComponentInChildren<RecordingSegmentIndicator>(true).Frame;
            Assert.That(frame.LeftPageId, Is.EqualTo("a"));
            Assert.That(frame.RightPageId, Is.EqualTo("c"));
            Assert.That(frame.LeftItem.center.x, Is.GreaterThan(frame.RightItem.center.x));
            Assert.That(bar.GetItem("a").SelectedWeight, Is.EqualTo(.75f));
            SetRightToLeft(false);
            frame = bar.ForegroundLayer.GetComponentInChildren<RecordingSegmentIndicator>(true).Frame;
            Assert.That(frame.LeftItem.center.x, Is.LessThan(frame.RightItem.center.x));
        }
        [TestCase(SegmentBarLayout.Equal)]
        [TestCase(SegmentBarLayout.ContentWidth)]
        public void RightToLeftWithoutAutoCenterRevealsInitialAndSelectedTitles(SegmentBarLayout mode)
        {
            bar.AutoCenter = false; bar.LayoutMode = mode;
            SetRightToLeft(true);
            var ids = new[] { "a", "b", "c", "d", "e", "f", "g" };
            bar.Bind(new[] { Page("a"), Page("b"), Page("c"), Page("d"), Page("e"), Page("f"), Page("g") });
            bar.Render(0, ids, "a", SegmentSelectionSource.Code);
            var rect = SegmentIndicator.RectInLayer((RectTransform)bar.GetItem("a").transform, (RectTransform)root.transform);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(300.01f));
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-300.01f));
            bar.Render(6, ids, "g", SegmentSelectionSource.Code);
            rect = SegmentIndicator.RectInLayer((RectTransform)bar.GetItem("g").transform, (RectTransform)root.transform);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-300.01f));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(300.01f));
        }
        [Test] public void CoreDirectionLeavesBusinessItemLayoutToExternalAdapter()
        {
            var icon = Child("Icon").gameObject.AddComponent<Image>(); icon.transform.SetParent(bar.DefaultTemplate.transform, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
            icon.rectTransform.pivot = new Vector2(0, .5f); icon.rectTransform.anchoredPosition = new Vector2(24, 0);
            bar.DefaultTemplate.Icon = icon;
            var badge = Child("Badge").gameObject.AddComponent<Text>(); badge.transform.SetParent(bar.DefaultTemplate.transform, false);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, .5f);
            badge.rectTransform.pivot = new Vector2(1, .5f); badge.rectTransform.anchoredPosition = new Vector2(-16, 0);
            bar.DefaultTemplate.Badge = badge;
            SetRightToLeft(true); bar.Bind(new[] { Page("a") });
            var item = bar.GetItem("a");
            Assert.That(item.Icon.rectTransform.anchorMin.x, Is.Zero);
            Assert.That(item.Icon.rectTransform.anchoredPosition.x, Is.EqualTo(24));
            Assert.That(item.Badge.rectTransform.anchorMin.x, Is.EqualTo(1));
            Assert.That(item.Badge.rectTransform.anchoredPosition.x, Is.EqualTo(-16));
            Assert.That(item.Icon.transform.localScale.x, Is.EqualTo(1));
            Assert.That(item.Badge.transform.localScale.x, Is.EqualTo(1));
            Assert.That(typeof(SegmentItemView).GetMethod("ApplyDirection").IsVirtual, Is.True);
            SetRightToLeft(false);
            Assert.That(item.Icon.rectTransform.anchoredPosition.x, Is.EqualTo(24));
            Assert.That(item.Badge.rectTransform.anchoredPosition.x, Is.EqualTo(-16));
        }
        [Test] public void StretchIndicatorGrowsTowardPhysicalLeftForRightToLeftPreview()
        {
            var indicator = Child("Indicator").gameObject.AddComponent<SegmentBuiltInIndicator>();
            indicator.Motion = SegmentIndicatorMotion.Stretch; indicator.WidthMode = SegmentIndicatorWidth.MatchItem; indicator.Inset = 0;
            indicator.ApplyFrame(new SegmentIndicatorFrame { LeftItem = new Rect(200, 0, 100, 100), RightItem = new Rect(0, 0, 100, 100), Progress = .25f });
            var rect = SegmentIndicator.RectInLayer((RectTransform)indicator.transform, (RectTransform)root.transform);
            Assert.That(rect.xMin, Is.EqualTo(100).Within(.01));
            Assert.That(rect.xMax, Is.EqualTo(300).Within(.01));
        }
        [Test] public void DiscreteIndicatorStaysAtCommittedItemDuringPreview()
        {
            var indicator = Child("Indicator").gameObject.AddComponent<SegmentBuiltInIndicator>();
            indicator.Motion = SegmentIndicatorMotion.Discrete;
            indicator.WidthMode = SegmentIndicatorWidth.MatchItem;
            indicator.Inset = 0;
            indicator.ApplyFrame(new SegmentIndicatorFrame { LeftItem = new Rect(0, 0, 100, 100), RightItem = new Rect(200, 0, 100, 100), CommittedItem = new Rect(0, 0, 100, 100), Progress = .9f, HasCommitted = true });
            Assert.That(((RectTransform)indicator.transform).anchoredPosition.x, Is.EqualTo(50).Within(.01));
        }
        [Test] public void StretchIndicatorExpandsAcrossGapAtMidpoint()
        {
            var indicator = Child("Indicator").gameObject.AddComponent<SegmentBuiltInIndicator>();
            indicator.Motion = SegmentIndicatorMotion.Stretch;
            indicator.WidthMode = SegmentIndicatorWidth.MatchItem;
            indicator.Inset = 0;
            indicator.ApplyFrame(new SegmentIndicatorFrame { LeftItem = new Rect(0, 0, 100, 100), RightItem = new Rect(200, 0, 100, 100), Progress = .5f });
            Assert.That(((RectTransform)indicator.transform).rect.width, Is.EqualTo(300).Within(.01));
            foreach (var graphic in indicator.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
        }
    }
}
