using UnityEngine.EventSystems;
namespace SegmentUI
{
    /// <summary>只接收共享手势作用域授权的拖拽回调。</summary>
    public interface ISegmentScrollReceiver
    {
        /// <summary>标题栏消费横向拖动，页面列表消费纵向拖动。</summary>
        bool IsTitleBar { get; }
        /// <summary>方向锁定并取得所有权后的起拖回调。</summary>
        void BeginOwned(PointerEventData data);
        /// <summary>当前所有者接收的连续位移；不得在此重新申请另一方向。</summary>
        void DragOwned(PointerEventData data);
        /// <summary>正常松手，可保留接收器自身的惯性。</summary>
        void EndOwned(PointerEventData data);
        /// <summary>失焦、隐藏或外部取消，必须结束拖拽并清理惯性。</summary>
        void CancelOwned(PointerEventData data);
    }
}
