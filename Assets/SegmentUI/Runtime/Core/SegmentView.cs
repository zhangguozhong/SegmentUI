using System;
using System.Collections.Generic;
using UnityEngine;

namespace SegmentUI
{
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
        public SegmentPager Pager => pager;
        public SegmentBar Bar => bar;
        public IReadOnlyList<string> EnabledPageIds => enabledIds.AsReadOnly();
        public string CurrentPageId { get; private set; }
        public string TargetPageId { get; private set; }
        public SegmentPhase Phase { get; internal set; } = SegmentPhase.Empty;
        public bool IsTransitioning => pager != null && pager.IsSettling;
        public bool IsPresented => presented && ancestorPresented && isActiveAndEnabled;
        public bool CanReceiveInput => IsPresented && focused && pager != null && pager.HasLayout && CurrentPageId != null;
        public int DataVersion { get; private set; }
        public string LastError { get; private set; }
        public SegmentDiagnostics Diagnostics { get; } = new SegmentDiagnostics();
        public event Action<SegmentSelectionChange> SelectionChanged;
        public event Action<float> TransitionProgress;
        public event Action<string> ItemReselected;
        public event Action<string, string> SelectionFailed;
        public SegmentPage GetPage(string id) => host?.Get(id);

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
        /// Updates Title/Icon/BadgeCount in place without interrupting interaction or rebinding pages.
        /// IsEnabled is preserved; use SetPages for changes to the selectable page sequence.
        /// Returns false for an unknown ID or a disposing view. Reentrant updates are queued.
        /// </summary>
        public bool UpdateItem(string pageId, SegmentItemData itemData)
        {
            if (itemData == null) throw new ArgumentNullException(nameof(itemData));
            if (disposing || pageId == null || !pages.Exists(page => page.Id == pageId)) return false;
            var snapshot = itemData.Copy();
            Schedule(() =>
            {
                // A preceding queued SetPages may have removed this ID.
                var page = pages.Find(candidate => candidate.Id == pageId);
                if (disposing || page == null) return;
                snapshot.IsEnabled = page.Item.IsEnabled;
                page.Item = snapshot;
                if (bar != null) bar.UpdateItem(pageId, snapshot);
            });
            return true;
        }

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
        public void CancelInteraction(SegmentCancelReason reason)
        {
            if (!configured) return;
            CancelGesture(reason);
            if (Diagnostics.Enabled) Diagnostics.Record("Cancel " + reason);
            pendingSelection = null; pager.Stop(); TargetPageId = null; host.StopVerticalMotion();
            pager.SetPosition(Mathf.Max(0, enabledIds.IndexOf(CurrentPageId)));
            RefreshPhase(); Render();
        }
        public void SetPresented(bool value)
        {
            if (disposing) return;
            if (mutating) { commands.Enqueue(() => SetPresented(value)); return; }
            Run(() => { presented = value; UpdatePresentation(SegmentCancelReason.Hidden); });
        }
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
        public void SetIndicator(SegmentIndicator template) => SetIndicators(template == null ? Array.Empty<SegmentIndicator>() : new[] { template });
        public void SetIndicators(SegmentIndicator[] templates) { EnsureConfigured(); if (bar != null) { bar.SetIndicators(templates); Render(); } }
        void PublishSelection(string previous, string current, SegmentSelectionSource source) => Notify(() => SelectionChanged?.Invoke(new SegmentSelectionChange(previous, current, source)));
        void Notify(Action action) => SegmentPage.InvokeSafe(action);
        void Run(Action action)
        {
            bool previous = mutating; mutating = true;
            try { action(); } finally { mutating = previous; }
            if (!mutating) DrainCommands();
        }
        bool draining;
        void Schedule(Action action) { if (mutating) commands.Enqueue(() => Run(action)); else Run(action); }
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
        // Implemented by the input adapter partial. Kept separate from binding and presentation.
        internal bool IsGestureActive => Phase == SegmentPhase.PendingGesture || Phase == SegmentPhase.DraggingHorizontal || Phase == SegmentPhase.DraggingVertical;
        partial void CancelGesture(SegmentCancelReason reason);
    }
}
