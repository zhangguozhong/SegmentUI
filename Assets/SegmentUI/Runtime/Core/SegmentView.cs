using System;
using System.Collections.Generic;
using UnityEngine;

namespace SegmentUI
{
    /// <summary>分页业务入口：以稳定 PageId 管理数据、选择与展示周期；Unity 主线程调用。</summary>
    public sealed partial class SegmentView : MonoBehaviour
    {
        [SerializeField] SegmentPager pager;
        [SerializeField] SegmentBar bar;
        [SerializeField] SegmentPageDescriptor[] initialPages = Array.Empty<SegmentPageDescriptor>();
        [SerializeField] string initialPageId;
        [SerializeField] SegmentLayoutDirection direction = SegmentLayoutDirection.Inherit;
        readonly List<SegmentPageDescriptor> pages = new List<SegmentPageDescriptor>();
        readonly List<string> enabledIds = new List<string>();
        readonly Queue<Action> commands = new Queue<Action>();
        SegmentPageHost host;
        bool presented = true, ancestorPresented = true, focused = true, mutating, configured, rebuilding, disposing;
        string pendingSelection;
        bool pendingAnimated;
        SegmentSelectionSource selectionSource;
        /// <summary>底层分页位置与动画驱动器；业务切页优先调用 Select。</summary>
        public SegmentPager Pager => pager;
        /// <summary>标题栏实例，可读取标题项或配置样式；允许为空。</summary>
        public SegmentBar Bar => bar;
        /// <summary>当前允许分页的逻辑 ID 顺序，RTL 不改变此顺序。</summary>
        public IReadOnlyList<string> EnabledPageIds => enabledIds.AsReadOnly();
        /// <summary>已正式提交的页面 ID；动画目标不会提前覆盖它。</summary>
        public string CurrentPageId { get; private set; }
        /// <summary>当前切页目标；提交或取消后清空。</summary>
        public string TargetPageId { get; private set; }
        /// <summary>交互状态，用于区分待判方向、横纵拖拽、吸附及暂停。</summary>
        public SegmentPhase Phase { get; internal set; } = SegmentPhase.Empty;
        /// <summary>是否正在执行分页吸附动画，不包含手指直接拖动。</summary>
        public bool IsTransitioning => pager != null && pager.IsSettling;
        /// <summary>综合自身、祖先页面和组件启用状态得到的有效展示状态。</summary>
        public bool IsPresented => presented && ancestorPresented && isActiveAndEnabled;
        /// <summary>页面已展示、拥有焦点且视口有效时才允许开始新输入。</summary>
        public bool CanReceiveInput => IsPresented && focused && pager != null && pager.HasLayout && CurrentPageId != null;
        /// <summary>SetPages 提交次数；纯视觉 UpdateItem 不递增。</summary>
        public int DataVersion { get; private set; }
        /// <summary>最近一次创建或选择失败的说明；成功开始新选择时清空。</summary>
        public string LastError { get; private set; }
        /// <summary>可选事件诊断缓冲；按需开启以跟踪选择、提交和取消。</summary>
        public SegmentDiagnostics Diagnostics { get; } = new SegmentDiagnostics();
        /// <summary>仅正式提交的页面 ID 改变后通知，不在拖拽预览时通知。</summary>
        public event Action<SegmentSelectionChange> SelectionChanged;
        /// <summary>连续逻辑分页位置通知；值可以位于两个索引之间。</summary>
        public event Action<float> TransitionProgress;
        /// <summary>点击当前已提交标题时通知，代码重复 Select 不触发。</summary>
        public event Action<string> ItemReselected;
        /// <summary>创建或绑定失败时通知目标 ID 与错误信息。</summary>
        public event Action<string, string> SelectionFailed;
        /// <summary>只查询已经创建的页面，不触发懒加载；未创建时返回 null。</summary>
        public SegmentPage GetPage(string id) => host?.Get(id);

