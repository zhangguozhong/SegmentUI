using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SegmentUI
{
    public enum SegmentBarLayout { Equal, ContentWidth }
    [Serializable] public sealed class SegmentExistingItem { public string PageId; public SegmentItemView View; }

    /// <summary>维护标题实例、点击映射和指示器；标题滚动独立于内容分页。</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SegmentBar : MonoBehaviour
    {
        /// <summary>标题项容器，其排列和宽度由标题栏统一管理。</summary>
        public RectTransform ItemsRoot;
        /// <summary>没有单项 ItemTemplate 时使用的默认标题模板。</summary>
        public SegmentItemView DefaultTemplate;
        /// <summary>首次成功绑定时安装的默认指示器；显式 SetIndicators 后不再自动安装。</summary>
        [Tooltip("Installed once on first Bind unless SetIndicators was called explicitly. Empty SetIndicators disables defaults.")]
        public SegmentIndicator[] DefaultIndicators = Array.Empty<SegmentIndicator>();
        /// <summary>指示器的背景/前景挂载层，不应拦截射线。</summary>
        public RectTransform BackgroundLayer, ForegroundLayer;
        /// <summary>只滚动标题区域，与页面横向分页分离。</summary>
        public ScrollRect TitleScroll;
        /// <summary>Equal 等分且保留最小热区，ContentWidth 按标题内容计算宽度。</summary>
        public SegmentBarLayout LayoutMode;
        /// <summary>标题最小宽度、内容留白及项间距；有效点击宽度至少保留 96 UI 单位。</summary>
        public float MinimumItemWidth = 96, ItemPadding = 32, Spacing = 0;
        /// <summary>使用宿主已有标题实例及 ID 映射，解绑时隐藏但不销毁。</summary>
        public bool UseExistingItems;
        /// <summary>已有标题模式下的唯一 PageId 映射，每项必须带有效 Button。</summary>
        public SegmentExistingItem[] ExistingItems = Array.Empty<SegmentExistingItem>();
        /// <summary>选择变化时居中目标；关闭则只保证目标可见，UpdateItem 不主动触发两者。</summary>
        public bool AutoCenter = true;
        bool rightToLeft;
        /// <summary>切换标题物理排列并通知内部布局适配器，PageId 映射保持不变。</summary>
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
        /// <summary>热区或视口不满足要求时的诊断信息，此时相关按钮被禁用。</summary>
        public string LayoutError { get; private set; }
        /// <summary>有效标题点击通知页面 ID，分页宿主决定是否接受选择。</summary>
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
        /// <summary>整体交互开关，最终按钮可用性还取决于 Item.IsEnabled 与有效热区。</summary>
        public bool Interactable
        {
            get => interactable;
            set { interactable = value; UpdateInteractable(); }
        }
        /// <summary>注入标题容器、默认模板和指示器层；装饰层统一关闭射线。</summary>
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
        /// <summary>绑定前检查模板、现有标题映射及按钮引用，避免更新到一半才发现无效配置。</summary>
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
        /// <summary>结构性重绑会清理并重新建立标题及点击监听；只更新显示内容应使用 SegmentView.UpdateItem。</summary>
        public void Bind(IReadOnlyList<SegmentPageDescriptor> pages)
        {
            ValidateConfiguration(pages); ClearItems(); EnsureScroll(); PrepareLayers();
            // 映射可能包含当前快照没有使用的宿主标题，首次绑定时也需隐藏这些项。
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
        /// <summary>按稳定页面 ID 查找当前标题实例，不创建新实例。</summary>
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
        /// <summary>由布局签名决定是否重算宽度；内容宽度模式按文字、图标和角标计算。</summary>
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
            // 已授权的滚动接收器在手势期间必须保持启用。
            // 新输入由手势作用域拦截；这里禁用会意外取消正在进行的手势。
            if (TitleScroll != null) TitleScroll.enabled = usable && (TitleScroll is SegmentScrollRect || interactable);
        }
        /// <summary>使用连续分页位置计算标题权重和指示器帧，几何统一转换到指示器所在层。</summary>
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
        /// <summary>先验证并创建新指示器，成功后才释放旧指示器；空数组显式关闭。</summary>
        public void SetIndicators(SegmentIndicator[] templates)
        {
            var requested = templates ?? Array.Empty<SegmentIndicator>();
            // 完整验证新指示器配置后再替换旧视觉和默认安装状态。
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
        /// <summary>解除标题滚动监听、点击映射和指示器附加效果，避免复用残留事件。</summary>
        void OnDestroy()
        {
            if (TitleScroll != null) TitleScroll.onValueChanged.RemoveListener(OnTitleScrolled);
            ClearItems();
            ReleaseIndicators(indicators); indicators.Clear();
        }
    }
}
