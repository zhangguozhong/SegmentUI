using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace SegmentUI.Tests
{
    public sealed class CountingNativeScrollRect : ScrollRect
    {
        public int Initializations;
        public override void OnInitializePotentialDrag(PointerEventData data)
        { Initializations++; base.OnInitializePotentialDrag(data); }
    }
    public sealed class NativeScrollPageFactory : ISegmentPageFactory
    {
        public bool CountInitialization;
        public SegmentPage Create(Transform parent)
        {
            var page = new GameObject("NativePage", typeof(RectTransform), typeof(SegmentPage));
            page.transform.SetParent(parent, false);
            var node = new GameObject("NativeScroll", typeof(RectTransform), typeof(Image), CountInitialization ? typeof(CountingNativeScrollRect) : typeof(ScrollRect));
            var rect = (RectTransform)node.transform; rect.SetParent(page.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var scroll = node.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
            scroll.viewport = rect;
            scroll.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            scroll.content.SetParent(rect, false);
            scroll.content.anchorMin = scroll.content.anchorMax = scroll.content.pivot = new Vector2(0, 1);
            scroll.content.sizeDelta = new Vector2(400, 1800);
            var button = new GameObject("BusinessButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var br = (RectTransform)button.transform; br.SetParent(scroll.content, false);
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0, 1); br.sizeDelta = new Vector2(400, 120);
            return page.GetComponent<SegmentPage>();
        }
        public void Release(SegmentPage page) => Object.Destroy(page.gameObject);
    }
    public class SegmentNativeScrollTests
    {
        GameObject root, events;
        SegmentView view; ScrollRect scroll; ReplayMouse mouse; ReplayModule module;
        Vector2 blank, buttonPoint; int clicks;
        [UnitySetUp] public IEnumerator Setup()
        {
            clicks = 0;
            events = new GameObject("Events", typeof(EventSystem));
            mouse = events.AddComponent<ReplayMouse>(); module = events.AddComponent<ReplayModule>();
            module.inputOverride = mouse; module.enabled = false;
            root = new GameObject("Segment", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false); viewport.sizeDelta = new Vector2(400, 600);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            var pager = root.AddComponent<SegmentPager>(); pager.Configure(viewport, content);
            view = root.AddComponent<SegmentView>(); view.Configure(pager, null);
            var factory = new NativeScrollPageFactory();
            view.SetPages(new[] { new SegmentPageDescriptor { Id="a", Source=PageSource.FromFactory(factory) }, new SegmentPageDescriptor { Id="b", Source=PageSource.FromFactory(factory) } });
            scroll = view.GetPage("a").GetComponentInChildren<ScrollRect>();
            var button = scroll.GetComponentInChildren<Button>(); button.onClick.AddListener(() => clicks++);
            yield return null; Canvas.ForceUpdateCanvases();
            blank = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            var br = (RectTransform)button.transform;
            buttonPoint = RectTransformUtility.WorldToScreenPoint(null, br.TransformPoint(br.rect.center));
        }
        [UnityTearDown] public IEnumerator Cleanup() { Object.Destroy(root); Object.Destroy(events); yield return null; }
        void Step(Vector2 p, bool down=false, bool held=false, bool up=false)
        { mouse.Position=p; mouse.Down=down; mouse.Held=held; mouse.Up=up; module.Step(); }
        [UnityTest] public IEnumerator NativeBlankHorizontalDragPagesWithoutMovingList()
        {
            float y=scroll.content.anchoredPosition.y;
            Step(blank,true,true); Step(blank+new Vector2(-140,10),false,true);
            Assert.That(view.Pager.Position,Is.GreaterThan(.2f));
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
            Assert.That(scroll.GetType(),Is.EqualTo(typeof(ScrollRect)));
            Step(blank+new Vector2(-140,10),false,false,true);yield return null;
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
        }
        [UnityTest] public IEnumerator NativeButtonDragCancelsClickButTapStillWorks()
        {
            Step(buttonPoint,true,true);Step(buttonPoint,false,false,true);
            Assert.That(clicks,Is.EqualTo(1));Assert.That(view.Gesture.ActivePointerId,Is.Null);
            Step(buttonPoint,true,true);Step(buttonPoint+new Vector2(-140,0),false,true);
            Assert.That(view.Pager.Position,Is.GreaterThan(.2f));
            Step(buttonPoint+new Vector2(-140,0),false,false,true);
            Assert.That(clicks,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator NativeVerticalDragLocksDirectionAndCancelClearsOwnership()
        {
            Step(blank,true,true);Step(blank+new Vector2(0,100),false,true);
            Assert.That(view.Phase,Is.EqualTo(SegmentPhase.DraggingVertical));
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(0));
            Step(blank+new Vector2(-150,120),false,true);
            Assert.That(view.Pager.Position,Is.Zero);
            view.CancelInteraction(SegmentCancelReason.FocusLost);
            float y=scroll.content.anchoredPosition.y;
            Step(blank+new Vector2(-180,180),false,true);Step(blank+new Vector2(-180,180),false,false,true);
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
            Assert.That(scroll.velocity,Is.EqualTo(Vector2.zero));yield return null;
        }
        [UnityTest] public IEnumerator DisablingNativeTargetCancelsItsGesture()
        {
            Step(blank,true,true);Step(blank+new Vector2(0,100),false,true);
            Assert.That(view.Gesture.ActivePointerId,Is.EqualTo(-1));
            scroll.enabled=false;yield return null;
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
            Step(blank+new Vector2(0,100),false,false,true);
        }
        [UnityTest] public IEnumerator NativeVerticalReleasePreservesInertia()
        {
            Step(blank,true,true);Step(blank+new Vector2(0,60),false,true);yield return null;
            Step(blank+new Vector2(0,120),false,true);yield return null;
            Step(blank+new Vector2(0,180),false,true);yield return null;
            Step(blank+new Vector2(0,180),false,false,true);
            Assert.That(scroll.velocity.y,Is.GreaterThan(0));
            float y=scroll.content.anchoredPosition.y;yield return null;
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(y));
            Assert.That(view.Pager.Position,Is.Zero);
        }
        [UnityTest] public IEnumerator SecondNativePointerCannotStealOrEndOwner()
        {
            var first=new PointerEventData(EventSystem.current){pointerId=1,button=PointerEventData.InputButton.Left,position=blank,pressPosition=blank};
            ExecuteEvents.Execute(scroll.gameObject,first,ExecuteEvents.initializePotentialDrag);
            Assert.That(first.pointerDrag,Is.Not.Null);
            first.position+=new Vector2(-100,0);
            ExecuteEvents.Execute(first.pointerDrag,first,ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(first.pointerDrag,first,ExecuteEvents.dragHandler);
            float position=view.Pager.Position;
            var second=new PointerEventData(EventSystem.current){pointerId=2,button=PointerEventData.InputButton.Left,position=blank,pressPosition=blank};
            ExecuteEvents.Execute(scroll.gameObject,second,ExecuteEvents.initializePotentialDrag);
            second.position+=new Vector2(0,100);
            ExecuteEvents.Execute(second.pointerDrag,second,ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(second.pointerDrag,second,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(second.pointerDrag,second,ExecuteEvents.endDragHandler);
            Assert.That(view.Gesture.ActivePointerId,Is.EqualTo(1));
            Assert.That(view.Pager.Position,Is.EqualTo(position));
            ExecuteEvents.Execute(first.pointerDrag,first,ExecuteEvents.endDragHandler);yield return null;
        }
        [UnityTest] public IEnumerator RepeatedInstallAndDestroyAdapterPreserveNativeComponent()
        {
            var originalContent=scroll.content;var originalViewport=scroll.viewport;
            SegmentScrollRectAdapter.Install(view.GetPage("a").transform);
            SegmentScrollRectAdapter.Install(view.GetPage("a").transform);
            Assert.That(scroll.GetComponents<SegmentScrollRectAdapter>().Length,Is.EqualTo(1));
            Step(blank,true,true);Step(blank+new Vector2(0,100),false,true);
            Object.Destroy(scroll.GetComponent<SegmentScrollRectAdapter>());yield return null;
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
            Step(blank+new Vector2(0,100),false,false,true);
            Assert.That(scroll.enabled,Is.True);Assert.That(scroll.content,Is.SameAs(originalContent));Assert.That(scroll.viewport,Is.SameAs(originalViewport));
            yield return null;
            Assert.That(scroll.GetComponentInChildren<SegmentScrollDragProxy>(),Is.Null);
        }
        [UnityTest] public IEnumerator NativeInitializationCallbackRunsOncePerPress()
        {
            var factory=new NativeScrollPageFactory { CountInitialization=true };
            view.SetPages(new[]{new SegmentPageDescriptor{Id="count",Source=PageSource.FromFactory(factory)}});
            yield return null;Canvas.ForceUpdateCanvases();
            var counting=view.GetPage("count").GetComponentInChildren<CountingNativeScrollRect>();
            var point=RectTransformUtility.WorldToScreenPoint(null,counting.viewport.TransformPoint(counting.viewport.rect.center));
            Step(point,true,true);Step(point+new Vector2(0,100),false,true);
            Assert.That(counting.Initializations,Is.EqualTo(1));
            Step(point+new Vector2(0,100),false,false,true);
        }
        [UnityTest] public IEnumerator ReleasedExistingPageReturnsToNativeDragging()
        {
            var slot=new GameObject("BorrowedSlot",typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(view.Pager.Content,false);
            var page=new NativeScrollPageFactory().Create(slot);
            view.SetPages(new[]{new SegmentPageDescriptor{Id="borrowed",Source=PageSource.FromExisting(page)}});
            yield return null;Canvas.ForceUpdateCanvases();
            var native=page.GetComponentInChildren<ScrollRect>();
            var point=RectTransformUtility.WorldToScreenPoint(null,native.viewport.TransformPoint(native.viewport.rect.center));
            var button=native.GetComponentInChildren<Button>();int restoredClicks=0;
            button.onClick.AddListener(()=>restoredClicks++);
            var br=(RectTransform)button.transform;
            var bp=RectTransformUtility.WorldToScreenPoint(null,br.TransformPoint(br.rect.center));
            view.SetPages(new SegmentPageDescriptor[0]);yield return null;
            Step(bp,true,true);Step(bp,false,false,true);
            Assert.That(restoredClicks,Is.EqualTo(1));
            Step(point,true,true);Step(point+new Vector2(0,60),false,true);Step(point+new Vector2(0,100),false,true);
            Assert.That(native.content.anchoredPosition.y,Is.GreaterThan(0));
            Step(point+new Vector2(0,100),false,false,true);
        }
    }
}