        /// <summary>绑定前注入分页器与可选标题栏；初始化后不能重复配置。</summary>
        public void Configure(SegmentPager pager, SegmentBar bar)
        {
            if (configured) throw new InvalidOperationException("Configure before binding pages.");
            this.pager = pager; this.bar = bar; EnsureConfigured();
        }
        void EnsureConfigured()
        {
            if (configured) return;
            if (pager == null || pager.Viewport == null || pager.Content == null)
                throw new InvalidOperationException("Assign Pager viewport/content before using SegmentView.");
            configured = true; host = new SegmentPageHost(pager);
            pager.PositionChanged += OnPositionChanged; pager.Settled += OnSettled; pager.LayoutChanged += OnLayoutChanged;
            if (bar != null) bar.ItemClicked += OnItemClicked;
            var parentPage = GetComponentInParent<SegmentPage>();
            ancestorPresented = parentPage == null || parentPage.IsPresented;
            pager.SetRightToLeft(IsRightToLeft);
            if (bar != null) bar.RightToLeft = IsRightToLeft;
        }
        void Start()
        {
            if (configured && pages.Count > 0) return;
            if (pager != null && initialPages.Length > 0) SetPages(initialPages, string.IsNullOrEmpty(initialPageId) ? null : initialPageId);
        }
        /// <summary>校验并复制整份描述列表，取消当前交互后协调页面；省略初始 ID 时优先保留当前页。</summary>
        public void SetPages(IReadOnlyList<SegmentPageDescriptor> input, string initialPageId = null)
        {
            if (disposing) return;
            EnsureConfigured();
            var snapshot = Validate(input, initialPageId);
            if (mutating) { commands.Enqueue(() => SetPages(snapshot, initialPageId)); return; }
            Run(() => ApplyPages(snapshot, initialPageId));
        }
        List<SegmentPageDescriptor> Validate(IReadOnlyList<SegmentPageDescriptor> input, string initial)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var result = new List<SegmentPageDescriptor>(); var ids = new HashSet<string>();
            var instances = new HashSet<SegmentPage>();
            var existingSlots = new HashSet<Transform>();
            foreach (var original in input)
            {
                if (original == null || string.IsNullOrWhiteSpace(original.Id) || !ids.Add(original.Id) || original.Item == null || original.Source == null)
                    throw new ArgumentException("Each page needs a unique nonempty Id, Item and Source.");
                var d = original.Copy(); var s = d.Source;
                bool valid = s.Kind == PageSourceKind.Factory ? s.Factory != null && s.Prefab == null && s.ExistingInstance == null
                    : s.Kind == PageSourceKind.Prefab ? s.Prefab != null && s.Factory == null && s.ExistingInstance == null && !s.Prefab.gameObject.scene.IsValid()
                    : s.Kind == PageSourceKind.ExistingInstance && s.ExistingInstance != null && s.Prefab == null && s.Factory == null;
                if (!valid) throw new ArgumentException("Page source must be exactly one valid asset, factory or instance: " + d.Id);
                if (s.Kind == PageSourceKind.ExistingInstance && (!instances.Add(s.ExistingInstance) || !existingSlots.Add(s.ExistingInstance.transform.parent) || s.ExistingInstance.transform.parent == null || s.ExistingInstance.transform.parent.parent != pager.Content))
                    throw new ArgumentException("Existing pages must have a unique PageSlot directly under Content: " + d.Id);
                result.Add(d);
            }
            if (initial != null && !result.Exists(d => d.Id == initial && d.Item.IsEnabled))
                throw new ArgumentException("Initial page must exist and be enabled.");
            if (bar != null) bar.ValidateConfiguration(result);
            return result;
        }
        /// <summary>结构更新先取消交互，再协调实例、槽位与标题；仅最终候选页进入展示周期。</summary>
        void ApplyPages(List<SegmentPageDescriptor> snapshot, string initial)
        {
            CancelInteraction(SegmentCancelReason.DataChanged);
            string previous = CurrentPageId;
            int previousIndex = enabledIds.IndexOf(previous);
            rebuilding = true; DataVersion++; LastError = null;
            pages.Clear(); pages.AddRange(snapshot); enabledIds.Clear();
            foreach (var d in pages) if (d.Item.IsEnabled) enabledIds.Add(d.Id);
            string candidate = initial ?? (enabledIds.Contains(previous) ? previous : enabledIds.Count == 0 ? null : enabledIds[Mathf.Clamp(previousIndex, 0, enabledIds.Count - 1)]);
            try
            {
                host.Reconcile(pages); pager.SetPageCount(enabledIds.Count); host.Layout(enabledIds);
                if (bar != null) bar.Bind(pages);
                if (candidate != null)
                {
                    try { host.Prepare(candidate, enabledIds.IndexOf(candidate)); }
                    catch (Exception e) { LastError = e.Message; candidate = null; }
                }
                CurrentPageId = candidate; TargetPageId = null;
                if (previous != candidate) host.Present(previous, false, SegmentCancelReason.DataChanged);
                host.Present(candidate, IsPresented, SegmentCancelReason.DataChanged);
                pager.SetPosition(Mathf.Max(0, enabledIds.IndexOf(candidate))); host.Layout(enabledIds);
            }
            finally { rebuilding = false; }
            RefreshPhase(); Render();
            if (previous != CurrentPageId) PublishSelection(previous, CurrentPageId, SegmentSelectionSource.Data);
            if (LastError != null) Notify(() => SelectionFailed?.Invoke(initial ?? candidate, LastError));
        }
        /// <summary>
        /// 原地更新标题、图标和角标，不重建页面、不重新绑定业务数据，也不打断拖拽或切页动画。
        /// 保留当前 IsEnabled；改变可选页面序列时应调用 SetPages。在 Unity 主线程调用。
        /// </summary>
        /// <param name="pageId">已通过 SetPages 注册的稳定页面 ID。</param>
        /// <param name="itemData">复制并替换 Title、Icon、BadgeCount；未填写的字段也会覆盖旧值。</param>
        /// <returns>
        /// 接受更新返回 true；ID 不存在、为 null 或组件正在销毁时返回 false。
        /// 回调内的更新会排队；若执行前目标页已被移除，则跳过，true 不代表已立即刷新。
        /// </returns>
        /// <exception cref="ArgumentNullException">itemData 为 null。</exception>
        public bool UpdateItem(string pageId, SegmentItemData itemData)
        {
            if (itemData == null) throw new ArgumentNullException(nameof(itemData));
            if (disposing || pageId == null || !pages.Exists(page => page.Id == pageId)) return false;
            // 请求时复制，避免排队期间调用方继续修改对象而改变本次更新内容。
            var snapshot = itemData.Copy();
            Schedule(() =>
            {
                // 前面的排队操作可能已通过 SetPages 移除此 ID，执行时重新查找。
                var page = pages.Find(candidate => candidate.Id == pageId);
                if (disposing || page == null) return;
                // 启用状态影响分页索引与手势目标，视觉刷新不能顺带修改它。
                snapshot.IsEnabled = page.Item.IsEnabled;
                page.Item = snapshot;
                if (bar != null) bar.UpdateItem(pageId, snapshot);
            });
            return true;
        }

