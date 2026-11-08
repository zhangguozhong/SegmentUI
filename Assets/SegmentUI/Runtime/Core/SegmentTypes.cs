using System;
using UnityEngine;

namespace SegmentUI
{
    public enum SegmentLayoutDirection { Inherit, LeftToRight, RightToLeft }
    public enum SegmentSelectionSource { Code, Click, Gesture, Data }
    public enum SegmentSelectionResult { Accepted, Queued, NoChange, Invalid, Unavailable }
    public enum SegmentCancelReason { PointerCancelled, Hidden, Disabled, FocusLost, LayoutChanged, DataChanged, Destroyed }
    public enum SegmentPhase { Empty, Idle, PendingGesture, DraggingHorizontal, DraggingVertical, Settling, Suspended }
    public enum PageSourceKind { Prefab, Factory, ExistingInstance }

    [Serializable]
    public sealed class SegmentItemData
    {
        public string Title;
        public Sprite Icon;
        public int BadgeCount;
        public bool IsEnabled = true;
        public SegmentItemData Copy() => (SegmentItemData)MemberwiseClone();
    }

    public interface ISegmentPageFactory
    {
        SegmentPage Create(Transform parent);
        void Release(SegmentPage page);
    }

    [Serializable]
    public sealed class PageSource
    {
        public PageSourceKind Kind;
        public SegmentPage Prefab;
        public SegmentPage ExistingInstance;
        public int Version;
        [NonSerialized] public ISegmentPageFactory Factory;
        public static PageSource FromPrefab(SegmentPage prefab) => new PageSource { Kind = PageSourceKind.Prefab, Prefab = prefab };
        public static PageSource FromExisting(SegmentPage page) => new PageSource { Kind = PageSourceKind.ExistingInstance, ExistingInstance = page };
        public static PageSource FromFactory(ISegmentPageFactory factory, int version = 0) => new PageSource { Kind = PageSourceKind.Factory, Factory = factory, Version = version };
        public PageSource Copy() => (PageSource)MemberwiseClone();
        public bool SameIdentity(PageSource other) => other != null && Kind == other.Kind && Version == other.Version && Prefab == other.Prefab && ExistingInstance == other.ExistingInstance && ReferenceEquals(Factory, other.Factory);
    }

    [Serializable]
    public sealed class SegmentPageDescriptor
    {
        public string Id;
        public int PageRevision;
        public SegmentItemData Item = new SegmentItemData();
        public SegmentItemView ItemTemplate;
        public PageSource Source;
        [NonSerialized] public Action<SegmentPage> BindPage;
        public SegmentPageDescriptor Copy() => new SegmentPageDescriptor
        {
            Id = Id, PageRevision = PageRevision, Item = Item?.Copy(), ItemTemplate = ItemTemplate,
            Source = Source?.Copy(), BindPage = BindPage
        };
    }

    public readonly struct SegmentSelectionChange
    {
        public readonly string PreviousId, CurrentId;
        public readonly SegmentSelectionSource Source;
        public SegmentSelectionChange(string previous, string current, SegmentSelectionSource source)
        { PreviousId = previous; CurrentId = current; Source = source; }
    }
}
