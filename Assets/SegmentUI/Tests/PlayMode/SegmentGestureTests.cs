using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace SegmentUI.Tests
{
    public sealed class GesturePageFactory : ISegmentPageFactory
    {
        public SegmentPage Create(Transform parent)
        {
            var page = new GameObject("ScrollPage", typeof(RectTransform), typeof(SegmentPage));
            page.transform.SetParent(parent, false);
            var node = new GameObject("Scroll", typeof(RectTransform)); node.transform.SetParent(page.transform, false);
            var rect = (RectTransform)node.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var scroll = node.AddComponent<SegmentScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
            scroll.viewport = rect;
            scroll.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); scroll.content.SetParent(rect, false);
            scroll.content.anchorMin = scroll.content.anchorMax = scroll.content.pivot = new Vector2(0, 1);
            scroll.content.sizeDelta = new Vector2(400, 1800);
            return page.GetComponent<SegmentPage>();
        }
        public void Release(SegmentPage page) => Object.Destroy(page.gameObject);
    }
    public class SegmentGestureTests
    {
        protected GameObject root, events;
        protected SegmentView view;
        protected SegmentPager pager;
        protected SegmentScrollRect scroll;
        [UnitySetUp] public IEnumerator Setup()
        {
            events = new GameObject("Events", typeof(EventSystem));
            root = new GameObject("Segment", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)root.transform).sizeDelta = new Vector2(400, 600);
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>(); viewport.SetParent(root.transform, false); viewport.sizeDelta = new Vector2(400,600);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            pager = root.AddComponent<SegmentPager>(); pager.Configure(viewport, content);
            view = root.AddComponent<SegmentView>(); view.Configure(pager, null);
            var f = new GesturePageFactory();
            view.SetPages(new[]
            {
                new SegmentPageDescriptor { Id="a", Source=PageSource.FromFactory(f) },
                new SegmentPageDescriptor { Id="b", Source=PageSource.FromFactory(f) },
                new SegmentPageDescriptor { Id="c", Source=PageSource.FromFactory(f) }
            });
            scroll = view.GetPage("a").GetComponentInChildren<SegmentScrollRect>();
            Canvas.ForceUpdateCanvases(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Object.Destroy(root); Object.Destroy(events); yield return null; }
        protected PointerEventData Pointer(int id = -1) => new PointerEventData(EventSystem.current) { pointerId=id, button=PointerEventData.InputButton.Left, position=new Vector2(100,100), pressPosition=new Vector2(100,100), eligibleForClick=true };
        protected void Begin(PointerEventData p, Vector2 delta)
        {
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.initializePotentialDrag);
            p.position += delta; p.delta=delta;
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.dragHandler);
        }
        [UnityTest] public IEnumerator HorizontalGestureDoesNotMoveVerticalList()
        {
            var p=Pointer(); float y=scroll.content.anchoredPosition.y;
            Begin(p,new Vector2(-140,15));
            Assert.That(pager.Position,Is.GreaterThan(.2f));
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(y).Within(.01f));
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(view.CurrentPageId,Is.EqualTo("a").Or.EqualTo("b"));
            Assert.That(pager.Position,Is.EqualTo(Mathf.Round(pager.Position)));
        }
        [UnityTest] public IEnumerator VerticalDirectionRemainsLockedWhenFingerTurns()
        {
            var p=Pointer(); Begin(p,new Vector2(10,120));
            Assert.That(view.Phase,Is.EqualTo(SegmentPhase.DraggingVertical));
            p.position += new Vector2(-250,10); p.delta=new Vector2(-250,10);
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.dragHandler);
            Assert.That(pager.Position,Is.Zero);
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(0));
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler); yield return null;
        }
        [UnityTest] public IEnumerator SecondPointerCannotStealAndCancelRejectsLateEnd()
        {
            var p=Pointer(1); Begin(p,new Vector2(-100,0));
            Assert.That(view.Gesture.ActivePointerId,Is.EqualTo(1));
            var other=Pointer(2); Begin(other,new Vector2(0,200));
            Assert.That(view.Gesture.ActivePointerId,Is.EqualTo(1));
            view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);
            yield return null;
            Assert.That(view.Gesture.ActivePointerId,Is.Null);
            Assert.That(pager.Position,Is.Zero);
            Assert.That(view.CurrentPageId,Is.EqualTo("a"));
        }
        [UnityTest] public IEnumerator TouchingIntermediateAlignedPageDoesNotEnableItsVerticalScroll()
        {
            view.Select("c",true); pager.SetPosition(1);
            var p=Pointer(); Begin(p,new Vector2(0,120));
            Assert.That(view.Phase,Is.EqualTo(SegmentPhase.DraggingHorizontal));
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(view.CurrentPageId,Is.EqualTo("b"));
        }
        [UnityTest] public IEnumerator HalfwayReleasePrefersCommittedPageRatherThanEvenRounding()
        {
            view.Select("b",false); scroll=view.GetPage("b").GetComponentInChildren<SegmentScrollRect>();
            pager.SetPosition(.5f);
            var p=Pointer();Begin(p,new Vector2(0,100));
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(view.CurrentPageId,Is.EqualTo("b"));
        }
        [UnityTest] public IEnumerator NestedChildPointerBlocksRootKeyboardSubmit()
        {
            var child=new GameObject("Nested",typeof(RectTransform));child.transform.SetParent(view.GetPage("a").transform,false);
            var vp=child.GetComponent<RectTransform>();vp.sizeDelta=new Vector2(400,500);
            var content=new GameObject("NestedContent",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(vp,false);
            var childPager=child.AddComponent<SegmentPager>();childPager.Configure(vp,content);
            var nested=child.AddComponent<SegmentView>();nested.Configure(childPager,null);
            nested.SetPages(new[]{new SegmentPageDescriptor{Id="child",Source=PageSource.FromFactory(new GesturePageFactory())}});
            var childScroll=nested.GetPage("child").GetComponentInChildren<SegmentScrollRect>();
            var p=Pointer(1);childScroll.OnInitializePotentialDrag(p);p.position+=new Vector2(0,100);childScroll.OnBeginDrag(p);childScroll.OnDrag(p);
            var node=new GameObject("RootButton",typeof(RectTransform),typeof(SegmentButton));node.transform.SetParent(root.transform,false);
            int clicks=0;var button=node.GetComponent<SegmentButton>();button.onClick.AddListener(()=>clicks++);
            button.OnSubmit(new BaseEventData(EventSystem.current));
            Assert.That(clicks,Is.Zero);
            view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            Assert.That(nested.Gesture.ActivePointerId,Is.Null);yield return null;
        }
        [UnityTest] public IEnumerator DisablingAnUnrelatedCachedScrollDoesNotCancelActivePointer()
        {
            view.Select("b",false);var cached=view.GetPage("b").GetComponentInChildren<SegmentScrollRect>();view.Select("a",false);
            var p=Pointer(1);Begin(p,new Vector2(-100,0));cached.enabled=false;
            Assert.That(view.Gesture.ActivePointerId,Is.EqualTo(1));
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);yield return null;
        }
        [UnityTest] public IEnumerator VerticalIntentWithoutVerticalReceiverDoesNotJigglePager()
        {
            view.Select("b",false);var p=Pointer();var scope=view.Gesture;
            scope.Press(p,null);p.position+=new Vector2(10,150);scope.Begin(p,null);scope.Drag(p);
            Assert.That(pager.Position,Is.EqualTo(1));scope.End(p);yield return null;
        }
    }
}