        /// <summary>按 ID 请求切页；回调重入或手势进行中会排队，返回值区分接受、排队和无效目标。</summary>
        public SegmentSelectionResult Select(string pageId, bool animated = true) => RequestSelect(pageId, animated, SegmentSelectionSource.Code);
        SegmentSelectionResult RequestSelect(string id, bool animated, SegmentSelectionSource source)
        {
            if (disposing) return SegmentSelectionResult.Unavailable;
            EnsureConfigured();
            var descriptor = pages.Find(d => d.Id == id);
            if (descriptor == null) return SegmentSelectionResult.Invalid;
            if (!descriptor.Item.IsEnabled) return SegmentSelectionResult.Unavailable;
            if (mutating) { commands.Enqueue(() => RequestSelect(id, animated, source)); return SegmentSelectionResult.Queued; }
            if (IsGestureActive)
            { pendingSelection = id; pendingAnimated = animated; return SegmentSelectionResult.Queued; }
            if (CurrentPageId == id && TargetPageId == null && Mathf.Abs(pager.Position - enabledIds.IndexOf(id)) < .00001f)
            { if (source == SegmentSelectionSource.Click) Run(() => Notify(() => ItemReselected?.Invoke(id))); return SegmentSelectionResult.NoChange; }
            Run(() => StartSelection(id, animated, source));
            return LastError == null ? SegmentSelectionResult.Accepted : SegmentSelectionResult.Unavailable;
        }
        /// <summary>先准备目标页，成功后才启动动画；准备失败保持可恢复的当前状态。</summary>
        void StartSelection(string id, bool animated, SegmentSelectionSource source)
        {
            if (Diagnostics.Enabled) Diagnostics.Record("Select " + id + " via " + source);
            LastError = null;
            host.AllowRetry(id);
            try { host.Prepare(id, enabledIds.IndexOf(id)); }
            catch (Exception e) { FailSelection(id, e); return; }
            selectionSource = source; TargetPageId = id; Phase = SegmentPhase.Settling;
            pager.MoveTo(enabledIds.IndexOf(id), animated && IsPresented && pager.HasLayout);
            Render();
        }
        void OnSettled(int index)
        {
            if (rebuilding || index < 0 || index >= enabledIds.Count) return;
            if (mutating) { Commit(index); return; }
            Run(() => Commit(index));
        }
        /// <summary>吸附结束才提交选择：先退出旧页，再进入新页，最后发布选择通知。</summary>
        void Commit(int index)
        {
            string next = enabledIds[index], old = CurrentPageId;
            try { host.Prepare(next, index); }
            catch (Exception e) { FailSelection(next, e); return; }
            CurrentPageId = next; TargetPageId = null;
            if (Diagnostics.Enabled) Diagnostics.Record("Commit " + old + " -> " + next);
            RefreshPhase();
            if (old != next) host.Present(old, false, SegmentCancelReason.PointerCancelled);
            host.Present(next, IsPresented, SegmentCancelReason.PointerCancelled);
            Render();
            if (old != next) PublishSelection(old, next, selectionSource);
        }
        void FailSelection(string id, Exception error)
        {
            if (Diagnostics.Enabled) Diagnostics.Record("Failed " + id + ": " + error.Message);
            LastError = error.Message; pager.Stop(); TargetPageId = null;
            pager.SetPosition(Mathf.Max(0, enabledIds.IndexOf(CurrentPageId)));
            RefreshPhase(); Render(); Notify(() => SelectionFailed?.Invoke(id, LastError));
        }
        void OnItemClicked(string id) { if (!IsGestureActive) RequestSelect(id, true, SegmentSelectionSource.Click); }
        void OnPositionChanged(float position)
        {
            if (rebuilding) return;
            if (mutating) { Render(); Notify(() => TransitionProgress?.Invoke(position)); }
            else Run(() => { Render(); Notify(() => TransitionProgress?.Invoke(position)); });
        }
        /// <summary>按当前连续位置准备相邻预览页；仅控制显示和射线，不把预览当成 Enter。</summary>
        void Render()
        {
            if (!configured || rebuilding) return;
            int left = Mathf.FloorToInt(pager.Position), right = Mathf.CeilToInt(pager.Position);
            if (CurrentPageId != null && enabledIds.Count > 0)
            {
                for (int i = left; i <= right; i++)
                    if (i >= 0 && i < enabledIds.Count)
                        try { host.Prepare(enabledIds[i], i); }
                        catch (Exception e) { LastError = e.Message; }
            }
            bool stable = CurrentPageId != null && Mathf.Abs(pager.Position - enabledIds.IndexOf(CurrentPageId)) < .00001f;
            host.Render(pager.Position, enabledIds, CurrentPageId, Phase == SegmentPhase.Idle || (Phase == SegmentPhase.PendingGesture && stable), IsPresented);
            if (bar != null)
            {
                bar.Interactable = IsPresented && focused && Phase != SegmentPhase.DraggingHorizontal && Phase != SegmentPhase.DraggingVertical;
                bar.TargetPageId = TargetPageId;
                bar.TransitionVersion = pager.Version;
                bar.Render(pager.Position, enabledIds, CurrentPageId, selectionSource);
            }
        }
        void OnLayoutChanged()
        { CancelInteraction(SegmentCancelReason.LayoutChanged); host?.Layout(enabledIds); Render(); }
        /// <summary>释放手势、停止横纵惯性并回到已提交页；不销毁缓存页面。</summary>
        public void CancelInteraction(SegmentCancelReason reason)
        {
            if (!configured) return;
            CancelGesture(reason);
            if (Diagnostics.Enabled) Diagnostics.Record("Cancel " + reason);
            pendingSelection = null; pager.Stop(); TargetPageId = null; host.StopVerticalMotion();
            pager.SetPosition(Mathf.Max(0, enabledIds.IndexOf(CurrentPageId)));
            RefreshPhase(); Render();
        }
        /// <summary>宿主导航隐藏/返回时主动设置展示状态；CanvasGroup 隐藏不会自动触发生命周期。</summary>
        public void SetPresented(bool value)
        {
            if (disposing) return;
            if (mutating) { commands.Enqueue(() => SetPresented(value)); return; }
            Run(() => { presented = value; UpdatePresentation(SegmentCancelReason.Hidden); });
        }
        /// <summary>父页面展示状态向子分页传播，同时保留子分页当前选择。</summary>
        internal void SetAncestorPresented(bool value, SegmentCancelReason reason)
        {
            if (disposing) return;
            Schedule(() => { ancestorPresented = value; UpdatePresentation(reason); });
        }
        void UpdatePresentation(SegmentCancelReason reason)
        {
            if (!configured) return;
            if (!IsPresented) { CancelInteraction(reason); host.ExitAll(reason); }
            else host.Present(CurrentPageId, true, reason);
            RefreshPhase(); Render();
        }
        void RefreshPhase()
        { Phase = CurrentPageId == null ? SegmentPhase.Empty : !IsPresented || !focused || !pager.HasLayout ? SegmentPhase.Suspended : pager.IsSettling ? SegmentPhase.Settling : SegmentPhase.Idle; }
        /// <summary>用单个指示器模板替换现有指示器；null 表示关闭。</summary>
        public void SetIndicator(SegmentIndicator template) => SetIndicators(template == null ? Array.Empty<SegmentIndicator>() : new[] { template });
        /// <summary>整体替换指示器模板数组；空数组关闭，模板在标题栏的前景/背景层实例化。</summary>
        public void SetIndicators(SegmentIndicator[] templates) { EnsureConfigured(); if (bar != null) { bar.SetIndicators(templates); Render(); } }
        void PublishSelection(string previous, string current, SegmentSelectionSource source) => Notify(() => SelectionChanged?.Invoke(new SegmentSelectionChange(previous, current, source)));
        void Notify(Action action) => SegmentPage.InvokeSafe(action);
        /// <summary>状态变更期间拦截回调重入，当前操作完成后再执行后续命令。</summary>
        void Run(Action action)
        {
            bool previous = mutating; mutating = true;
            try { action(); } finally { mutating = previous; }
            if (!mutating) DrainCommands();
        }
        bool draining;
        void Schedule(Action action) { if (mutating) commands.Enqueue(() => Run(action)); else Run(action); }
        /// <summary>每次最多执行 32 条，避免回调持续生成命令导致单帧无限循环。</summary>
        void DrainCommands()
        {
            if (draining || mutating) return;
            draining = true;
            try { for (int i = 0; i < 32 && commands.Count > 0; i++) commands.Dequeue()(); }
            finally { draining = false; }
        }
        void Update() { DrainCommands(); if (configured && pager.RightToLeft != IsRightToLeft) Schedule(RefreshDirection); }
        void OnEnable() { if (configured && !disposing) Schedule(() => UpdatePresentation(SegmentCancelReason.Disabled)); }
        void OnDisable() { if (configured && !disposing) Schedule(() => UpdatePresentation(SegmentCancelReason.Disabled)); }
        void OnApplicationFocus(bool value) { if (disposing) return; Schedule(() => { focused = value; if (!value) CancelInteraction(SegmentCancelReason.FocusLost); else { RefreshPhase(); Render(); } }); }
        void OnApplicationPause(bool value) => OnApplicationFocus(!value);
        /// <summary>销毁时先阻断新操作并解绑事件，再释放页面与排队命令。</summary>
        void OnDestroy()
        {
            if (!configured) return;
            disposing = true; mutating = true;
            CancelGesture(SegmentCancelReason.Destroyed);
            if (pager != null)
            {
                pager.Stop();
                pager.PositionChanged -= OnPositionChanged; pager.Settled -= OnSettled; pager.LayoutChanged -= OnLayoutChanged;
            }
            if (bar != null) bar.ItemClicked -= OnItemClicked;
            host.Dispose(); commands.Clear(); configured = false;
        }
        // 由输入适配分部实现，与数据绑定和展示逻辑分开维护。
        internal bool IsGestureActive => Phase == SegmentPhase.PendingGesture || Phase == SegmentPhase.DraggingHorizontal || Phase == SegmentPhase.DraggingVertical;
        partial void CancelGesture(SegmentCancelReason reason);
    }
}
