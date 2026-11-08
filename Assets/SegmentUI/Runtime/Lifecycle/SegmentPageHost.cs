using System;
using System.Collections.Generic;
using UnityEngine;

namespace SegmentUI
{
    /// <summary>页面资源管理器：槽位提前创建，页面按需创建，按来源负责销毁、归还或恢复借用状态。</summary>
    internal sealed class SegmentPageHost
    {
        sealed class Entry
        {
            public SegmentPageDescriptor Descriptor;
            public SegmentPage Page;
            public RectTransform Slot;
            public CanvasGroup Group;

        }
        sealed class Slot
        {
            public RectTransform Rect;
            public bool Owned;
            public PageSource Source;
        }
        readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly Dictionary<string, SegmentPageDescriptor> descriptors = new Dictionary<string, SegmentPageDescriptor>();
        readonly Dictionary<string, Exception> failures = new Dictionary<string, Exception>();
        sealed class BorrowedSlot
        {
            public CanvasGroup Group;
            public float Alpha;
            public bool Interactable, BlocksRaycasts, OwnsGroup;
        }
        readonly Dictionary<SegmentPage, BorrowedSlot> borrowed = new Dictionary<SegmentPage, BorrowedSlot>();
        readonly SegmentPager pager;
        public SegmentPageHost(SegmentPager pager) { this.pager = pager; }
        public SegmentPage Get(string id) => id != null && entries.TryGetValue(id, out var e) ? e.Page : null;
        /// <summary>按 ID 与来源协调缓存；仅 PageRevision 改变时重绑实例，来源变化或移除时释放。</summary>
        public void Reconcile(IReadOnlyList<SegmentPageDescriptor> pages)
        {
            failures.Clear();
            var incoming = new Dictionary<string, SegmentPageDescriptor>();
            foreach (var d in pages) incoming.Add(d.Id, d);
            foreach (var pair in new List<KeyValuePair<string, Entry>>(entries))
            {
                var e = pair.Value;
                if (!incoming.TryGetValue(pair.Key, out var next) || !e.Descriptor.Source.SameIdentity(next.Source))
                { Release(e); entries.Remove(pair.Key); continue; }
                if (e.Descriptor.PageRevision != next.PageRevision)
                {
                    e.Page.Unbind();
                    try { e.Page.Bind(next); e.Page.ApplyDirection(pager.RightToLeft); SegmentScrollRectAdapter.Install(e.Page.transform); }
                    catch (Exception error) { failures[pair.Key] = error; Release(e); entries.Remove(pair.Key); }
                }
                e.Descriptor = next;
            }
            descriptors.Clear(); foreach (var d in pages) descriptors.Add(d.Id, d);
            foreach (var pair in new List<KeyValuePair<string, Slot>>(slots))
                if (!incoming.TryGetValue(pair.Key, out var next) || !pair.Value.Source.SameIdentity(next.Source))
                { ReleaseSlot(pair.Value); slots.Remove(pair.Key); }
            foreach (var d in pages)
                if (!slots.ContainsKey(d.Id))
                {
                    bool owned = d.Source.Kind != PageSourceKind.ExistingInstance;
                    var rect = owned ? new GameObject("PageSlot-" + d.Id, typeof(RectTransform)).GetComponent<RectTransform>() : d.Source.ExistingInstance.transform.parent as RectTransform;
                    if (owned) rect.SetParent(pager.Content, false);
                    slots.Add(d.Id, new Slot { Rect = rect, Owned = owned, Source = d.Source });
                    if (owned)
                    {
                        var group = rect.gameObject.AddComponent<CanvasGroup>();
                        group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
                    }
                }
            var keep = new HashSet<SegmentPage>();
            foreach (var d in pages)
                if (d.Source.Kind == PageSourceKind.ExistingInstance)
                {
                    var page = d.Source.ExistingInstance; keep.Add(page);
                    if (!borrowed.ContainsKey(page))
                    {
                        var group = page.transform.parent.GetComponent<CanvasGroup>();
                        bool owns = group == null;
                        if (owns) group = page.transform.parent.gameObject.AddComponent<CanvasGroup>();
                        borrowed.Add(page, new BorrowedSlot { Group = group, Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts, OwnsGroup = owns });
                        group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
                    }
                }
            foreach (var pair in new List<KeyValuePair<SegmentPage, BorrowedSlot>>(borrowed))
                if (!keep.Contains(pair.Key)) { RestoreBorrowed(pair.Value); borrowed.Remove(pair.Key); }
        }
        /// <summary>显式选择时清除该页的失败记录，允许重新尝试创建。</summary>
        public void AllowRetry(string id) => failures.Remove(id);
        /// <summary>懒创建并绑定页面；工厂必须将实例放入给定槽位，失败会记录并清理半成品。</summary>
        public SegmentPage Prepare(string id, int index)
        {
            if (failures.TryGetValue(id, out var error)) throw error;
            if (entries.TryGetValue(id, out var existing))
            { return existing.Page; }
            var descriptor = descriptors[id];
            var e = new Entry { Descriptor = descriptor, Slot = slots[id].Rect };
            try
            {
                if (descriptor.Source.Kind == PageSourceKind.ExistingInstance)
                {
                    e.Page = descriptor.Source.ExistingInstance;
                    e.Slot = e.Page.transform.parent as RectTransform;
                }
                else
                {
                    e.Page = descriptor.Source.Kind == PageSourceKind.Prefab
                        ? UnityEngine.Object.Instantiate(descriptor.Source.Prefab, e.Slot)
                        : descriptor.Source.Factory.Create(e.Slot);
                    if (e.Page == null) throw new InvalidOperationException("Page factory returned null: " + id);
                    if (e.Page.transform.parent != e.Slot) throw new InvalidOperationException("Factory must parent its page into the supplied slot: " + id);
                }
                var rect = e.Page.transform as RectTransform;
                if (rect == null) throw new ArgumentException("Page needs a RectTransform: " + id);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                e.Group = e.Slot.GetComponent<CanvasGroup>();
                if (e.Group == null) e.Group = e.Slot.gameObject.AddComponent<CanvasGroup>();
                e.Group.alpha = 0; e.Group.interactable = false; e.Group.blocksRaycasts = false;
                e.Page.gameObject.SetActive(true);
                e.Page.Bind(descriptor);
                e.Page.ApplyDirection(pager.RightToLeft);
                SegmentScrollRectAdapter.Install(e.Page.transform);
                entries.Add(id, e);
                return e.Page;
            }
            catch (Exception failure) { failures[id] = failure; Release(e); throw; }
        }
        /// <summary>仅将启用页的槽位提交给布局器，不压缩尚未实例化页面的占位。</summary>
        public void Layout(IReadOnlyList<string> enabled)
        {
            var ordered = new List<RectTransform>();
            foreach (var id in enabled) if (slots.TryGetValue(id, out var slot)) ordered.Add(slot.Rect);
            pager.SetSlots(ordered);
        }
        /// <summary>只显示当前插值涉及的两页；未稳定提交的预览页不开放业务交互。</summary>
        public void Render(float position, IReadOnlyList<string> enabled, string current, bool interactive, bool visible)
        {
            string left = enabled.Count > 0 ? enabled[Mathf.Clamp(Mathf.FloorToInt(position), 0, enabled.Count - 1)] : null;
            string right = enabled.Count > 0 ? enabled[Mathf.Clamp(Mathf.CeilToInt(position), 0, enabled.Count - 1)] : null;
            foreach (var pair in entries)
            {
                var e = pair.Value;
                if (e.Page == null || e.Group == null) continue;
                bool drawn = visible && (pair.Key == left || pair.Key == right);
                e.Group.alpha = drawn ? 1 : 0;
                e.Group.blocksRaycasts = drawn;
                e.Group.interactable = drawn && interactive && pair.Key == current;
            }
        }
        public void Present(string id, bool value, SegmentCancelReason reason) => Get(id)?.Present(value, reason);
        public void ExitAll(SegmentCancelReason reason)
        { foreach (var e in new List<Entry>(entries.Values)) if (e.Page != null) e.Page.Present(false, reason); }
        /// <summary>停止全部已创建页及嵌套列表惯性，避免新的分页手势与旧滚动同时推进。</summary>
        public void StopVerticalMotion()
        {
            foreach (var e in entries.Values)
                if (e.Page != null)
                    foreach (var scroll in e.Page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)) scroll.StopMovement();
        }
        public void Dispose()
        {
            var releasing = new List<Entry>(entries.Values);
            entries.Clear(); descriptors.Clear(); failures.Clear();
            foreach (var e in releasing) Release(e);
            foreach (var slot in slots.Values) ReleaseSlot(slot);
            slots.Clear(); pager.SetSlots(Array.Empty<RectTransform>());
            foreach (var e in borrowed.Values) RestoreBorrowed(e);
            borrowed.Clear();
        }
        /// <summary>恢复借用槽位原有 CanvasGroup 属性；仅删除本组件额外创建的 Group。</summary>
        static void RestoreBorrowed(BorrowedSlot slot)
        {
            if (slot.Group == null) return;
            if (slot.OwnsGroup) Destroy(slot.Group);
            else { slot.Group.alpha = slot.Alpha; slot.Group.interactable = slot.Interactable; slot.Group.blocksRaycasts = slot.BlocksRaycasts; }
        }
        /// <summary>先结束业务生命周期，再按来源释放；工厂回收与借用实例不走普通 Prefab 销毁路径。</summary>
        static void Release(Entry e)
        {
            if (e.Group != null) { e.Group.alpha = 0; e.Group.interactable = false; e.Group.blocksRaycasts = false; }
            if (e.Page != null)
            {
                e.Page.Release();
                if (e.Descriptor.Source.Kind == PageSourceKind.Factory)
                {
                    // 工厂可能只入池而不重挂，因此释放自有槽位前先脱离页面。
                    if (e.Page.transform.IsChildOf(e.Slot)) e.Page.transform.SetParent(null, false);
                    SegmentPage.InvokeSafe(() => e.Descriptor.Source.Factory.Release(e.Page));
                }
                else if (e.Descriptor.Source.Kind == PageSourceKind.Prefab)
                {
                    // 延迟 Destroy 前同一槽位可能重试创建，先隐藏失败页避免短暂重复显示。
                    e.Page.gameObject.SetActive(false);
                    Destroy(e.Page.gameObject);
                }
            }

        }
        public void ApplyDirection(bool rightToLeft)
        { foreach (var e in new List<Entry>(entries.Values)) if (e.Page != null) e.Page.ApplyDirection(rightToLeft); }
        static void ReleaseSlot(Slot slot)
        {
            if (!slot.Owned || slot.Rect == null) return;
            slot.Rect.gameObject.SetActive(false); slot.Rect.SetParent(null, false);
            Destroy(slot.Rect.gameObject);
        }
        internal static void Destroy(UnityEngine.Object obj)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
    }
}
