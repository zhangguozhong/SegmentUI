using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SegmentUI
{
    public enum SegmentBarLayout { Equal, ContentWidth }
    [Serializable] public sealed class SegmentExistingItem { public string PageId; public SegmentItemView View; }

    [RequireComponent(typeof(RectTransform))]
    public class SegmentBar : MonoBehaviour
    {
        public RectTransform ItemsRoot;
        public SegmentItemView DefaultTemplate;
        [Tooltip("Installed once on first Bind unless SetIndicators was called explicitly. Empty SetIndicators disables defaults.")]
        public SegmentIndicator[] DefaultIndicators = Array.Empty<SegmentIndicator>();
        public RectTransform BackgroundLayer, ForegroundLayer;
        public ScrollRect TitleScroll;
        public SegmentBarLayout LayoutMode;
        public float MinimumItemWidth = 96, ItemPadding = 32, Spacing = 0;
        public bool UseExistingItems;
        public SegmentExistingItem[] ExistingItems = Array.Empty<SegmentExistingItem>();
        public bool AutoCenter = true;
        bool rightToLeft;
        public bool RightToLeft
        {
            get => rightToLeft;
            set
            {
                if (rightToLeft == value) return;
                rightToLeft = value;
                foreach (var item in ordered) item.ApplyDirection(value);
                hasLayout = false; lastCentered = null;
                LayoutItems(); RefreshVisuals();
            }
        }
        public string TargetPageId { get; set; }
        public int TransitionVersion { get; set; }
        public string LayoutError { get; private set; }
        public event Action<string> ItemClicked;
        readonly Dictionary<string, SegmentItemView> items = new Dictionary<string, SegmentItemView>();
        readonly Dictionary<SegmentItemView, UnityAction> listeners = new Dictionary<SegmentItemView, UnityAction>();
        readonly HashSet<SegmentItemView> owned = new HashSet<SegmentItemView>();
        readonly List<SegmentItemView> ordered = new List<SegmentItemView>();
        readonly List<SegmentIndicator> indicators = new List<SegmentIndicator>();
        bool interactable = true, rendering, indicatorsInitialized, updatingItem;
        int layoutVersion, layoutSignature;
        bool hasLayout;
        string lastCentered;
        float lastPosition;
        IReadOnlyList<string> lastIds;
        string lastCurrent;
        SegmentSelectionSource lastSource;
        public bool Interactable
        {
            get => interactable;
            set { interactable = value; UpdateInteractable(); }
        }
        public void Configure(RectTransform itemsRoot, SegmentItemView defaultTemplate, RectTransform backgroundLayer, RectTransform foregroundLayer)
        {
            ItemsRoot = itemsRoot; DefaultTemplate = defaultTemplate; BackgroundLayer = backgroundLayer; ForegroundLayer = foregroundLayer;
            PrepareLayers(); EnsureScroll();
        }
        void PrepareLayers()
        {
            foreach (var layer in new[] { BackgroundLayer, ForegroundLayer })
            {
                if (layer == null) continue;
                var group = layer.GetComponent<CanvasGroup>(); if (group == null) group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                foreach (var graphic in layer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            if (BackgroundLayer != null) BackgroundLayer.SetAsFirstSibling();
            if (ForegroundLayer != null) ForegroundLayer.SetAsLastSibling();
        }
        void EnsureScroll()
        {
            if (ItemsRoot == null) return;
            if (TitleScroll == null)
            {
                TitleScroll = GetComponent<ScrollRect>(); if (TitleScroll == null) TitleScroll = gameObject.AddComponent<SegmentScrollRect>();
                TitleScroll.viewport = (RectTransform)transform;
                if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            }
            if (TitleScroll is SegmentScrollRect controlled) controlled.IsTitleBar = true;
            else if (GetComponentInParent<SegmentView>() != null) throw new ArgumentException("SegmentView titles require SegmentScrollRect for shared input arbitration; ordinary ScrollRect is unsupported.");
            TitleScroll.content = ItemsRoot; TitleScroll.horizontal = true; TitleScroll.vertical = false;
            TitleScroll.movementType = ScrollRect.MovementType.Clamped;
            TitleScroll.onValueChanged.RemoveListener(OnTitleScrolled); TitleScroll.onValueChanged.AddListener(OnTitleScrolled);
        }
        public void ValidateConfiguration(IReadOnlyList<SegmentPageDescriptor> pages)
        {
            if (ItemsRoot == null) throw new ArgumentException("SegmentBar requires an ItemsRoot.");
            var configuredScroll = TitleScroll != null ? TitleScroll : GetComponent<ScrollRect>();
            if (configuredScroll != null && !(configuredScroll is SegmentScrollRect) && GetComponentInParent<SegmentView>() != null)
                throw new ArgumentException("SegmentView titles require SegmentScrollRect for shared input arbitration; ordinary ScrollRect is unsupported.");
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            var ids = new HashSet<string>(); var existing = new Dictionary<string, SegmentItemView>();
            if (UseExistingItems)
            {
                var views = new HashSet<SegmentItemView>();
                foreach (var entry in ExistingItems ?? Array.Empty<SegmentExistingItem>())
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.PageId) || entry.View == null || existing.ContainsKey(entry.PageId) || !views.Add(entry.View) || entry.View.transform.parent != ItemsRoot)
                        throw new ArgumentException("Existing Items require unique PageId/View pairs directly beneath ItemsRoot.");
                    existing.Add(entry.PageId, entry.View);
                }
            }
            foreach (var page in pages)
            {
                if (page == null || string.IsNullOrWhiteSpace(page.Id) || !ids.Add(page.Id) || page.Item == null) throw new ArgumentException("Segment Items require unique nonempty Ids and Item data.");
                if (UseExistingItems)
                {
                    if (page.ItemTemplate != null || !existing.ContainsKey(page.Id)) throw new ArgumentException("Existing Item mode requires a PageId mapping and cannot use per-page templates.");
                    if (existing[page.Id].Button == null) throw new ArgumentException("Existing Item requires a Button.");
                }
                else if ((page.ItemTemplate != null ? page.ItemTemplate : DefaultTemplate) == null) throw new ArgumentException("Segment Item template is missing for " + page.Id);
                else if ((page.ItemTemplate != null ? page.ItemTemplate : DefaultTemplate).Button == null) throw new ArgumentException("Segment Item template requires a Button for " + page.Id);
            }
        }
        public void Bind(IReadOnlyList<SegmentPageDescriptor> pages)
        {
            ValidateConfiguration(pages); ClearItems(); EnsureScroll(); PrepareLayers();
            // The mapping may contain host-owned Items absent from the current snapshot, including on first bind.
            if (UseExistingItems) foreach (var entry in ExistingItems) entry.View.gameObject.SetActive(false);
            foreach (var page in pages)
            {
                SegmentItemView item = null;
                if (UseExistingItems) { foreach (var entry in ExistingItems) if (entry.PageId == page.Id) { item = entry.View; break; } }
                else { item = Instantiate(page.ItemTemplate != null ? page.ItemTemplate : DefaultTemplate, ItemsRoot); owned.Add(item); }
                item.gameObject.SetActive(true); item.transform.SetAsLastSibling(); item.PageId = page.Id; item.Bind(page.Item.Copy()); item.ApplyDirection(RightToLeft);
                string id = page.Id;
                UnityAction click = () => { if (interactable && item.IsEnabled && item.Button != null && item.Button.interactable) ItemClicked?.Invoke(id); };
                item.Button.onClick.AddListener(click); listeners.Add(item, click); items.Add(id, item); ordered.Add(item);
            }
            lastCentered = null; hasLayout = false; LayoutItems(); Interactable = interactable;
            if (!indicatorsInitialized) SetIndicators(DefaultIndicators);
        }
        /// <summary>复用已有标题项，只刷新内容和布局，保留分页进度及标题栏滚动状态。</summary>
        internal void UpdateItem(string id, SegmentItemData data)
        {
            var item = GetItem(id);
            if (item == null) return;
            updatingItem = true;
            try
            {
                // Bind 会重置选中视觉；保存当前动画权重与已提交状态，更新后立即恢复。
                float weight = item.SelectedWeight; bool committed = item.IsCommitted;
                item.Bind(data.Copy());
                // 重新应用方向，让 RTL 适配器处理 Bind 后新增或更新的内部内容。
                item.ApplyDirection(RightToLeft);
                item.ApplyVisualState(weight, committed);
                LayoutItems();
                // 重布局会使居中记录失效；本次只刷新指示器，不触发居中或清空滚动速度。
                // 记录当前目标，避免下一帧 Render 再次自动居中。
                lastCentered = TargetPageId ?? lastCurrent;
                RefreshVisuals();
            }
            finally { updatingItem = false; }
        }
        public SegmentItemView GetItem(string id) => id != null && items.TryGetValue(id, out var item) ? item : null;
        void ClearItems()
        {
            foreach (var item in ordered)
            {
                if (item == null) continue;
                if (item.Button != null && listeners.TryGetValue(item, out var listener)) item.Button.onClick.RemoveListener(listener);
                item.Unbind(); item.gameObject.SetActive(false);
                if (owned.Contains(item)) Release(item.gameObject);
            }
            ordered.Clear(); items.Clear(); listeners.Clear(); owned.Clear(); lastIds = null;
        }
        static void Release(GameObject go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
        float ViewWidth => TitleScroll != null && TitleScroll.viewport != null ? TitleScroll.viewport.rect.width : ((RectTransform)transform).rect.width;
        int GetLayoutSignature()
        {
            var viewport = TitleScroll != null && TitleScroll.viewport != null ? TitleScroll.viewport : (RectTransform)transform;
            unchecked
            {
                int hash = viewport.rect.size.GetHashCode();
                hash = hash * 31 + RightToLeft.GetHashCode(); hash = hash * 31 + (int)LayoutMode; hash = hash * 31 + MinimumItemWidth.GetHashCode();
                hash = hash * 31 + ItemPadding.GetHashCode(); hash = hash * 31 + Spacing.GetHashCode();
                foreach (var item in ordered)
                {
                    hash = hash * 31 + item.GetInstanceID();
                    if (item.Label != null) hash = hash * 31 + item.Label.preferredWidth.GetHashCode();
                    if (item.Icon != null && item.Icon.gameObject.activeSelf) hash = hash * 31 + item.Icon.rectTransform.rect.width.GetHashCode();
                    if (item.Badge != null && item.Badge.gameObject.activeSelf) hash = hash * 31 + item.Badge.preferredWidth.GetHashCode();
                }
                return hash;
            }
        }
        void LayoutItems()
        {
            if (ItemsRoot == null || ViewWidth <= 0) { UpdateInteractable(); return; }
            int signature = GetLayoutSignature();
            if (hasLayout && signature == layoutSignature) { UpdateInteractable(); return; }
            hasLayout = true; layoutSignature = signature; layoutVersion++; lastCentered = null;
            float minimum = Mathf.Max(96, MinimumItemWidth), spacing = Mathf.Max(0, Spacing);
            var layout = ItemsRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null && LayoutMode == SegmentBarLayout.Equal) layout = ItemsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.enabled = LayoutMode == SegmentBarLayout.Equal;
            ItemsRoot.anchorMin = new Vector2(0, 0); ItemsRoot.anchorMax = new Vector2(0, 1); ItemsRoot.pivot = new Vector2(0, .5f);
            float width = Mathf.Max(ViewWidth, ordered.Count * minimum + Mathf.Max(0, ordered.Count - 1) * spacing);
            if (LayoutMode == SegmentBarLayout.ContentWidth)
            {
                width = 0;
                foreach (var item in ordered)
                {
                    float preferred = item.Label != null ? item.Label.preferredWidth : minimum;
                    if (item.Icon != null && item.Icon.gameObject.activeSelf) preferred += item.Icon.rectTransform.rect.width + 8;
                    if (item.Badge != null && item.Badge.gameObject.activeSelf) preferred += Mathf.Max(24, item.Badge.preferredWidth) + 8;
                    float itemWidth = Mathf.Max(minimum, preferred + ItemPadding);
                    var rect = (RectTransform)item.transform; rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, .5f);
                    rect.sizeDelta = new Vector2(itemWidth, 0); rect.anchoredPosition = new Vector2(width, 0); width += itemWidth + spacing;
                }
                width = Mathf.Max(ViewWidth, width - (ordered.Count > 0 ? spacing : 0));
                if (RightToLeft)
                    foreach (var item in ordered)
                    {
                        var rect = (RectTransform)item.transform;
                        rect.anchoredPosition = new Vector2(width - rect.anchoredPosition.x - rect.rect.width, 0);
                    }
            }
            else
            {
                layout.reverseArrangement = RightToLeft;
                layout.spacing = spacing; layout.padding = new RectOffset(); layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
                foreach (var item in ordered)
                {
                    var element = item.GetComponent<LayoutElement>(); if (element == null) element = item.gameObject.AddComponent<LayoutElement>();
                    element.minWidth = minimum; element.preferredWidth = minimum; element.flexibleWidth = 1; element.minHeight = 96; element.preferredHeight = 96;
                }
            }
            ItemsRoot.sizeDelta = new Vector2(width, 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ItemsRoot);
            float maximumOffset = Mathf.Max(0, width - ViewWidth);
            float offset = lastIds == null ? (RightToLeft ? maximumOffset : 0) : Mathf.Clamp(-ItemsRoot.anchoredPosition.x, 0, maximumOffset);
            ItemsRoot.anchoredPosition = new Vector2(-offset, 0);
            UpdateInteractable();
        }
        void UpdateInteractable()
        {
            var viewport = TitleScroll != null && TitleScroll.viewport != null ? TitleScroll.viewport : (RectTransform)transform;
            bool usable = viewport.rect.width > 0 && viewport.rect.height >= 96;
            LayoutError = usable ? null : "Segment title viewport must have positive width and at least 96 UI units of height.";
            foreach (var item in ordered)
            {
                if (item.Button == null) continue;
                var hitTransform = item.Button.transform as RectTransform;
                bool valid = usable && hitTransform != null;
                if (valid)
                {
                    var itemRect = SegmentIndicator.RectInLayer((RectTransform)item.transform, ItemsRoot);
                    var hitRect = SegmentIndicator.RectInLayer(hitTransform, ItemsRoot);
                    valid = hitRect.width >= 95.99f && hitRect.height >= 95.99f && hitRect.xMin >= itemRect.xMin - .01f && hitRect.xMax <= itemRect.xMax + .01f && hitRect.yMin >= itemRect.yMin - .01f && hitRect.yMax <= itemRect.yMax + .01f;
                    if (!valid) LayoutError = "Segment Item '" + item.PageId + "' Button must cover at least 96 × 96 UI units within its Item.";
                }
                item.Button.interactable = interactable && item.IsEnabled && valid;
            }
            // A controlled receiver must stay enabled during its own accepted gesture.
            // SegmentGestureScope gates new input; disabling here would cancel the in-flight owner.
            if (TitleScroll != null) TitleScroll.enabled = usable && (TitleScroll is SegmentScrollRect || interactable);
        }
        public void Render(float position, IReadOnlyList<string> enabledIds, string currentId, SegmentSelectionSource source)
        {
            if (rendering) return;
            rendering = true;
            try
            {
                lastPosition = position; lastIds = enabledIds; lastCurrent = currentId; lastSource = source;
                LayoutItems();
                if (enabledIds == null || enabledIds.Count == 0 || float.IsNaN(position) || float.IsInfinity(position))
                {
                    foreach (var item in ordered) item.ApplyVisualState(0, false);
                    foreach (var indicator in indicators) indicator.ResetVisual(); return;
                }
                position = Mathf.Clamp(position, 0, enabledIds.Count - 1);
                int left = Mathf.FloorToInt(position), right = Mathf.Min(left + 1, enabledIds.Count - 1); float t = position - left;
                foreach (var item in ordered) item.ApplyVisualState(item.PageId == enabledIds[left] ? (left == right ? 1 : 1 - t) : item.PageId == enabledIds[right] ? t : 0, item.PageId == currentId);
                string centerId = TargetPageId ?? currentId;
                if (centerId != null && lastCentered != centerId)
                {
                    // 数据更新期间禁止居中；正常选择页面仍按 AutoCenter/Reveal 规则执行。
                    if (!updatingItem)
                    { if (AutoCenter) CenterItem(centerId); else RevealItem(centerId); }
                    lastCentered = centerId;
                }
                var a = GetItem(enabledIds[left]); var b = GetItem(enabledIds[right]); var committed = GetItem(currentId);
                if (a == null || b == null) return;
                foreach (var indicator in indicators)
                {
                    var layer = indicator.transform.parent as RectTransform;
                    if (LayoutError != null || layer == null || ((RectTransform)a.transform).rect.width <= 0 || ((RectTransform)a.transform).rect.height < 96) { indicator.ResetVisual(); continue; }
                    indicator.ApplyFrame(new SegmentIndicatorFrame
                    {
                        LeftPageId = a.PageId, RightPageId = b.PageId, CurrentPageId = currentId, TargetPageId = this.TargetPageId,
                        LeftItem = SegmentIndicator.RectInLayer((RectTransform)a.transform, layer), RightItem = SegmentIndicator.RectInLayer((RectTransform)b.transform, layer),
                        LeftContent = SegmentIndicator.RectInLayer(a.ContentRect, layer), RightContent = SegmentIndicator.RectInLayer(b.ContentRect, layer),
                        HasCommitted = committed != null, CommittedItem = committed == null ? default : SegmentIndicator.RectInLayer((RectTransform)committed.transform, layer),
                        CommittedContent = committed == null ? default : SegmentIndicator.RectInLayer(committed.ContentRect, layer), Progress = t, Source = source, LayoutVersion = layoutVersion, TransitionVersion = this.TransitionVersion
                    });
                    indicator.DisableRaycasts();
                }
            }
            finally { rendering = false; }
        }
        void CenterItem(string id)
        {
            var item = GetItem(id); if (item == null || TitleScroll == null) return;
            TitleScroll.StopMovement(); float max = Mathf.Max(0, ItemsRoot.rect.width - ViewWidth);
            Rect rect = SegmentIndicator.RectInLayer((RectTransform)item.transform, ItemsRoot);
            float offset = Mathf.Clamp(rect.center.x - ItemsRoot.rect.xMin - ViewWidth * .5f, 0, max);
            ItemsRoot.anchoredPosition = new Vector2(-offset, 0);
        }
        void RevealItem(string id)
        {
            var item = GetItem(id); if (item == null || TitleScroll == null) return;
            Rect rect = SegmentIndicator.RectInLayer((RectTransform)item.transform, ItemsRoot);
            float offset = -ItemsRoot.anchoredPosition.x;
            float min = rect.xMin - ItemsRoot.rect.xMin, max = rect.xMax - ItemsRoot.rect.xMin;
            if (rect.width > ViewWidth) offset = RightToLeft ? max - ViewWidth : min;
            else if (min < offset) offset = min;
            else if (max > offset + ViewWidth) offset = max - ViewWidth;
            TitleScroll.StopMovement();
            ItemsRoot.anchoredPosition = new Vector2(-Mathf.Clamp(offset, 0, Mathf.Max(0, ItemsRoot.rect.width - ViewWidth)), 0);
        }
        public void SetIndicators(SegmentIndicator[] templates)
        {
            var requested = templates ?? Array.Empty<SegmentIndicator>();
            // Validate the full replacement before touching the existing visuals or default-install state.
            foreach (var template in requested)
            {
                if (template == null) continue;
                if ((template.Layer == SegmentIndicatorLayer.Background ? BackgroundLayer : ForegroundLayer) == null)
                    throw new ArgumentException("Indicator layer is not configured.");
            }
            var staged = new List<SegmentIndicator>();
            try
            {
                foreach (var template in requested)
                {
                    if (template == null) continue;
                    var layer = template.Layer == SegmentIndicatorLayer.Background ? BackgroundLayer : ForegroundLayer;
                    var instance = Instantiate(template, layer); staged.Add(instance);
                    instance.DisableRaycasts(); instance.ResetVisual();
                }
            }
            catch { ReleaseIndicators(staged); throw; }
            ReleaseIndicators(indicators); indicators.Clear(); indicators.AddRange(staged);
            indicatorsInitialized = true; PrepareLayers(); RefreshVisuals();
        }
        static void ReleaseIndicators(List<SegmentIndicator> instances)
        {
            foreach (var indicator in instances)
            {
                if (indicator == null) continue;
                try { indicator.CancelEffects(); }
                finally { if (indicator != null) { indicator.gameObject.SetActive(false); Release(indicator.gameObject); } }
            }
        }
        void RefreshVisuals() { if (lastIds != null) Render(lastPosition, lastIds, lastCurrent, lastSource); }
        void OnTitleScrolled(Vector2 _) { RefreshVisuals(); }
        void OnRectTransformDimensionsChange() { if (!isActiveAndEnabled || rendering) return; hasLayout = false; lastCentered = null; RefreshVisuals(); }
        void LateUpdate() { RefreshVisuals(); }
        void OnDestroy()
        {
            if (TitleScroll != null) TitleScroll.onValueChanged.RemoveListener(OnTitleScrolled);
            ClearItems();
            ReleaseIndicators(indicators); indicators.Clear();
        }
    }
}
