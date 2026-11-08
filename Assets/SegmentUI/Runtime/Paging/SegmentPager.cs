using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>连续逻辑页坐标与吸附动画；容器宽度由布局组件管理，分页器只写横向位移。</summary>
    [DisallowMultipleComponent]
    public sealed class SegmentPager : MonoBehaviour
    {
        [SerializeField] RectTransform viewport, content;
        [SerializeField, Range(.05f, .5f)] float duration = .24f;
        int count, version;
        readonly List<RectTransform> slots = new List<RectTransform>();
        HorizontalLayoutGroup layout;
        ContentSizeFitter fitter;
        public bool RightToLeft { get; private set; }
        /// <summary>将屏幕横向拖动转成逻辑翻页方向；RTL 仅改变物理映射。</summary>
        public float DragSign => RightToLeft ? 1 : -1;
        float start, target, elapsed;
        Vector2 lastSize;
        /// <summary>连续页位置改变时触发，可能在两个页面索引之间。</summary>
        public event Action<float> PositionChanged;
        /// <summary>动画完成或立即定位后通知最终索引。</summary>
        public event Action<int> Settled;
        /// <summary>视口尺寸改变后通知宿主重新处理布局与交互。</summary>
        public event Action LayoutChanged;
        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public int PageCount => count;
        /// <summary>停止/替换动画时递增；旧动画回调不得提交新状态。</summary>
        public int Version => version;
        public bool HasLayout => viewport != null && viewport.rect.width > 0 && viewport.rect.height > 0;
        /// <summary>以页面为单位的连续逻辑坐标，0 表示第一张启用页。</summary>
        public float Position { get; private set; }
        public bool IsSettling { get; private set; }
        /// <summary>设置视口和 PageContent，建立布局组件与初始几何。</summary>
        public void Configure(RectTransform viewport, RectTransform content)
        {
            this.viewport = viewport; this.content = content;
            lastSize = viewport.rect.size;
            ApplyGeometry();
        }
        /// <summary>更新启用页数量并限制当前位置；会停止旧动画。</summary>
        public void SetPageCount(int count)
        {
            this.count = Mathf.Max(0, count);
            Stop(); SetPosition(Position); ApplyGeometry();
        }
        /// <summary>直接设置有限逻辑位置并限制在合法页范围，不主动提交业务选择。</summary>
        public void SetPosition(float position)
        {
            if (float.IsNaN(position) || float.IsInfinity(position)) throw new ArgumentOutOfRangeException(nameof(position));
            Position = Mathf.Clamp(position, 0, Mathf.Max(0, count - 1));
            if (content != null && viewport != null)
                content.anchoredPosition = new Vector2(-PhysicalPosition * viewport.rect.width, 0);
            PositionChanged?.Invoke(Position);
        }
        /// <summary>从当前视觉位置吸附到目标索引；animated=false 立即定位并触发 Settled。</summary>
        public void MoveTo(int index, bool animated)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            Stop(); start = Position; target = index; elapsed = 0;
            if (!animated || Mathf.Abs(start - target) < .00001f)
            { SetPosition(index); Settled?.Invoke(index); }
            else IsSettling = true;
        }
        /// <summary>按不受 timeScale 影响的时间推进动画；位置回调导致版本变化时放弃旧提交。</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!IsSettling || !HasLayout) return;
            int runningVersion = version;
            elapsed += Mathf.Max(0, unscaledDeltaTime);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
            SetPosition(Mathf.LerpUnclamped(start, target, t * t * (3 - 2 * t)));
            if (runningVersion != version || t < 1) return;
            IsSettling = false;
            Settled?.Invoke(Mathf.RoundToInt(target));
        }
        /// <summary>停止当前吸附并使旧版本失效，不改变当前连续位置。</summary>
        public void Stop() { version++; IsSettling = false; }
        /// <summary>视口尺寸真正改变时才重布局，并通知宿主取消不再适用的手势。</summary>
        public void RefreshLayout()
        {
            if (viewport == null) return;
            Vector2 size = viewport.rect.size;
            if (size == lastSize) return;
            lastSize = size;
            Stop(); ApplyGeometry(); LayoutChanged?.Invoke();
        }
        float PhysicalPosition => RightToLeft ? Mathf.Max(0, count - 1) - Position : Position;
        /// <summary>更新物理排列方向并停止旧动画，不改变逻辑索引。</summary>
        public void SetRightToLeft(bool value)
        {
            if (RightToLeft == value) return;
            RightToLeft = value; Stop(); ApplyGeometry();
        }
        /// <summary>PageContent 使用 HorizontalLayoutGroup 与 ContentSizeFitter，避免手工宽度与布局驱动冲突。</summary>
        public void EnsureLayoutComponents()
        {
            if (content == null) return;
            if (layout == null) layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (fitter == null) fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            layout.enabled = fitter.enabled = true;
            layout.padding = new RectOffset(); layout.spacing = 0;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childScaleWidth = layout.childScaleHeight = false;
            layout.reverseArrangement = RightToLeft;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
        internal void SetSlots(IReadOnlyList<RectTransform> ordered)
        {
            slots.Clear(); foreach (var slot in ordered) slots.Add(slot);
            if (content == null) return;
            // PageContent 是受管布局区域；禁用页和已释放的借用槽位
            // 即使仍在层级内，也不能参与内容首选宽度计算。
            foreach (Transform child in content)
            {
                if (!(child is RectTransform rect)) continue;
                var element = rect.GetComponent<LayoutElement>();
                if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = !slots.Contains(rect);
            }
            ApplyGeometry();
        }
        /// <summary>更新槽位尺寸并重建布局，内容宽度由 ContentSizeFitter 独占。</summary>
        public void ApplyGeometry()
        {
            if (viewport == null || content == null) return;
            EnsureLayoutComponents();
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);
            // 纵向尺寸跟随视口，横向尺寸仅由 ContentSizeFitter 驱动。
            content.sizeDelta = new Vector2(content.sizeDelta.x, 0);
            for (int i = 0; i < slots.Count; i++) if (slots[i] != null) LayoutSlot(slots[i], i);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = new Vector2(-PhysicalPosition * viewport.rect.width, 0);
        }
        /// <summary>槽位通过 LayoutElement 提供视口宽高；顺序由逻辑索引与布局组方向共同确定。</summary>
        public void LayoutSlot(RectTransform slot, int index)
        {
            slot.pivot = new Vector2(0, 1);
            var element = slot.GetComponent<LayoutElement>();
            if (element == null) element = slot.gameObject.AddComponent<LayoutElement>();
            element.enabled = true; element.ignoreLayout = false; element.layoutPriority = 100;
            element.minWidth = element.preferredWidth = Mathf.Max(0, viewport.rect.width);
            element.minHeight = element.preferredHeight = Mathf.Max(0, viewport.rect.height);
            element.flexibleWidth = element.flexibleHeight = 0;
            slot.SetSiblingIndex(index);
        }
        void Update() { RefreshLayout(); Tick(Time.unscaledDeltaTime); }
        void OnDisable() => Stop();
    }
}
