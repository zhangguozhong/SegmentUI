using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Tests
{
    public sealed class ProbePage : SegmentPage
    {
        public static readonly List<string> Events = new List<string>();
        public static Action<ProbePage> ExitHook;
        public static Action<ProbePage> EnterHook;
        protected override void OnBind() => Events.Add("bind:" + PageId);
        protected override void OnEnter() { Events.Add("enter:" + PageId); EnterHook?.Invoke(this); }
        protected override void OnExit(SegmentCancelReason reason) { Events.Add("exit:" + PageId); ExitHook?.Invoke(this); }
        protected override void OnDirectionChanged(bool rtl) => Events.Add("direction:" + PageId + ":" + rtl);
        protected override void OnUnbind() => Events.Add("unbind:" + PageId);
        protected override void OnRelease() => Events.Add("release:" + PageId);
    }
    public sealed class ProbeFactory : ISegmentPageFactory
    {
        public int Created, Released;
        public bool Fail;
        public SegmentPage Create(Transform parent)
        {
            if (Fail) throw new InvalidOperationException("expected factory failure");
            Created++;
            var node = new GameObject("Probe", typeof(RectTransform), typeof(ProbePage));
            node.transform.SetParent(parent, false);
            return node.GetComponent<ProbePage>();
        }
        public void Release(SegmentPage page) { Released++; UnityEngine.Object.DestroyImmediate(page.gameObject); }
    }
    public class SegmentLifecycleTests
    {
        protected GameObject root;
        protected SegmentView view;
        protected SegmentPager pager;
        protected ProbeFactory factory;
        protected SegmentPageDescriptor[] pages;
        [SetUp] public void Setup()
        {
            ProbePage.Events.Clear(); ProbePage.ExitHook = null; ProbePage.EnterHook = null;
            root = new GameObject("Segment", typeof(RectTransform));
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false); viewport.sizeDelta = new Vector2(400, 600);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            pager = root.AddComponent<SegmentPager>(); pager.Configure(viewport, content);
            view = root.AddComponent<SegmentView>(); view.Configure(pager, null);
            factory = new ProbeFactory();
            pages = new[] { Page("a"), Page("b"), Page("c") };
        }
        protected SegmentPageDescriptor Page(string id) => new SegmentPageDescriptor { Id = id, Item = new SegmentItemData { Title = id }, Source = PageSource.FromFactory(factory) };
        [TearDown] public void Cleanup() { ProbePage.ExitHook = null; ProbePage.EnterHook = null; UnityEngine.Object.DestroyImmediate(root); }

        bool UpdateItem(string id, SegmentItemData data) => view.UpdateItem(id, data);
        SegmentBar ConfigureItemBar()
        {
            var node = new GameObject("Bar", typeof(RectTransform), typeof(SegmentBar));
            node.transform.SetParent(root.transform, false);
            ((RectTransform)node.transform).sizeDelta = new Vector2(400, 100);
            var bar = node.GetComponent<SegmentBar>();
            var items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(node.transform, false);
            var template = new GameObject("Template", typeof(RectTransform), typeof(Button), typeof(SegmentItemView)).GetComponent<SegmentItemView>();
            template.transform.SetParent(node.transform, false); template.Button = template.GetComponent<Button>();
            template.Label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            template.Label.transform.SetParent(template.transform, false);
            template.Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            template.Badge = new GameObject("Badge", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            template.Badge.transform.SetParent(template.transform, false); template.Badge.font = template.Label.font;
            template.gameObject.SetActive(false);
            bar.Configure(items, template, null, null); bar.LayoutMode = SegmentBarLayout.ContentWidth;
            typeof(SegmentView).GetField("bar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(view, bar);
            return bar;
        }
        [Test] public void UpdateItemKeepsPagesLazyAndPreservesInstancesAndEnabledState()
        {
            var bar = ConfigureItemBar(); pages[2].Item.IsEnabled = false;
            view.SetPages(pages); var page = view.GetPage("a"); var item = bar.GetItem("a"); var other = bar.GetItem("b");
            int version = view.DataVersion; ProbePage.Events.Clear();
            var data = new SegmentItemData { Title = "updated", BadgeCount = 7, IsEnabled = false };
            Assert.That(UpdateItem("a", data), Is.True); data.Title = "external mutation";
            Assert.That(UpdateItem("c", new SegmentItemData { Title = "disabled", BadgeCount = 2 }), Is.True);
            Assert.That(UpdateItem("b", new SegmentItemData { Title = "still lazy" }), Is.True);
            Assert.That(bar.GetItem("a"), Is.SameAs(item)); Assert.That(bar.GetItem("b"), Is.SameAs(other));
            Assert.That(item.Label.text, Is.EqualTo("updated")); Assert.That(item.Badge.text, Is.EqualTo("7"));
            Assert.That(item.IsEnabled, Is.True); Assert.That(bar.GetItem("c").IsEnabled, Is.False);
            Assert.That(item.IsCommitted, Is.True); Assert.That(item.SelectedWeight, Is.EqualTo(1));
            Assert.That(view.GetPage("a"), Is.SameAs(page)); Assert.That(view.GetPage("b"), Is.Null);
            Assert.That(factory.Created, Is.EqualTo(1)); Assert.That(factory.Released, Is.Zero);
            Assert.That(view.DataVersion, Is.EqualTo(version)); Assert.That(ProbePage.Events, Is.Empty);
            Assert.That(UpdateItem("missing", data), Is.False);
            Assert.That(UpdateItem(null, data), Is.False);
            Assert.Throws<ArgumentNullException>(() => view.UpdateItem("a", null));
            Assert.That(UpdateItem("a", new SegmentItemData { Title = "clear", BadgeCount = 0 }), Is.True);
            Assert.That(item.Badge.gameObject.activeSelf, Is.False);
        }
        [Test] public void UpdateItemPreservesTransitionAndTitleScrollVelocityWhileRelayouting()
        {
            var bar = ConfigureItemBar(); view.SetPages(pages);
            var layer = new GameObject("IndicatorLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(bar.transform, false); bar.ForegroundLayer = layer;
            var indicator = new GameObject("Recording", typeof(RectTransform), typeof(RecordingSegmentIndicator)).GetComponent<RecordingSegmentIndicator>();
            indicator.transform.SetParent(root.transform, false); indicator.gameObject.SetActive(false);
            view.SetIndicator(indicator);
            view.Select("b", true); pager.Tick(.05f);
            var recorder = layer.GetComponentInChildren<RecordingSegmentIndicator>(true);
            float progress = recorder.Frame.Progress;
            float indicatorWidth = recorder.Frame.RightItem.width;
            var item = bar.GetItem("b"); float width = ((RectTransform)item.transform).rect.width;
            float position = pager.Position, weight = item.SelectedWeight; int version = pager.Version;
            bar.TitleScroll.velocity = new Vector2(91, 0);
            Assert.That(UpdateItem("b", new SegmentItemData { Title = new string('W', 80), BadgeCount = 99 }), Is.True);
            Assert.That(((RectTransform)item.transform).rect.width, Is.GreaterThan(width));
            Assert.That(bar.TitleScroll.velocity.x, Is.EqualTo(91));
            Assert.That(recorder.Frame.Progress, Is.EqualTo(progress));
            Assert.That(recorder.Frame.RightItem.width, Is.GreaterThan(indicatorWidth));
            Assert.That(recorder.Frame.TransitionVersion, Is.EqualTo(version));
            Assert.That(item.SelectedWeight, Is.EqualTo(weight)); Assert.That(item.IsCommitted, Is.False);
            Assert.That(pager.Position, Is.EqualTo(position)); Assert.That(pager.Version, Is.EqualTo(version));
            Assert.That(view.TargetPageId, Is.EqualTo("b")); Assert.That(view.IsTransitioning, Is.True);
            pager.Tick(1); Assert.That(view.CurrentPageId, Is.EqualTo("b"));
        }
        [Test] public void UpdateItemFromSelectionCallbackCopiesQueuedDataAndDoesNotRebindPages()
        {
            var bar = ConfigureItemBar(); view.SetPages(pages);
            view.SelectionChanged += _ => {
                var data = new SegmentItemData { Title = "queued", BadgeCount = 4 };
                Assert.That(UpdateItem("b", data), Is.True); data.Title = "changed after request";
            };
            view.Select("b", false);
            Assert.That(bar.GetItem("b").Label.text, Is.EqualTo("queued"));
            Assert.That(view.GetPage("b").BindingVersion, Is.EqualTo(1));
            Assert.That(bar.GetItem("b").IsCommitted, Is.True);
        }
        [Test] public void UpdateItemQueuedAfterRemovalDoesNotResurrectPage()
        {
            var bar = ConfigureItemBar(); view.SetPages(pages); bool accepted = false;
            // A selection notification runs inside the mutation guard.
            view.SelectionChanged += change => {
                if (change.CurrentId != "b") return;
                view.SetPages(new[] { pages[0] });
                accepted = view.UpdateItem("b", new SegmentItemData { Title = "removed" });
            };
            view.Select("b", false);
            Assert.That(accepted, Is.True); Assert.That(view.CurrentPageId, Is.EqualTo("a"));
            Assert.That(view.GetPage("b"), Is.Null); Assert.That(bar.GetItem("b"), Is.Null);
        }
        [Test] public void UpdateItemRefreshesIconAndClampsShrinkingTitleRangeWithoutStoppingVelocity()
        {
            var bar = ConfigureItemBar();
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(bar.DefaultTemplate.transform, false); bar.DefaultTemplate.Icon = icon;
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                view.SetPages(pages);
                view.UpdateItem("a", new SegmentItemData { Title = new string('W', 200), Icon = sprite });
                var item = bar.GetItem("a"); Assert.That(item.Icon.sprite, Is.SameAs(sprite));
                Assert.That(item.Icon.gameObject.activeSelf, Is.True);
                bar.ItemsRoot.anchoredPosition = new Vector2(-500, 0); bar.TitleScroll.velocity = new Vector2(55, 0);
                view.UpdateItem("a", new SegmentItemData { Title = "short" });
                Assert.That(item.Icon.sprite, Is.Null); Assert.That(item.Icon.gameObject.activeSelf, Is.False);
                Assert.That(bar.ItemsRoot.anchoredPosition.x, Is.Zero.Within(.01f));
                Assert.That(bar.TitleScroll.velocity.x, Is.EqualTo(55));
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); }
        }
        [Test] public void UpdateItemWorksWithoutTitleBarAndDoesNotInstantiatePage()
        {
            view.SetPages(pages); int version = pager.Version;
            Assert.That(UpdateItem("b", new SegmentItemData { Title = "headless" }), Is.True);
            Assert.That(view.GetPage("b"), Is.Null); Assert.That(pager.Version, Is.EqualTo(version));
        }

        [Test] public void EmptySlotsExistBeforeLazyPagesAndLayoutOwnsContentWidth()
        {
            view.SetPages(pages,"a");
            Assert.That(pager.Content.GetComponent<HorizontalLayoutGroup>(),Is.Not.Null);
            Assert.That(pager.Content.GetComponent<ContentSizeFitter>().horizontalFit,Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(pager.Content.childCount,Is.EqualTo(3));Assert.That(factory.Created,Is.EqualTo(1));
            Assert.That(view.GetPage("b"),Is.Null);Assert.That(view.GetPage("c"),Is.Null);
            Assert.That(pager.Content.rect.width,Is.EqualTo(1200).Within(.01f));
            Assert.That(pager.Content.GetChild(1).childCount,Is.Zero);
            view.Select("c",false);
            Assert.That(factory.Created,Is.EqualTo(2));Assert.That(view.GetPage("b"),Is.Null);
            Assert.That(((RectTransform)view.GetPage("c").transform.parent).anchoredPosition.x,Is.EqualTo(800).Within(.01f));
        }
        [Test] public void ReorderAndDisabledPagesKeepEmptySlotsWithoutLayoutGaps()
        {
            view.SetPages(pages,"a");
            var empty=pager.Content.Find("PageSlot-b");Assert.That(empty,Is.Not.Null);
            pages[1].Item.IsEnabled=false;view.SetPages(new[]{pages[2],pages[1],pages[0]});
            Assert.That(factory.Created,Is.EqualTo(1));Assert.That(pager.Content.rect.width,Is.EqualTo(800).Within(.01f));
            Assert.That(empty.GetComponent<LayoutElement>().ignoreLayout,Is.True);
            Assert.That(pager.Content.Find("PageSlot-b"),Is.SameAs(empty));
            Assert.That(view.CurrentPageId,Is.EqualTo("a"));Assert.That(pager.Position,Is.EqualTo(1));
            pager.Viewport.sizeDelta=new Vector2(700,600);pager.RefreshLayout();
            Assert.That(pager.Content.rect.width,Is.EqualTo(1400).Within(.01f));
        }
        [Test] public void RtlMirrorsSlotsButPreservesLogicalSelectionAndLazyPages()
        {
            view.SetPages(pages,"a");
            var method=typeof(SegmentView).GetMethod("SetDirection");Assert.That(method,Is.Not.Null);
            method.Invoke(view,new[]{Enum.Parse(method.GetParameters()[0].ParameterType,"RightToLeft")});
            Assert.That(pager.Position,Is.Zero);Assert.That(view.CurrentPageId,Is.EqualTo("a"));
            Assert.That(factory.Created,Is.EqualTo(1));
            Assert.That(pager.Content.anchoredPosition.x,Is.EqualTo(-800).Within(.01f));
            Assert.That(((RectTransform)view.GetPage("a").transform.parent).anchoredPosition.x,Is.EqualTo(800).Within(.01f));
            view.Select("c",false);
            Assert.That(pager.Position,Is.EqualTo(2));Assert.That(pager.Content.anchoredPosition.x,Is.Zero.Within(.01f));
        }
        [Test] public void RtlDirectionHookReappliesWhenBusinessPageRebinds()
        {
            view.SetDirection(SegmentLayoutDirection.RightToLeft);view.SetPages(pages,"a");
            ProbePage.Events.Clear();pages[0].PageRevision++;view.SetPages(pages);
            CollectionAssert.Contains(ProbePage.Events,"direction:a:True");
        }
        [Test] public void PreviewCancellationDoesNotEnterDestination()
        {
            view.SetPages(pages, "a"); ProbePage.Events.Clear();
            view.Select("b", true); pager.Tick(.08f);
            view.CancelInteraction(SegmentCancelReason.PointerCancelled);
            CollectionAssert.DoesNotContain(ProbePage.Events, "enter:b");
            CollectionAssert.DoesNotContain(ProbePage.Events, "exit:a");
            Assert.That(view.CurrentPageId, Is.EqualTo("a"));
            Assert.That(pager.Position, Is.Zero);
        }
        [Test] public void LatestTargetCommitsExactlyOnce()
        {
            view.SetPages(pages, "a"); ProbePage.Events.Clear();
            view.Select("b", true); pager.Tick(.06f);
            view.Select("c", true); pager.Tick(1);
            Assert.That(view.CurrentPageId, Is.EqualTo("c"));
            CollectionAssert.DoesNotContain(ProbePage.Events, "enter:b");
            Assert.That(ProbePage.Events.FindAll(x => x == "exit:a").Count, Is.EqualTo(1));
            Assert.That(ProbePage.Events.FindAll(x => x == "enter:c").Count, Is.EqualTo(1));
        }
        [Test] public void InvalidSnapshotDoesNotDestroyCurrentPage()
        {
            view.SetPages(pages, "b");
            Assert.Throws<ArgumentException>(() => view.SetPages(new[] { Page("x"), Page("x") }));
            Assert.That(view.CurrentPageId, Is.EqualTo("b"));
            Assert.That(view.Select("missing"), Is.EqualTo(SegmentSelectionResult.Invalid));
        }
        [Test] public void DisabledPagesAreSkippedAndSnapshotIsCopied()
        {
            pages[1].Item.IsEnabled = false;
            view.SetPages(pages, "a");
            pages[0].Item.IsEnabled = false;
            Assert.That(view.Select("b"), Is.EqualTo(SegmentSelectionResult.Unavailable));
            view.Select("c", false);
            Assert.That(pager.Position, Is.EqualTo(1));
            Assert.That(view.Select("a", false), Is.EqualTo(SegmentSelectionResult.Accepted));
        }
        [Test] public void HideAndShowPairsPresentationWithoutChangingSelection()
        {
            view.SetPages(pages, "a"); ProbePage.Events.Clear();
            view.SetPresented(false); view.SetPresented(false);
            view.Select("b", false);
            CollectionAssert.DoesNotContain(ProbePage.Events, "enter:b");
            view.SetPresented(true); view.SetPresented(true);
            Assert.That(ProbePage.Events.FindAll(x => x == "exit:a").Count, Is.EqualTo(1));
            Assert.That(ProbePage.Events.FindAll(x => x == "enter:b").Count, Is.EqualTo(1));
        }
        [Test] public void CurrentRemovalWithFailedReplacementLeavesNoInvalidCurrent()
        {
            view.SetPages(pages, "a"); ProbePage.Events.Clear();
            factory.Fail = true;
            view.SetPages(new[] { Page("new") });
            Assert.That(view.CurrentPageId, Is.Null);
            Assert.That(view.LastError, Does.Contain("expected factory failure"));
            Assert.That(ProbePage.Events.FindAll(x => x == "exit:a").Count, Is.EqualTo(1));
        }
        [Test] public void CachedPagesRebindOnRevisionButNotTitleChange()
        {
            view.SetPages(pages, "a"); view.Select("b", false); view.Select("a", false);
            ProbePage.Events.Clear();
            pages[1].Item.Title = "new title"; view.SetPages(pages);
            CollectionAssert.DoesNotContain(ProbePage.Events, "unbind:b");
            pages[1].PageRevision++; view.SetPages(pages);
            CollectionAssert.Contains(ProbePage.Events, "unbind:b");
            CollectionAssert.Contains(ProbePage.Events, "bind:b");
            CollectionAssert.DoesNotContain(ProbePage.Events, "enter:b");
        }
        [Test] public void SelectionCallbackCanQueueAnotherSelection()
        {
            view.SetPages(pages, "a");
            view.SelectionChanged += e => { if (e.CurrentId == "b") view.Select("c", false); };
            view.Select("b", false);
            Assert.That(view.CurrentPageId, Is.EqualTo("c"));
        }
        [TestCase(1f/30)] [TestCase(1f/60)] [TestCase(.2f)]
        public void SeededSixThousandMixedOperationsAlwaysConverge(float deltaTime)
        {
            view.SetPages(pages, "a"); var random = new System.Random(1108);
            for (int i=0;i<6000;i++)
            {
                switch(random.Next(8))
                {
                    case 0: case 1: view.Select(pages[random.Next(3)].Id,true); break;
                    case 2: view.CancelInteraction(SegmentCancelReason.PointerCancelled); break;
                    case 3: view.SetPresented(random.Next(2)==0); break;
                    case 4: view.Select(pages[random.Next(3)].Id,false); break;
                    case 5: pager.Tick(deltaTime); break;
                    case 6: view.SetPages(pages); break;
                    case 7: view.SetDirection(random.Next(2)==0 ? SegmentLayoutDirection.LeftToRight : SegmentLayoutDirection.RightToLeft); break;
                }
                Assert.That(float.IsNaN(pager.Position),Is.False,"seed1108 step"+i);
                Assert.That(pager.Position,Is.InRange(0,2));
            }
            view.SetPresented(true); pager.Tick(1);
            Assert.That(pager.Position,Is.EqualTo(System.Array.FindIndex(pages,p=>p.Id==view.CurrentPageId)));
            view.SetPresented(false);
            var balances=new Dictionary<string,int>();
            foreach(var e in ProbePage.Events)
            {
                var tokens=e.Split(':'); if(tokens[0]!="enter" && tokens[0]!="exit")continue;
                if(!balances.ContainsKey(tokens[1]))balances[tokens[1]]=0;
                balances[tokens[1]] += tokens[0]=="enter"?1:-1;
                Assert.That(balances[tokens[1]],Is.InRange(0,1),e);
            }
            foreach(var value in balances.Values)Assert.That(value,Is.Zero);
        }
        [Test] public void ChangingCachedSourceReleasesOldFactoryAndCreatesCorrectType()
        {
            view.SetPages(pages,"a"); view.Select("b",false); view.Select("a",false);
            var oldB=view.GetPage("b"); var replacement=new ProbeFactory();
            pages[1].Source=PageSource.FromFactory(replacement); view.SetPages(pages);
            Assert.That(factory.Released,Is.EqualTo(1));
            view.Select("b",false);
            Assert.That(replacement.Created,Is.EqualTo(1)); Assert.That(view.GetPage("b"),Is.Not.SameAs(oldB));
        }
        [Test] public void ResizeAndZeroWidthCancelWithoutChangingCommittedPage()
        {
            view.SetPages(pages,"a"); view.Select("c",true);pager.Tick(.08f);
            pager.Viewport.sizeDelta=new Vector2(0,600);pager.RefreshLayout();
            Assert.That(view.CurrentPageId,Is.EqualTo("a"));Assert.That(pager.IsSettling,Is.False);
            Assert.That(view.CanReceiveInput,Is.False);
            pager.Viewport.sizeDelta=new Vector2(700,600);pager.RefreshLayout();
            Assert.That(pager.Position,Is.Zero);Assert.That(view.CanReceiveInput,Is.True);
        }
        [Test] public void FailedCurrentRebindIsNotRetriedWithinSameUpdate()
        {
            view.SetPages(pages,"a"); int calls=0;
            pages[0].PageRevision++;
            pages[0].BindPage=p=>{ calls++; if(calls==1)throw new InvalidOperationException("rebind failed"); };
            view.SetPages(pages);
            Assert.That(calls,Is.EqualTo(1));Assert.That(view.CurrentPageId,Is.Null);
            Assert.That(view.LastError,Does.Contain("rebind failed"));
            view.Select("a",false);
            Assert.That(calls,Is.EqualTo(2));Assert.That(view.CurrentPageId,Is.EqualTo("a"));
        }
        [Test] public void AllExistingInstancesAreHiddenUntilChosen()
        {
            SegmentPage Existing(string id)
            {
                var slot=new GameObject("Slot"+id,typeof(RectTransform));slot.transform.SetParent(pager.Content,false);
                var page=new GameObject(id,typeof(RectTransform),typeof(ProbePage));page.transform.SetParent(slot.transform,false);
                return page.GetComponent<ProbePage>();
            }
            var a=Existing("a");var b=Existing("b");
            view.SetPages(new[]{new SegmentPageDescriptor{Id="a",Source=PageSource.FromExisting(a)},new SegmentPageDescriptor{Id="b",Source=PageSource.FromExisting(b)}});
            var group=b.transform.parent.GetComponent<CanvasGroup>();
            Assert.That(group,Is.Not.Null);Assert.That(group.alpha,Is.Zero);Assert.That(group.blocksRaycasts,Is.False);
            Assert.That(b.PageId,Is.Null,"non-current existing page remains lazily bound");
        }
        [Test] public void DisablingCanQueueSnapshotChangeFromExitCallback()
        {
            view.SetPages(pages,"a");view.Select("b",false);view.Select("a",false);
            ProbePage.ExitHook=p=>view.SetPages(Array.Empty<SegmentPageDescriptor>());
            root.SetActive(false);
            DeliverDisable(); // Explicit lifecycle delivery in EditMode (no player loop).
            Assert.That(view.CurrentPageId,Is.Null);Assert.That(pager.PageCount,Is.Zero);
        }
        sealed class PoolFactory : ISegmentPageFactory
        {
            public SegmentPage Retained;
            public SegmentPage Create(Transform parent)
            {
                var go=new GameObject("PoolPage",typeof(RectTransform),typeof(ProbePage));go.transform.SetParent(parent,false);return go.GetComponent<ProbePage>();
            }
            public void Release(SegmentPage page){Retained=page;page.gameObject.SetActive(false);}
        }
        [Test] public void ReleasingFactoryPageDoesNotDestroyItsPooledInstance()
        {
            var pool=new PoolFactory();view.SetPages(new[]{new SegmentPageDescriptor{Id="pooled",Source=PageSource.FromFactory(pool)}});
            view.SetPages(Array.Empty<SegmentPageDescriptor>());
            try {Assert.That(pool.Retained != null,Is.True,"factory owns page release even when it doesn't reparent");}
            finally {if(pool.Retained!=null)UnityEngine.Object.DestroyImmediate(pool.Retained.gameObject);}
        }
        void DeliverDisable() => typeof(SegmentView).GetMethod("OnDisable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(view,null);
        [Test] public void QueuedPresentationExitCannotReenterSnapshotCommit()
        {
            bool insideExit=false,nestedCommit=false;
            ProbePage.EnterHook=p=>{ if(p.PageId=="a"){root.SetActive(false);DeliverDisable();} };
            ProbePage.ExitHook=p=>{ insideExit=true;view.SetPages(Array.Empty<SegmentPageDescriptor>());insideExit=false; };
            view.SelectionChanged+=change=>{if(change.CurrentId==null)nestedCommit=insideExit;};
            view.SetPages(pages,"a");
            Assert.That(nestedCommit,Is.False);Assert.That(view.CurrentPageId,Is.Null);
        }
        [Test] public void ExistingCurrentRebindFailureHidesItsUnboundView()
        {
            var slot=new GameObject("ExistingSlot",typeof(RectTransform));slot.transform.SetParent(pager.Content,false);
            var node=new GameObject("Existing",typeof(RectTransform),typeof(ProbePage));node.transform.SetParent(slot.transform,false);
            var d=new SegmentPageDescriptor{Id="existing",Source=PageSource.FromExisting(node.GetComponent<ProbePage>())};
            view.SetPages(new[]{d});
            Assert.That(slot.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
            d.PageRevision++;d.BindPage=p=>throw new InvalidOperationException("expected rebind failure");
            view.SetPages(new[]{d});
            Assert.That(view.CurrentPageId,Is.Null);
            Assert.That(slot.GetComponent<CanvasGroup>().alpha,Is.Zero);
            Assert.That(slot.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
        }
    }
}
