using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace SegmentUI.Tests
{
    public sealed class ReplayMouse : BaseInput
    {
        public Vector2 Position;
        public bool Down, Up, Held;
        public override bool mousePresent => true;
        public override Vector2 mousePosition => Position;
        public override Vector2 mouseScrollDelta => Vector2.zero;
        public override bool GetMouseButtonDown(int button) => button==0 && Down;
        public override bool GetMouseButtonUp(int button) => button==0 && Up;
        public override bool GetMouseButton(int button) => button==0 && Held;
    }
    public sealed class ReplayModule : StandaloneInputModule
    {
        // Actual UGUI raycast/press/drag/release path, without the editor focus gate.
        public void Step() => ProcessMouseEvent();
    }
    public class SegmentEventSystemTests : SegmentGestureTests
    {
        ReplayMouse mouse; ReplayModule module; Button button; Vector2 point;
        int clicks;
        [UnitySetUp] public IEnumerator AddButton()
        {
            clicks=0;
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root.AddComponent<GraphicRaycaster>();
            var node=new GameObject("BusinessButton",typeof(RectTransform),typeof(Image),typeof(Button),typeof(SegmentPointerRelay));
            var rect=(RectTransform)node.transform; rect.SetParent(scroll.content,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(400,120);
            button=node.GetComponent<Button>();button.onClick.AddListener(()=>clicks++);
            mouse=events.AddComponent<ReplayMouse>();module=events.AddComponent<ReplayModule>(); module.inputOverride=mouse;module.enabled=false;
            yield return null; Canvas.ForceUpdateCanvases();
            point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        }
        void Step(Vector2 p,bool down=false,bool held=false,bool up=false)
        { mouse.Position=p;mouse.Down=down;mouse.Held=held;mouse.Up=up;module.Step(); }
        [UnityTest] public IEnumerator RealClickReleasesScopeAndFiresOnce()
        {
            Step(point,true,true);Step(point,false,false,true);
            Assert.That(clicks,Is.EqualTo(1));Assert.That(view.Gesture.ActivePointerId,Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator RealDragFromButtonCancelsClickAndRoutesHorizontal()
        {
            Step(point,true,true);Step(point+new Vector2(-160,0),false,true);
            Assert.That(pager.Position,Is.GreaterThan(.2f));
            Step(point+new Vector2(-160,0),false,false,true);
            Assert.That(clicks,Is.Zero);Assert.That(view.Gesture.ActivePointerId,Is.Null);
            yield return new WaitForSecondsRealtime(.3f);
        }
    }
}
