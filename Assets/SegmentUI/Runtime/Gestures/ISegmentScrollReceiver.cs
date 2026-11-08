using UnityEngine.EventSystems;
namespace SegmentUI
{
    /// <summary>Receives only drags authorized by the shared gesture scope.</summary>
    public interface ISegmentScrollReceiver
    {
        bool IsTitleBar { get; }
        void BeginOwned(PointerEventData data);
        void DragOwned(PointerEventData data);
        void EndOwned(PointerEventData data);
        void CancelOwned(PointerEventData data);
    }
}
