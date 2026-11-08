using System;
using UnityEngine;

namespace SegmentUI
{
    /// <summary>布局方向配置；Inherit 从父分页继承，独立根默认为 LTR。</summary>
    public enum SegmentLayoutDirection { Inherit, LeftToRight, RightToLeft }
    public enum SegmentSelectionSource { Code, Click, Gesture, Data }
    /// <summary>选择请求结果：Accepted 接受、Queued 排队、NoChange 无变化、Invalid 无此 ID、Unavailable 不可用。</summary>
    public enum SegmentSelectionResult { Accepted, Queued, NoChange, Invalid, Unavailable }
    /// <summary>交互或展示结束原因，业务可据此区分取消、隐藏、结构变化与销毁。</summary>
    public enum SegmentCancelReason { PointerCancelled, Hidden, Disabled, FocusLost, LayoutChanged, DataChanged, Destroyed }
    /// <summary>交互阶段枚举；待判方向与正式横纵拖拽分开管理。</summary>
    public enum SegmentPhase { Empty, Idle, PendingGesture, DraggingHorizontal, DraggingVertical, Settling, Suspended }
    public enum PageSourceKind { Prefab, Factory, ExistingInstance }

    /// <summary>标题显示数据快照；UpdateItem 仅使用 Title/Icon/BadgeCount，IsEnabled 由 SetPages 管理。</summary>
    [Serializable]
    public sealed class SegmentItemData
    {
        /// <summary>显示标题；null 按空文本处理。</summary>
        public string Title;
        /// <summary>可选图标；null 隐藏图标节点。</summary>
        public Sprite Icon;
        /// <summary>正数显示数字角标，小于等于零隐藏。</summary>
        public int BadgeCount;
        /// <summary>SetPages 中控制页面是否可选；禁用标题仍保留，但不占分页序列。</summary>
        public bool IsEnabled = true;
        public SegmentItemData Copy() => (SegmentItemData)MemberwiseClone();
    }

    /// <summary>自定义创建/回收协议，可接对象池；Create 必须返回挂在指定 parent 下的页面。</summary>
    public interface ISegmentPageFactory
    {
        /// <summary>同步创建或借出页面，组件负责随后的业务绑定与展示。</summary>
        SegmentPage Create(Transform parent);
        /// <summary>组件已结束页面会话并从自有槽位脱离后调用；实现负责回收或销毁。</summary>
        void Release(SegmentPage page);
    }

    /// <summary>页面来源描述，Prefab、Factory、ExistingInstance 三者必须恰好有一个有效。</summary>
    [Serializable]
    public sealed class PageSource
    {
        public PageSourceKind Kind;
        public SegmentPage Prefab;
        public SegmentPage ExistingInstance;
        /// <summary>页面来源版本；递增可要求来源身份改变并释放旧实例。</summary>
        public int Version;
        [NonSerialized] public ISegmentPageFactory Factory;
        /// <summary>来源为 Prefab 资产，实例由组件创建与销毁，不能传场景对象。</summary>
        public static PageSource FromPrefab(SegmentPage prefab) => new PageSource { Kind = PageSourceKind.Prefab, Prefab = prefab };
        /// <summary>借用场景页面，不销毁实例；父槽位必须直接位于 Pager.Content 下。</summary>
        public static PageSource FromExisting(SegmentPage page) => new PageSource { Kind = PageSourceKind.ExistingInstance, ExistingInstance = page };
        /// <summary>来源为工厂；工厂引用或 version 改变会替换已有页面实例。</summary>
        public static PageSource FromFactory(ISegmentPageFactory factory, int version = 0) => new PageSource { Kind = PageSourceKind.Factory, Factory = factory, Version = version };
        public PageSource Copy() => (PageSource)MemberwiseClone();
        /// <summary>比较来源身份及版本；标题数据变化不参与页面实例替换判定。</summary>
        public bool SameIdentity(PageSource other) => other != null && Kind == other.Kind && Version == other.Version && Prefab == other.Prefab && ExistingInstance == other.ExistingInstance && ReferenceEquals(Factory, other.Factory);
    }

    /// <summary>用稳定 ID 将标题、模板、页面来源和业务绑定关联为一项。</summary>
    [Serializable]
    public sealed class SegmentPageDescriptor
    {
        /// <summary>唯一、非空、稳定的页面标识，不随标题或物理排列方向变化。</summary>
        public string Id;
        /// <summary>业务绑定版本；递增后 SetPages 保留实例但执行 Unbind/Bind。</summary>
        public int PageRevision;
        public SegmentItemData Item = new SegmentItemData();
        /// <summary>可选单项标题模板，未设置时使用 SegmentBar.DefaultTemplate。</summary>
        public SegmentItemView ItemTemplate;
        public PageSource Source;
        /// <summary>在页面 OnBind 前注入业务依赖；仅换委托不会重绑，应同时递增 PageRevision。</summary>
        [NonSerialized] public Action<SegmentPage> BindPage;
        /// <summary>复制结构和显示数据；Prefab、工厂、实例及绑定委托引用保持不变。</summary>
        public SegmentPageDescriptor Copy() => new SegmentPageDescriptor
        {
            Id = Id, PageRevision = PageRevision, Item = Item?.Copy(), ItemTemplate = ItemTemplate,
            Source = Source?.Copy(), BindPage = BindPage
        };
    }

    /// <summary>正式选择变化通知，包含前后页面 ID 与触发来源。</summary>
    public readonly struct SegmentSelectionChange
    {
        public readonly string PreviousId, CurrentId;
        public readonly SegmentSelectionSource Source;
        public SegmentSelectionChange(string previous, string current, SegmentSelectionSource source)
        { PreviousId = previous; CurrentId = current; Source = source; }
    }
}
