#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SegmentUI.Tests
{
    public class SegmentVisualLifecycleTests
    {
        GameObject demo, events;
        SegmentView view;
        ReplayMouse mouse;
        ReplayModule module;
        [UnitySetUp] public IEnumerator Setup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SegmentUI/Samples/Generated/SegmentDemo.prefab");
            Assert.That(prefab, Is.Not.Null, "Generate SegmentUI demo assets before running PlayMode acceptance.");
            demo = Object.Instantiate(prefab);
            // Batch mode defaults to a short landscape Game View. Reserve the demo's
            // portrait design height; actual portrait width/height layouts are captured separately.
            demo.GetComponent<CanvasScaler>().matchWidthOrHeight = 1;
            var eventSystem = demo.GetComponentInChildren<EventSystem>();
            events = eventSystem != null ? eventSystem.gameObject : new GameObject("VisualEvents", typeof(EventSystem));
            foreach (var existing in events.GetComponents<BaseInputModule>()) existing.enabled = false;
            mouse = events.AddComponent<ReplayMouse>(); module = events.AddComponent<ReplayModule>();
            module.inputOverride = mouse; module.enabled = false;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); view = demo.GetComponentInChildren<SegmentView>();
            Assert.That(view.CurrentPageId, Is.EqualTo("dispatch"));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (events != null && !events.transform.IsChildOf(demo.transform)) Object.Destroy(events);
            Object.Destroy(demo); yield return null;
        }
        [UnityTest] public IEnumerator LiveDemoUpdatesDuringDragAndAnimationAndStopsWhenToggledOff()
        {
            var live = demo.transform.Find("SafeAreaContent/Footer/Controls/Live");
            Assert.That(live, Is.Not.Null, "Demo needs a LIVE control for timer-driven UpdateItem.");
            var button = live.GetComponent<Button>(); var label = button.GetComponentInChildren<Text>();
            Assert.That(label.text, Is.EqualTo("LIVE: OFF"));
            var item = view.Bar.GetItem("activity"); var originalIcon = item.Icon.sprite;
            button.onClick.Invoke(); Assert.That(label.text, Is.EqualTo("LIVE: ON"));
            var scroll = view.GetPage("dispatch").GetComponentInChildren<SegmentScrollRect>();
            var p = new PointerEventData(EventSystem.current) { pointerId = 31, button = PointerEventData.InputButton.Left,
                position = new Vector2(200, 200), pressPosition = new Vector2(200, 200) };
            scroll.OnInitializePotentialDrag(p); p.delta = new Vector2(-100, 0); p.position += p.delta;
            scroll.OnBeginDrag(p); scroll.OnDrag(p);
            string before = item.Label.text; int version = view.Pager.Version; float position = view.Pager.Position;
            yield return new WaitForSecondsRealtime(.16f);
            Assert.That(item.Label.text, Is.Not.EqualTo(before));
            Assert.That(view.Gesture.ActivePointerId, Is.EqualTo(31));
            Assert.That(view.Pager.Version, Is.EqualTo(version)); Assert.That(view.Pager.Position, Is.EqualTo(position));
            Assert.That(view.Phase, Is.EqualTo(SegmentPhase.DraggingHorizontal));
            scroll.OnEndDrag(p); view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            view.Select("activity", true); before = item.Label.text;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(item.Label.text, Is.Not.EqualTo(before)); Assert.That(view.TargetPageId, Is.EqualTo("activity"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.CurrentPageId, Is.EqualTo("activity")); Assert.That(view.Bar.GetItem("activity"), Is.SameAs(item));
            Assert.That(item.Icon.sprite, Is.SameAs(originalIcon));
            button.onClick.Invoke(); Assert.That(label.text, Is.EqualTo("LIVE: OFF"));
            before = item.Label.text; string badge = item.Badge.text;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(item.Label.text, Is.EqualTo(before)); Assert.That(item.Badge.text, Is.EqualTo(badge));
        }
        bool UpdateItem(string id, SegmentItemData data) => view.UpdateItem(id, data);
        [UnityTest] public IEnumerator UpdateItemDuringDragKeepsOwnerAndContinuesReceivingMovement()
        {
            foreach (bool rtl in new[] { false, true })
            foreach (bool vertical in new[] { false, true })
            {
                view.SetDirection(rtl ? SegmentLayoutDirection.RightToLeft : SegmentLayoutDirection.LeftToRight);
                view.Select("dispatch", false);
                var page = view.GetPage("dispatch");
                var scroll = page.GetComponentInChildren<SegmentScrollRect>();
                var p = new PointerEventData(EventSystem.current) { pointerId = 11, button = PointerEventData.InputButton.Left,
                    position = new Vector2(200, 200), pressPosition = new Vector2(200, 200) };
                scroll.OnInitializePotentialDrag(p);
                p.delta = vertical ? new Vector2(0, 100) : new Vector2(rtl ? 100 : -100, 0);
                p.position += p.delta; scroll.OnBeginDrag(p); scroll.OnDrag(p);
                var phase = view.Phase; var owner = view.Gesture.ActivePointerId;
                Assert.That(phase, Is.EqualTo(vertical ? SegmentPhase.DraggingVertical : SegmentPhase.DraggingHorizontal));
                float position = view.Pager.Position; var scrollPosition = scroll.content.anchoredPosition;
                int version = view.Pager.Version; var item = view.Bar.GetItem("dispatch");
                Assert.That(UpdateItem("dispatch", new SegmentItemData { Title = "live title", BadgeCount = 8 }), Is.True);
                Assert.That(view.Phase, Is.EqualTo(phase)); Assert.That(view.Gesture.ActivePointerId, Is.EqualTo(owner));
                Assert.That(view.Pager.Version, Is.EqualTo(version)); Assert.That(view.Pager.Position, Is.EqualTo(position));
                Assert.That(scroll.content.anchoredPosition, Is.EqualTo(scrollPosition));
                Assert.That(view.GetPage("dispatch"), Is.SameAs(page)); Assert.That(view.Bar.GetItem("dispatch"), Is.SameAs(item));
                Assert.That(item.Label.text, Is.EqualTo("live title")); Assert.That(item.RightToLeft, Is.EqualTo(rtl));
                p.position += p.delta; scroll.OnDrag(p);
                if (vertical) Assert.That(scroll.content.anchoredPosition, Is.Not.EqualTo(scrollPosition));
                else Assert.That(view.Pager.Position, Is.GreaterThan(position));
                scroll.OnEndDrag(p); view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator UpdateItemDuringSettlingPreservesTargetAndCompletesNormally()
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);
            view.Select("activity", true); view.Pager.Tick(.05f);
            var source = view.GetPage("dispatch"); var target = view.GetPage("activity");
            var item = view.Bar.GetItem("activity"); float weight = item.SelectedWeight;
            int version = view.Pager.Version; float position = view.Pager.Position;
            int changes = 0; view.SelectionChanged += _ => changes++;
            for (int i = 0; i < 10; i++) Assert.That(UpdateItem("activity", new SegmentItemData { Title = "updated " + i, BadgeCount = i }), Is.True);
            Assert.That(view.IsTransitioning, Is.True); Assert.That(view.TargetPageId, Is.EqualTo("activity"));
            Assert.That(view.CurrentPageId, Is.EqualTo("dispatch")); Assert.That(view.Pager.Position, Is.EqualTo(position));
            Assert.That(view.Pager.Version, Is.EqualTo(version)); Assert.That(item.SelectedWeight, Is.EqualTo(weight));
            Assert.That(view.GetPage("dispatch"), Is.SameAs(source)); Assert.That(view.GetPage("activity"), Is.SameAs(target));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(view.CurrentPageId, Is.EqualTo("activity")); Assert.That(changes, Is.EqualTo(1));
            Assert.That(item.IsCommitted, Is.True); Assert.That(item.Badge.text, Is.EqualTo("9"));
        }
        [UnityTest] public IEnumerator UpdateItemDoesNotEndTitleBarDragOrStopItsVelocity()
        {
            var bar = view.Bar; bar.LayoutMode = SegmentBarLayout.ContentWidth;
            Assert.That(UpdateItem("dispatch", new SegmentItemData { Title = new string('W', 100) }), Is.True);
            var scroll = (SegmentScrollRect)bar.TitleScroll;
            var p = new PointerEventData(EventSystem.current) { pointerId = 15, button = PointerEventData.InputButton.Left,
                position = new Vector2(200, 200), pressPosition = new Vector2(200, 200) };
            scroll.OnInitializePotentialDrag(p); p.delta = new Vector2(-80, 0); p.position += p.delta;
            scroll.OnBeginDrag(p); scroll.OnDrag(p); var phase = view.Phase;
            var before = bar.ItemsRoot.anchoredPosition; scroll.velocity = new Vector2(75, 0);
            Assert.That(view.Gesture.ActivePointerId, Is.EqualTo(15));
            Assert.That(UpdateItem("activity", new SegmentItemData { Title = new string('M', 120), BadgeCount = 2 }), Is.True);
            Assert.That(view.Gesture.ActivePointerId, Is.EqualTo(15)); Assert.That(view.Phase, Is.EqualTo(phase));
            Assert.That(bar.ItemsRoot.anchoredPosition, Is.EqualTo(before)); Assert.That(scroll.velocity.x, Is.EqualTo(75));
            p.position += p.delta; scroll.OnDrag(p); Assert.That(bar.ItemsRoot.anchoredPosition.x, Is.LessThan(before.x));
            scroll.OnEndDrag(p); yield return null;
        }
        sealed class UnbakedPageFactory : ISegmentPageFactory
        {
            public SegmentPage Create(Transform parent)
            {
                var root = new GameObject("UnbakedPage", typeof(RectTransform), typeof(SegmentPage));
                root.transform.SetParent(parent, false);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(root.transform, false);
                label.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
                root.AddComponent<SegmentUI.Integrations.SegmentRtlLayoutBridge>();
                return root.GetComponent<SegmentPage>();
            }
            public void Release(SegmentPage page) => Object.Destroy(page.gameObject);
        }
        [UnityTest] public IEnumerator RtlFirstSnapshotWaitsForBindingAndDisabledBridgeResynchronizes()
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);
            view.SetPages(new[] { new SegmentPageDescriptor {
                Id = "bound", Source = PageSource.FromFactory(new UnbakedPageFactory()),
                BindPage = page => {
                    var text = page.GetComponentsInChildren<Text>(true)[0];
                    text.alignment = TextAnchor.UpperLeft;
                    text.rectTransform.anchoredPosition = new Vector2(37, 9);
                }
            } }, "bound");
            var current = view.GetPage("bound");
            var label = current.GetComponentsInChildren<Text>(true)[0];
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.UpperRight));
            Assert.That(label.rectTransform.anchoredPosition.x, Is.EqualTo(-37));
            var bridge = current.GetComponent<SegmentUI.Integrations.SegmentRtlLayoutBridge>();
            bridge.enabled = false;
            view.SetDirection(SegmentLayoutDirection.LeftToRight);
            bridge.enabled = true;
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.UpperLeft));
            Assert.That(label.rectTransform.anchoredPosition.x, Is.EqualTo(37));
            yield return null;
        }
        [UnityTest] public IEnumerator IndependentRtlRootsHandleLazyNestedPagesAndRepeatedCollection()
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);
            Assert.That(view.GetPage("native"), Is.Null, "Page remains lazy.");
            view.Select("native", false);
            var page = view.GetPage("native");
            var adapter = page.GetComponent<RTLLayout.RtlLayoutRoot>();
            Assert.That(adapter, Is.Not.Null);
            var label = page.GetComponentsInChildren<Text>(true)[0];
            for (int i = 0; i < 5; i++) { adapter.Collect(); adapter.SetRightToLeft(true); }
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            var item = view.Bar.GetItem("activity");
            Assert.That(item.Icon.rectTransform.anchorMin.x, Is.EqualTo(1));
            Assert.That(item.Icon.rectTransform.anchoredPosition.x, Is.EqualTo(-24));
            var dynamic = new GameObject("DynamicLabel", typeof(RectTransform), typeof(Text));
            dynamic.transform.SetParent(item.transform, false);
            dynamic.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            item.ApplyDirection(true); item.ApplyDirection(true);
            Assert.That(dynamic.GetComponent<Text>().alignment, Is.EqualTo(TextAnchor.MiddleRight));
            view.Select("nested", false);
            var child = view.GetPage("nested").GetComponentInChildren<SegmentView>();
            Assert.That(child.IsRightToLeft, Is.True);
            child.SetDirection(SegmentLayoutDirection.LeftToRight);
            var childLabel = child.GetPage("left").GetComponentsInChildren<Text>(true)[0];
            for (int i = 0; i < 3; i++)
            {
                demo.GetComponent<RTLLayout.RtlLayoutRoot>().Collect();
                view.SetDirection(SegmentLayoutDirection.LeftToRight);
                view.SetDirection(SegmentLayoutDirection.RightToLeft);
            }
            Assert.That(childLabel.alignment, Is.EqualTo(TextAnchor.MiddleLeft), "Outer root must not override explicitly LTR child view.");
            Assert.That(child.IsRightToLeft, Is.False);
            view.Select("native", false);
            Assert.That(view.GetPage("native"), Is.SameAs(page));
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            view.SetDirection(SegmentLayoutDirection.LeftToRight);
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(item.Icon.rectTransform.anchoredPosition.x, Is.EqualTo(24));
            yield return null;
        }
        [UnityTest] public IEnumerator DemoCanRetryAfterMissingPageReferenceIsRestored()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SegmentUI/Samples/Generated/SegmentDemo.prefab");
            var retryRoot = Object.Instantiate(prefab);
            var sample = retryRoot.GetComponent<SegmentUI.Samples.SegmentDemo>();
            try
            {
                sample.enabled = false; // Explicit initialization before Start.
                var pageB = sample.PageB;
                sample.PageB = null;
                Assert.Throws<System.ArgumentException>(() => sample.Initialize());
                Assert.That(sample.View.CurrentPageId, Is.Null);
                sample.PageB = pageB;
                sample.Initialize();
                Assert.That(sample.View.CurrentPageId, Is.EqualTo("dispatch"));
                sample.View.Select("activity", false);
                Assert.That(sample.View.CurrentPageId, Is.EqualTo("activity"));
            }
            finally { Object.Destroy(retryRoot); }
            yield return null;
        }
        [UnityTest] public IEnumerator DestroyingOnlyBarComponentReleasesItsOwnedVisualInstances()
        {
            var bar = view.Bar; var item = bar.GetItem("dispatch");
            var indicator = bar.ForegroundLayer.GetComponentInChildren<SegmentIndicator>(true);
            Assert.That(item, Is.Not.Null); Assert.That(indicator, Is.Not.Null);
            Object.Destroy(bar); yield return null; yield return null;
            Assert.That(item == null, Is.True);
            Assert.That(indicator == null, Is.True);
        }
        [UnityTest] public IEnumerator DemoTitleDragKeepsControlledScrollerEnabledAndDoesNotPage()
        {
            var bar = view.Bar; bar.LayoutMode = SegmentBarLayout.ContentWidth;
            bar.GetItem("dispatch").Label.text = new string('W', 60);
            bar.GetItem("activity").Label.text = new string('W', 60);
            yield return null; Canvas.ForceUpdateCanvases();
            var rect = bar.TitleScroll.viewport;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector2(rect.rect.xMin + 100, rect.rect.center.y)));
            float before = bar.ItemsRoot.anchoredPosition.x;
            Step(point, true, true); Step(point + new Vector2(-120, 0), false, true);
            Assert.That(bar.TitleScroll.enabled, Is.True, "Owned title dragging must not disable its receiver.");
            Assert.That(view.Gesture.ActivePointerId, Is.EqualTo(-1));
            Assert.That(bar.ItemsRoot.anchoredPosition.x, Is.LessThan(before));
            Assert.That(view.Pager.Position, Is.Zero);
            Step(point + new Vector2(-120, 0), false, false, true);
            yield return null;
            Assert.That(view.Gesture.ActivePointerId, Is.Null);
            Assert.That(view.CurrentPageId, Is.EqualTo("dispatch"));
        }
        [UnityTest] public IEnumerator DemoContentHorizontalDragKeepsVerticalListStillAndSettles()
        {
            Assert.That(view.Bar, Is.Not.Null);
            var scroll = view.GetPage("dispatch").GetComponentInChildren<SegmentScrollRect>();
            Assert.That(scroll.IsTitleBar, Is.False);
            var rect = scroll.viewport;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            float y = scroll.content.anchoredPosition.y;
            Step(point, true, true);
            Step(point + new Vector2(-160, 0), false, true);
            Assert.That(view.Pager.Position, Is.GreaterThan(0));
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(y).Within(.01f));
            Assert.That(view.Gesture.ActivePointerId, Is.EqualTo(-1));
            Step(point + new Vector2(-160, 0), false, false, true);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.Gesture.ActivePointerId, Is.Null);
            Assert.That(view.Pager.IsSettling, Is.False);
            Assert.That(view.Pager.Position, Is.EqualTo(Mathf.Round(view.Pager.Position)).Within(.001f));
            Assert.That(view.CurrentPageId, Is.EqualTo("dispatch").Or.EqualTo("activity"));
        }
        [UnityTest] public IEnumerator DemoNativePagePreservesTapAndRoutesButtonHorizontalDrag()
        {
            Assert.That(view.Select("native",false),Is.EqualTo(SegmentSelectionResult.Accepted));
            yield return null;Canvas.ForceUpdateCanvases();
            var page=view.GetPage("native");var scroll=page.GetComponent<ScrollRect>();
            Assert.That(scroll.GetType(),Is.EqualTo(typeof(ScrollRect)));
            Assert.That(scroll.GetComponent<SegmentScrollRectAdapter>(),Is.Not.Null);
            var button=page.GetComponentInChildren<Button>();Assert.That(button.GetType(),Is.EqualTo(typeof(Button)));
            var label=button.GetComponentInChildren<Text>();var rect=(RectTransform)button.transform;
            Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(96));
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Step(point,true,true);Step(point,false,false,true);
            Assert.That(label.text,Does.Contain("TAPS 1"));
            float y=scroll.content.anchoredPosition.y;
            Step(point,true,true);Step(point+new Vector2(180,0),false,true);
            Assert.That(view.Pager.Position,Is.LessThan(3));
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
            Step(point+new Vector2(180,0),false,false,true);
            Assert.That(label.text,Does.Contain("TAPS 1"));
            view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
        }
        [UnityTest] public IEnumerator DemoNativeVerticalDragKeepsPageAndSupportsCacheAndReset()
        {
            Assert.That(view.Select("native",false),Is.EqualTo(SegmentSelectionResult.Accepted));
            yield return null;Canvas.ForceUpdateCanvases();
            var scroll=view.GetPage("native").GetComponent<ScrollRect>();var rect=scroll.viewport;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Step(point,true,true);Step(point+new Vector2(0,140),false,true);
            Assert.That(view.Phase,Is.EqualTo(SegmentPhase.DraggingVertical));
            Step(point+new Vector2(180,160),false,true);
            Assert.That(view.Pager.Position,Is.EqualTo(3));
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(0));
            Step(point+new Vector2(180,160),false,false,true);scroll.StopMovement();
            float y=scroll.content.anchoredPosition.y;
            view.Select("activity",false);view.Select("native",false);
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
            demo.transform.Find("SafeAreaContent/Footer/Controls/Reset").GetComponent<Button>().onClick.Invoke();
            Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(1).Within(.001f));
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
        }
        [UnityTest] public IEnumerator NestedLiveUpdatesKeepLazyPagesAndStopWithParentSwitch()
        {
            var sample = demo.GetComponent<SegmentUI.Samples.SegmentDemo>();
            sample.ToggleLiveUpdates(); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(view.GetPage("nested"), Is.Null, "LIVE must not create a hidden parent page.");
            yield return OpenNested(); var child = ChildView; var page = child.GetPage("left");
            var item = child.Bar.GetItem("left"); string title = item.Label.text;
            yield return new WaitForSecondsRealtime(.16f);
            Assert.That(item.Label.text, Is.Not.EqualTo(title)); Assert.That(item.Badge, Is.Not.Null);
            Assert.That(child.GetPage("middle"), Is.Null); Assert.That(child.GetPage("right"), Is.Null);
            Assert.That(child.GetPage("left"), Is.SameAs(page)); Assert.That(page.BindingVersion, Is.EqualTo(1));
            var nested = (SegmentUI.Samples.SegmentNestedDemoPage)view.GetPage("nested");
            while (nested.LiveUpdateCount % 10 != 0) nested.UpdateLiveItems();
            Assert.That(item.Badge.gameObject.activeSelf, Is.False);
            nested.UpdateLiveItems(); Assert.That(item.Badge.gameObject.activeSelf, Is.True);
            view.Select("activity", false); title = item.Label.text;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(item.Label.text, Is.Not.EqualTo(title), "Cached child titles may update without entering their pages.");
            sample.ToggleLiveUpdates(); title = item.Label.text; string badge = item.Badge.text;
            yield return new WaitForSecondsRealtime(.16f);
            Assert.That(item.Label.text, Is.EqualTo(title)); Assert.That(item.Badge.text, Is.EqualTo(badge));
            view.Select("nested", false); Assert.That(child.GetPage("left"), Is.SameAs(page));
        }
        [UnityTest] public IEnumerator NestedLiveUpdatesPreserveChildParentAndNativeScrollGesturesInBothDirections()
        {
            demo.GetComponent<SegmentUI.Samples.SegmentDemo>().ToggleLiveUpdates();
            foreach (bool rtl in new[] { false, true })
            for (int mode = 0; mode < 4; mode++)
            {
                view.Select("activity", false); view.SetDirection(rtl ? SegmentLayoutDirection.RightToLeft : SegmentLayoutDirection.LeftToRight);
                yield return OpenNested(); var child = ChildView; string id = mode == 3 ? "right" : "left";
                child.Select(id, false); yield return null; Canvas.ForceUpdateCanvases();
                var page = child.GetPage(id); var scroll = page.GetComponent<ScrollRect>();
                scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
                var item = child.Bar.GetItem(id); var point = ChildPoint(child);
                float sign = rtl ? 1 : -1;
                var delta = mode == 1 ? new Vector2(0, 90) : new Vector2(sign * ChildScreenWidth(child) * (mode == 2 ? -.65f : .65f), 0);
                var end = point + delta; Step(point, true, true); Step(end, false, true);
                bool parentOwns = mode >= 2; var owner = parentOwns ? view : child;
                Assert.That(owner.Phase, Is.EqualTo(mode == 1 ? SegmentPhase.DraggingVertical : SegmentPhase.DraggingHorizontal));
                var pointer = owner.Gesture.ActivePointerId; Assert.That(pointer.HasValue, Is.True);
                float parentPosition = view.Pager.Position, childPosition = child.Pager.Position;
                int parentVersion = view.Pager.Version, childVersion = child.Pager.Version;
                var listPosition = scroll.content.anchoredPosition; string title = item.Label.text;
                yield return new WaitForSecondsRealtime(.16f);
                Assert.That(item.Label.text, Is.Not.EqualTo(title)); Assert.That(item.Badge, Is.Not.Null);
                Assert.That(owner.Gesture.ActivePointerId, Is.EqualTo(pointer));
                Assert.That(view.Pager.Position, Is.EqualTo(parentPosition)); Assert.That(child.Pager.Position, Is.EqualTo(childPosition));
                Assert.That(view.Pager.Version, Is.EqualTo(parentVersion)); Assert.That(child.Pager.Version, Is.EqualTo(childVersion));
                Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(listPosition.y).Within(.1f));
                Assert.That(child.GetPage(id), Is.SameAs(page)); Assert.That(child.Bar.GetItem(id), Is.SameAs(item));
                Assert.That(item.RightToLeft, Is.EqualTo(rtl));
                Step(end, false, false, true); yield return new WaitForSecondsRealtime(.35f);
                Assert.That(view.Gesture.HasActivePointer, Is.False);
                if (mode == 0) Assert.That(child.CurrentPageId, Is.EqualTo("middle"));
                if (mode == 2) Assert.That(view.CurrentPageId, Is.EqualTo("activity"));
                if (mode == 3) Assert.That(view.CurrentPageId, Is.EqualTo("native"));
            }
        }
        [UnityTest] public IEnumerator NestedLiveUpdatesPreserveProgrammaticTransitionsInLtrAndRtl()
        {
            var sample = demo.GetComponent<SegmentUI.Samples.SegmentDemo>(); sample.ToggleLiveUpdates();
            foreach (bool rtl in new[] { false, true })
            {
                view.Select("activity", false); view.SetDirection(rtl ? SegmentLayoutDirection.RightToLeft : SegmentLayoutDirection.LeftToRight);
                yield return OpenNested(); var child = ChildView; child.Select("left", false);
                child.Select("middle", true); var target = child.GetPage("middle"); var item = child.Bar.GetItem("middle");
                int version = child.Pager.Version; string title = item.Label.text;
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(item.Label.text, Is.Not.EqualTo(title)); Assert.That(child.TargetPageId, Is.EqualTo("middle"));
                Assert.That(child.Pager.Version, Is.EqualTo(version));
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(child.CurrentPageId, Is.EqualTo("middle")); Assert.That(child.GetPage("middle"), Is.SameAs(target));
                title = item.Label.text; view.Select("native", true); version = view.Pager.Version;
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(item.Label.text, Is.Not.EqualTo(title)); Assert.That(view.TargetPageId, Is.EqualTo("native"));
                Assert.That(view.Pager.Version, Is.EqualTo(version));
                yield return new WaitForSecondsRealtime(.25f); Assert.That(view.CurrentPageId, Is.EqualTo("native"));
                view.Select("nested", false); Assert.That(child.CurrentPageId, Is.EqualTo("middle"));
            }
        }
        IEnumerator OpenNested()
        {
            Assert.That(view.Select("nested",false),Is.EqualTo(SegmentSelectionResult.Accepted));
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var child=ChildView;
            Assert.That(child.CanReceiveInput,Is.True,"child presented="+child.IsPresented+" active="+child.isActiveAndEnabled+" rect="+child.Pager.Viewport.rect+" layout="+child.Pager.HasLayout+" parent presented="+view.GetPage("nested").IsPresented);
        }
        SegmentView ChildView => view.GetPage("nested").GetComponentInChildren<SegmentView>();
        Vector2 ChildPoint(SegmentView child)
        {
            var scroll=child.GetPage(child.CurrentPageId).GetComponent<ScrollRect>();
            Assert.That(scroll.GetType(),Is.EqualTo(typeof(ScrollRect)));
            Assert.That(scroll.GetComponent<SegmentScrollRectAdapter>(),Is.Not.Null);
            return RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
        }
        float ChildScreenWidth(SegmentView child)
        {
            var r=child.Pager.Viewport;
            return Mathf.Abs(RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector2(r.rect.xMax,0))).x-RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector2(r.rect.xMin,0))).x);
        }
        [UnityTest] public IEnumerator NestedNativeVerticalDragLocksBothPagersAndParentCancelReleasesChild()
        {
            yield return OpenNested();var child=ChildView;var point=ChildPoint(child);
            var scroll=child.GetPage("left").GetComponent<ScrollRect>();
            Step(point,true,true);Step(point+new Vector2(0,120),false,true);
            Step(point+new Vector2(-180,140),false,true);
            Assert.That(child.Phase,Is.EqualTo(SegmentPhase.DraggingVertical));
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(0));
            Assert.That(view.Pager.Position,Is.EqualTo(2));Assert.That(child.Pager.Position,Is.Zero);
            var other=new PointerEventData(EventSystem.current){pointerId=2,button=PointerEventData.InputButton.Left,position=point,pressPosition=point};
            Assert.That(view.Gesture.Press(other,null),Is.False);
            view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            float y=scroll.content.anchoredPosition.y;
            Step(point+new Vector2(0,220),false,true);Step(point+new Vector2(0,220),false,false,true);
            Assert.That(view.Gesture.HasActivePointer,Is.False);Assert.That(child.Gesture.ActivePointerId,Is.Null);
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
        }
        [UnityTest] public IEnumerator NestedHorizontalDragMovesChildAndPreservesSelectionWhenParentReturns()
        {
            yield return OpenNested();var child=ChildView;var point=ChildPoint(child);
            var scroll=child.GetPage("left").GetComponent<ScrollRect>();float y=scroll.content.anchoredPosition.y;
            var end=point-new Vector2(ChildScreenWidth(child)*.65f,0);
            Step(point,true,true);Step(end,false,true);
            Assert.That(child.Pager.Position,Is.GreaterThan(.5f));Assert.That(view.Pager.Position,Is.EqualTo(2));
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
            Step(end,false,false,true);yield return new WaitForSecondsRealtime(.5f);
            Assert.That(child.CurrentPageId,Is.EqualTo("middle"));
            view.Select("activity",false);Assert.That(child.IsPresented,Is.False);
            view.Select("nested",false);Assert.That(child.IsPresented,Is.True);Assert.That(child.CurrentPageId,Is.EqualTo("middle"));
        }
        [UnityTest] public IEnumerator NestedLeftEdgeYieldsToPreviousParentPage()
        {
            yield return OpenNested();var child=ChildView;var point=ChildPoint(child);
            var end=point+new Vector2(ChildScreenWidth(child)*.65f,0);
            Step(point,true,true);Step(end,false,true);
            Assert.That(view.Pager.Position,Is.LessThan(2));Assert.That(child.Pager.Position,Is.Zero);
            Step(end,false,false,true);yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.CurrentPageId,Is.EqualTo("activity"));Assert.That(child.IsPresented,Is.False);
            Assert.That(view.Gesture.HasActivePointer,Is.False);
        }
        [UnityTest] public IEnumerator NestedRightEdgeYieldsToNextParentPage()
        {
            yield return OpenNested();var child=ChildView;child.Select("right",false);
            yield return null;Canvas.ForceUpdateCanvases();var point=ChildPoint(child);
            var end=point-new Vector2(ChildScreenWidth(child)*.65f,0);
            Step(point,true,true);Step(end,false,true);
            Assert.That(view.Pager.Position,Is.GreaterThan(2));Assert.That(child.Pager.Position,Is.EqualTo(2));
            Step(end,false,false,true);yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.CurrentPageId,Is.EqualTo("native"));Assert.That(child.IsPresented,Is.False);
            Assert.That(view.Gesture.HasActivePointer,Is.False);
        }
        [UnityTest] public IEnumerator RtlSwipeRightAdvancesLogicalPageAndDirectionSwitchCancelsDrag()
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);
            Assert.That(view.CurrentPageId,Is.EqualTo("dispatch"));
            var scroll=view.GetPage("dispatch").GetComponent<ScrollRect>();var rect=scroll.viewport;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var end=point+new Vector2(ChildScreenWidth(view)*.65f,0);
            Step(point,true,true);Step(end,false,true);
            Assert.That(view.Pager.Position,Is.GreaterThan(.5f));
            Step(end,false,false,true);yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.CurrentPageId,Is.EqualTo("activity"));
            scroll=view.GetPage("activity").GetComponent<ScrollRect>();rect=scroll.viewport;
            point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Step(point,true,true);Step(point+new Vector2(100,0),false,true);
            view.SetDirection(SegmentLayoutDirection.LeftToRight);
            Assert.That(view.Gesture.HasActivePointer,Is.False);Assert.That(view.Pager.Position,Is.EqualTo(1));
            Step(point+new Vector2(200,0),false,true);Step(point+new Vector2(200,0),false,false,true);
            Assert.That(view.CurrentPageId,Is.EqualTo("activity"));Assert.That(view.Pager.Position,Is.EqualTo(1));
        }
        IEnumerator RtlNestedEdge(bool last)
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);yield return OpenNested();var child=ChildView;
            Assert.That(child.IsRightToLeft,Is.True);child.Select(last?"right":"left",false);
            yield return null;Canvas.ForceUpdateCanvases();var point=ChildPoint(child);
            var end=point+new Vector2(ChildScreenWidth(child)*.65f*(last?1:-1),0);
            Step(point,true,true);Step(end,false,true);
            Assert.That(child.Pager.Position,Is.EqualTo(last?2:0));
            Step(end,false,false,true);yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.CurrentPageId,Is.EqualTo(last?"native":"activity"));Assert.That(view.Gesture.HasActivePointer,Is.False);
        }
        [UnityTest] public IEnumerator RtlFirstChildOutwardLeftDragYieldsToPreviousParent() { yield return RtlNestedEdge(false); }
        [UnityTest] public IEnumerator RtlLastChildOutwardRightDragYieldsToNextParent() { yield return RtlNestedEdge(true); }
        [UnityTest] public IEnumerator ChildDirectionOverrideDoesNotChangeParentLogicalOrder()
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);yield return OpenNested();var child=ChildView;
            child.SetDirection(SegmentLayoutDirection.LeftToRight);
            Assert.That(child.IsRightToLeft,Is.False);Assert.That(view.IsRightToLeft,Is.True);
            var point=ChildPoint(child);var end=point-new Vector2(ChildScreenWidth(child)*.65f,0);
            Step(point,true,true);Step(end,false,true);
            Assert.That(child.Pager.Position,Is.GreaterThan(.5f));Assert.That(view.Pager.Position,Is.EqualTo(2));
            Step(end,false,false,true);yield return new WaitForSecondsRealtime(.5f);
            Assert.That(child.CurrentPageId,Is.EqualTo("middle"));
            view.SetDirection(SegmentLayoutDirection.LeftToRight);view.SetDirection(SegmentLayoutDirection.RightToLeft);
            Assert.That(child.IsRightToLeft,Is.False);Assert.That(child.CurrentPageId,Is.EqualTo("middle"));
        }
        [UnityTest] public IEnumerator FailedPrefabRetryInSameFrameDoesNotRevealPendingDestroyedPage()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SegmentUI/Samples/Generated/PageNative.prefab").GetComponent<SegmentPage>();
            SegmentPage failed=null;int attempts=0;
            var descriptor=new SegmentPageDescriptor{Id="retry",Source=PageSource.FromPrefab(prefab),BindPage=p=>{if(attempts++==0){failed=p;throw new System.InvalidOperationException("expected binding failure");}}};
            view.SetPages(new[]{descriptor});Assert.That(view.CurrentPageId,Is.Null);
            view.Select("retry",false);
            Assert.That(view.CurrentPageId,Is.EqualTo("retry"));
            Assert.That(failed==null || !failed.gameObject.activeInHierarchy,Is.True,"A pending Destroy must not reappear when its eager slot is reused.");
            yield return null;
        }
        void Step(Vector2 point, bool down = false, bool held = false, bool up = false)
        { mouse.Position = point; mouse.Down = down; mouse.Held = held; mouse.Up = up; module.Step(); }
    }
}
#endif
